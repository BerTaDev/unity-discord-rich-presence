using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>
    /// Runs one background thread that finds the Discord client, does the handshake,
    /// answers pings, reconnects when Discord restarts and pushes the latest activity.
    /// Only the newest activity is ever sent, so callers can update as often as they like.
    /// Events are raised on the worker thread.
    /// </summary>
    internal sealed class DiscordRpcClient : IDisposable
    {
        const int PipeCount = 10;
        const int PollIntervalMs = 100;
        const int HandshakeTimeoutMs = 10000;
        const int MinRetryDelayMs = 1000;
        const int MaxRetryDelayMs = 15000;
        const int CloseInvalidClientId = 4000;

        // Discord accepts 5 activity updates per 20 seconds per connection.
        internal const int RateLimitCount = 5;
        internal const int RateLimitWindowMs = 20000;

        readonly string clientId;
        readonly int processId;
        readonly Func<IIpcTransport> transportFactory;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly object gate = new object();
        readonly Queue<long> sendTimes = new Queue<long>();
        readonly Stopwatch clock = Stopwatch.StartNew();

        Thread thread;
        volatile bool running;

        // Latest activity JSON requested by the game (null = cleared). Guarded by `gate`.
        string desiredActivity;

        // What Discord shows for the current connection. Worker thread only.
        string sentActivity;
        bool sentValid;
        int nonce;

        public DiscordRpcClient(string clientId, int processId, Func<IIpcTransport> transportFactory)
        {
            this.clientId = clientId;
            this.processId = processId;
            this.transportFactory = transportFactory;
        }

        public event Action<DiscordUser> Ready;
        public event Action Disconnected;
        public event Action<DiscordError> Error;

        public bool IsRunning => running;

        public void Start()
        {
            if (thread != null) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "Discord Rich Presence" };
            thread.Start();
        }

        /// <summary>Queues an activity (already serialized). Null clears the presence.</summary>
        public void SetActivity(string activityJson)
        {
            lock (gate) desiredActivity = activityJson;
            wake.Set();
        }

        public void Dispose()
        {
            running = false;
            stop.Set();
            wake.Set();
            if (thread != null && thread != Thread.CurrentThread) thread.Join(1000);
            thread = null;
        }

        void Run()
        {
            int retryDelay = MinRetryDelayMs;
            while (running)
            {
                IIpcTransport transport = Connect();
                if (transport != null)
                {
                    SessionResult result;
                    try
                    {
                        result = RunSession(transport);
                    }
                    finally
                    {
                        transport.Dispose();
                    }

                    if (result == SessionResult.Fatal)
                    {
                        running = false;
                        break;
                    }
                    if (result == SessionResult.WasReady) retryDelay = MinRetryDelayMs;
                }

                if (stop.WaitOne(retryDelay)) break;
                retryDelay = Math.Min(retryDelay * 2, MaxRetryDelayMs);
            }
        }

        IIpcTransport Connect()
        {
            for (int i = 0; i < PipeCount; i++)
            {
                IIpcTransport transport = transportFactory();
                if (transport.TryConnect(i)) return transport;
                transport.Dispose();
            }
            return null;
        }

        enum SessionResult
        {
            Failed,
            WasReady,
            Fatal,
        }

        SessionResult RunSession(IIpcTransport transport)
        {
            bool ready = false;
            try
            {
                return RunSessionLoop(transport, ref ready);
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
            {
                // Discord was closed or restarted; we'll reconnect.
                return ready ? SessionResult.WasReady : SessionResult.Failed;
            }
            catch (Exception e)
            {
                RaiseError(-1, "Discord connection error: " + e.Message);
                return ready ? SessionResult.WasReady : SessionResult.Failed;
            }
            finally
            {
                if (ready) Disconnected?.Invoke();
            }
        }

        SessionResult RunSessionLoop(IIpcTransport transport, ref bool ready)
        {
            var reader = new FrameReader();
            var readBuffer = new byte[16 * 1024];
            long handshakeDeadline = clock.ElapsedMilliseconds + HandshakeTimeoutMs;
            sentActivity = null;
            sentValid = false;

            var handshake = new StringBuilder("{\"v\":1,\"client_id\":");
            Json.WriteString(handshake, clientId);
            Send(transport, Opcode.Handshake, handshake.Append('}').ToString());

            while (running)
            {
                // Discord often writes a CLOSE frame (with the reason) and hangs up right away,
                // so frames already received are handled before the disconnect is reported.
                Exception readError = null;
                try
                {
                    int read;
                    while ((read = transport.Read(readBuffer)) > 0)
                        reader.Append(readBuffer, read);
                }
                catch (Exception e) when (e is IOException || e is SocketException)
                {
                    readError = e;
                }

                while (reader.TryRead(out Opcode opcode, out string payload))
                {
                    switch (opcode)
                    {
                        case Opcode.Frame:
                            if (HandleMessage(payload)) ready = true;
                            break;
                        case Opcode.Ping:
                            Send(transport, Opcode.Pong, payload);
                            break;
                        case Opcode.Close:
                            return HandleClose(payload, ready);
                    }
                }

                if (readError != null)
                    return ready ? SessionResult.WasReady : SessionResult.Failed;

                if (!ready && clock.ElapsedMilliseconds > handshakeDeadline)
                    return SessionResult.Failed;

                if (ready) Flush(transport);
                wake.WaitOne(PollIntervalMs);
            }

            // Shutting down: take the presence off the profile before closing the pipe.
            if (ready && sentValid && sentActivity != null)
                Send(transport, Opcode.Frame, BuildSetActivity(null));
            return ready ? SessionResult.WasReady : SessionResult.Failed;
        }

        // Sends the newest activity if it differs from what Discord shows and the rate limit allows it.
        void Flush(IIpcTransport transport)
        {
            string activity;
            lock (gate) activity = desiredActivity;

            if (sentValid && activity == sentActivity) return;
            if (!sentValid && activity == null)
            {
                // A fresh connection shows nothing, so there is nothing to clear.
                sentValid = true;
                return;
            }

            long now = clock.ElapsedMilliseconds;
            while (sendTimes.Count > 0 && now - sendTimes.Peek() >= RateLimitWindowMs)
                sendTimes.Dequeue();
            if (sendTimes.Count >= RateLimitCount) return; // try again on a later tick

            Send(transport, Opcode.Frame, BuildSetActivity(activity));
            sendTimes.Enqueue(now);
            sentActivity = activity;
            sentValid = true;
        }

        // Returns true when the message is the READY event.
        bool HandleMessage(string payload)
        {
            var message = Json.Parse(payload) as Dictionary<string, object>;
            if (message == null) return false;

            string evt = message.GetString("evt");
            Dictionary<string, object> data = message.GetObject("data");
            if (evt == "READY")
            {
                Dictionary<string, object> user = data.GetObject("user");
                Ready?.Invoke(new DiscordUser(
                    user.GetString("id"),
                    user.GetString("username"),
                    user.GetString("global_name"),
                    user.GetString("avatar")));
                return true;
            }

            if (evt == "ERROR")
                RaiseError((int)data.GetLong("code", -1), data.GetString("message") ?? "Unknown Discord error.");
            return false;
        }

        SessionResult HandleClose(string payload, bool ready)
        {
            var data = Json.Parse(payload) as Dictionary<string, object>;
            int code = (int)data.GetLong("code", -1);
            string message = data.GetString("message") ?? "Discord closed the connection.";

            if (code == CloseInvalidClientId)
            {
                RaiseError(code, message + ". Check the Application ID in the Discord Developer Portal.");
                return SessionResult.Fatal;
            }

            RaiseError(code, message);
            return ready ? SessionResult.WasReady : SessionResult.Failed;
        }

        string BuildSetActivity(string activity)
        {
            var sb = new StringBuilder(80 + (activity?.Length ?? 4));
            sb.Append("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":")
              .Append(processId.ToString(CultureInfo.InvariantCulture))
              .Append(",\"activity\":")
              .Append(activity ?? "null")
              .Append("},\"nonce\":\"")
              .Append((++nonce).ToString(CultureInfo.InvariantCulture))
              .Append("\"}");
            return sb.ToString();
        }

        void RaiseError(int code, string message) => Error?.Invoke(new DiscordError(code, message));

        static void Send(IIpcTransport transport, Opcode opcode, string payload)
        {
            byte[] frame = FrameReader.Encode(opcode, payload, out int length);
            transport.Write(frame, length);
        }
    }
}
