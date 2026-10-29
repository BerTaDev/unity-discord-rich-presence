using System;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>
    /// A byte stream to the local Discord client. Only ever used from the RPC worker thread,
    /// so implementations don't need to be thread-safe.
    /// </summary>
    internal interface IIpcTransport : IDisposable
    {
        /// <summary>Tries to open discord-ipc-{index}. Never throws.</summary>
        bool TryConnect(int index);

        /// <summary>
        /// Reads whatever is available without blocking. Returns 0 when nothing is waiting.
        /// Throws <see cref="System.IO.IOException"/> when Discord closed the connection.
        /// </summary>
        int Read(byte[] buffer);

        /// <summary>Writes all <paramref name="count"/> bytes. Throws <see cref="System.IO.IOException"/> on failure.</summary>
        void Write(byte[] buffer, int count);
    }
}
