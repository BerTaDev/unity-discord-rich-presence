using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BerTaDev.DiscordRichPresence.Internal;
using NUnit.Framework;

namespace BerTaDev.DiscordRichPresence.Tests
{
    /// <summary>Drives the real client against an in-memory fake of the Discord desktop app.</summary>
    sealed class DiscordRpcClientTests
    {
        const string ClientId = "123456789012345678";
        const int Timeout = 3000;

        FakeDiscord discord;
        DiscordRpcClient client;

        [SetUp]
        public void SetUp()
        {
            discord = new FakeDiscord();
            client = new DiscordRpcClient(ClientId, 42, () => new FakeTransport(discord));
        }

        [TearDown]
        public void TearDown() => client.Dispose();

        [Test]
        public void Handshake_RaisesReadyWithUser()
        {
            DiscordUser user = null;
            var ready = new ManualResetEventSlim();
            client.Ready += u => { user = u; ready.Set(); };

            client.Start();

            Assert.IsTrue(ready.Wait(Timeout), "READY was not raised");
            Assert.AreEqual("Tester", user.DisplayName);
            StringAssert.Contains("\"client_id\":\"" + ClientId + "\"", discord.Handshakes.Single());
        }

        [Test]
        public void OnlyTheLatestActivityIsSent()
        {
            client.SetActivity("{\"state\":\"one\"}");
            client.SetActivity("{\"state\":\"two\"}");
            client.SetActivity("{\"state\":\"three\"}");
            client.Start();

            Assert.IsTrue(discord.WaitForActivities(1, Timeout));
            Thread.Sleep(300);
            var sent = discord.Activities;
            Assert.AreEqual(1, sent.Count);
            StringAssert.Contains("\"pid\":42", sent[0]);
            StringAssert.Contains("three", sent[0]);
        }

        [Test]
        public void RepeatedIdenticalActivity_IsSentOnce()
        {
            client.Start();
            for (int i = 0; i < 20; i++) client.SetActivity("{\"state\":\"same\"}");

            Assert.IsTrue(discord.WaitForActivities(1, Timeout));
            Thread.Sleep(300);
            Assert.AreEqual(1, discord.Activities.Count);
        }

        [Test]
        public void RateLimit_AllowsFiveUpdatesPerWindow()
        {
            client.Start();
            Assert.IsTrue(discord.WaitForReady(Timeout));

            for (int i = 0; i < 12; i++)
            {
                client.SetActivity("{\"state\":\"update " + i + "\"}");
                Thread.Sleep(30);
            }
            Thread.Sleep(500);

            Assert.AreEqual(DiscordRpcClient.RateLimitCount, discord.Activities.Count);
        }

        [Test]
        public void Ping_IsAnsweredWithPong()
        {
            client.Start();
            Assert.IsTrue(discord.WaitForReady(Timeout));

            discord.SendToClient(Opcode.Ping, "{\"n\":7}");

            Assert.IsTrue(SpinWait.SpinUntil(() => discord.Pongs.Contains("{\"n\":7}"), Timeout));
        }

        [Test]
        public void InvalidClientId_RaisesErrorAndStops()
        {
            discord.CloseWith = "{\"code\":4000,\"message\":\"Invalid Client ID\"}";
            DiscordError error = null;
            var errored = new ManualResetEventSlim();
            client.Error += e => { error = e; errored.Set(); };

            client.Start();

            Assert.IsTrue(errored.Wait(Timeout));
            Assert.AreEqual(4000, error.Code);
            Assert.IsTrue(SpinWait.SpinUntil(() => !client.IsRunning, Timeout));
        }

        [Test]
        public void ReconnectsAndRestoresActivity_WhenDiscordRestarts()
        {
            var disconnected = new ManualResetEventSlim();
            client.Disconnected += disconnected.Set;
            client.SetActivity("{\"state\":\"playing\"}");
            client.Start();
            Assert.IsTrue(discord.WaitForActivities(1, Timeout));

            discord.Restart();

            Assert.IsTrue(disconnected.Wait(Timeout), "Disconnected was not raised");
            Assert.IsTrue(discord.WaitForActivities(2, Timeout), "Activity was not restored after reconnecting");
            Assert.AreEqual(2, discord.Handshakes.Count);
        }

        [Test]
        public void Dispose_ClearsThePresence()
        {
            client.SetActivity("{\"state\":\"playing\"}");
            client.Start();
            Assert.IsTrue(discord.WaitForActivities(1, Timeout));

            client.Dispose();

            StringAssert.Contains("\"activity\":null", discord.Activities.Last());
        }

        /// <summary>In-memory stand-in for the Discord client: answers handshakes and records commands.</summary>
        sealed class FakeDiscord
        {
            readonly object gate = new object();
            readonly List<string> handshakes = new List<string>();
            readonly List<string> activities = new List<string>();
            readonly List<string> pongs = new List<string>();
            readonly List<byte> toClient = new List<byte>();
            int generation;
            bool hungUp;

            public string CloseWith;

            public List<string> Handshakes { get { lock (gate) return handshakes.ToList(); } }
            public List<string> Activities { get { lock (gate) return activities.ToList(); } }
            public List<string> Pongs { get { lock (gate) return pongs.ToList(); } }
            public int Generation { get { lock (gate) return generation; } }

            public bool WaitForReady(int timeout) => SpinWait.SpinUntil(() => Handshakes.Count > 0, timeout) && WaitIdle();

            public bool WaitForActivities(int count, int timeout) => SpinWait.SpinUntil(() => Activities.Count >= count, timeout);

            // Drops the current connection, like quitting and reopening Discord.
            public void Restart()
            {
                lock (gate)
                {
                    generation++;
                    toClient.Clear();
                }
            }

            public void SendToClient(Opcode opcode, string payload)
            {
                byte[] frame = FrameReader.Encode(opcode, payload, out int length);
                lock (gate) toClient.AddRange(frame.Take(length));
            }

            public void Receive(Opcode opcode, string payload)
            {
                lock (gate)
                {
                    switch (opcode)
                    {
                        case Opcode.Handshake:
                            handshakes.Add(payload);
                            break;
                        case Opcode.Pong:
                            pongs.Add(payload);
                            return;
                        default:
                            if (payload.Contains("SET_ACTIVITY")) activities.Add(payload);
                            return;
                    }
                }

                if (CloseWith != null)
                {
                    // Like the real client: write the reason, then hang up immediately.
                    SendToClient(Opcode.Close, CloseWith);
                    lock (gate) hungUp = true;
                }
                else
                    SendToClient(Opcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"user\":{\"id\":\"1\",\"username\":\"tester\",\"global_name\":\"Tester\",\"avatar\":null}}}");
            }

            public int Read(byte[] buffer)
            {
                lock (gate)
                {
                    int count = Math.Min(buffer.Length, toClient.Count);
                    if (count == 0 && hungUp) throw new IOException("Pipe closed");
                    toClient.CopyTo(0, buffer, 0, count);
                    toClient.RemoveRange(0, count);
                    return count;
                }
            }

            static bool WaitIdle()
            {
                Thread.Sleep(200);
                return true;
            }
        }

        sealed class FakeTransport : IIpcTransport
        {
            readonly FakeDiscord discord;
            readonly FrameReader reader = new FrameReader();
            int generation = -1;

            public FakeTransport(FakeDiscord discord) => this.discord = discord;

            public bool TryConnect(int index)
            {
                if (index != 0) return false;
                generation = discord.Generation;
                return true;
            }

            public int Read(byte[] buffer)
            {
                if (generation != discord.Generation) throw new IOException("Pipe closed");
                return discord.Read(buffer);
            }

            public void Write(byte[] buffer, int count)
            {
                if (generation != discord.Generation) throw new IOException("Pipe closed");
                reader.Append(buffer, count);
                while (reader.TryRead(out Opcode opcode, out string payload))
                    discord.Receive(opcode, payload);
            }

            public void Dispose() { }
        }
    }
}
