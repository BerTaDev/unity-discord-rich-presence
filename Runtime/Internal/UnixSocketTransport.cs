#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>Talks to the discord-ipc-N Unix domain socket on macOS and Linux.</summary>
    internal sealed class UnixSocketTransport : IIpcTransport
    {
        // Relative to the temp dir: native install, Flatpak, Snap and Vesktop.
        static readonly string[] SubDirectories =
        {
            "",
            "app/com.discordapp.Discord",
            "app/com.discordapp.DiscordCanary",
            "app/com.discordapp.DiscordPTB",
            "snap.discord",
            "snap.discord-canary",
            ".flatpak/dev.vencord.Vesktop/xdg-run",
        };

        static List<string> searchDirectories;

        Socket socket;

        public bool TryConnect(int index)
        {
            foreach (string directory in GetSearchDirectories())
            {
                string path = Path.Combine(directory, "discord-ipc-" + index);
                Socket candidate = null;
                try
                {
                    candidate = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    candidate.Connect(new UnixEndPoint(path));
                    socket = candidate;
                    return true;
                }
                catch (Exception)
                {
                    candidate?.Dispose();
                }
            }
            return false;
        }

        public int Read(byte[] buffer)
        {
            int available = socket.Available;
            if (available == 0)
            {
                // "Readable" with nothing to read means the other side hung up.
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    throw new IOException("Discord closed the connection.");
                return 0;
            }
            return socket.Receive(buffer, 0, Math.Min(available, buffer.Length), SocketFlags.None);
        }

        public void Write(byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int sent = socket.Send(buffer, offset, count - offset, SocketFlags.None);
                if (sent <= 0) throw new IOException("Writing to the Discord socket failed.");
                offset += sent;
            }
        }

        public void Dispose()
        {
            socket?.Dispose();
            socket = null;
        }

        static List<string> GetSearchDirectories()
        {
            if (searchDirectories != null) return searchDirectories;

            string root = Env("XDG_RUNTIME_DIR") ?? Env("TMPDIR") ?? Env("TMP") ?? Env("TEMP") ?? "/tmp";
            var result = new List<string>();
            foreach (string sub in SubDirectories)
                result.Add(sub.Length == 0 ? root : Path.Combine(root, sub));
            if (root != "/tmp") result.Add("/tmp");
            return searchDirectories = result;
        }

        static string Env(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Minimal AF_UNIX endpoint (same layout as Mono.Unix.UnixEndPoint), so we don't depend on
        /// UnixDomainSocketEndPoint, which older Unity profiles don't ship.
        /// </summary>
        sealed class UnixEndPoint : EndPoint
        {
            readonly string path;

            public UnixEndPoint(string path)
            {
                this.path = path;
            }

            public override AddressFamily AddressFamily => AddressFamily.Unix;

            public override SocketAddress Serialize()
            {
                byte[] bytes = Encoding.UTF8.GetBytes(path);
                var address = new SocketAddress(AddressFamily.Unix, 2 + bytes.Length + 1);
                for (int i = 0; i < bytes.Length; i++)
                    address[2 + i] = bytes[i];
                address[2 + bytes.Length] = 0;
                return address;
            }

            public override EndPoint Create(SocketAddress address)
            {
                int length = address.Size - 2;
                var bytes = new byte[length];
                for (int i = 0; i < length; i++)
                    bytes[i] = address[2 + i];
                int end = Array.IndexOf(bytes, (byte)0);
                return new UnixEndPoint(Encoding.UTF8.GetString(bytes, 0, end < 0 ? length : end));
            }

            public override string ToString() => path;
        }
    }
}
#endif
