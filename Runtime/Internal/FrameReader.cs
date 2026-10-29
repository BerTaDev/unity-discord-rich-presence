using System;
using System.IO;
using System.Text;

namespace BerTaDev.DiscordRichPresence.Internal
{
    internal enum Opcode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4,
    }

    /// <summary>
    /// Discord IPC frames are: int32 opcode, int32 length (both little-endian), then UTF-8 JSON.
    /// Bytes arrive in arbitrary chunks, so this buffers until a whole frame is there.
    /// </summary>
    internal sealed class FrameReader
    {
        const int HeaderSize = 8;
        const int MaxPayloadSize = 1024 * 1024;

        byte[] buffer = new byte[4096];
        int count;

        public void Append(byte[] data, int length)
        {
            if (count + length > buffer.Length)
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, count + length));
            Buffer.BlockCopy(data, 0, buffer, count, length);
            count += length;
        }

        public bool TryRead(out Opcode opcode, out string payload)
        {
            opcode = default;
            payload = null;
            if (count < HeaderSize) return false;

            int op = ReadInt32(0);
            int length = ReadInt32(4);
            if (length < 0 || length > MaxPayloadSize)
                throw new IOException("Invalid frame from Discord (length " + length + ").");
            if (count < HeaderSize + length) return false;

            opcode = (Opcode)op;
            payload = Encoding.UTF8.GetString(buffer, HeaderSize, length);

            int consumed = HeaderSize + length;
            count -= consumed;
            Buffer.BlockCopy(buffer, consumed, buffer, 0, count);
            return true;
        }

        public static byte[] Encode(Opcode opcode, string payload, out int length)
        {
            int payloadLength = Encoding.UTF8.GetByteCount(payload);
            length = HeaderSize + payloadLength;
            var frame = new byte[length];
            WriteInt32(frame, 0, (int)opcode);
            WriteInt32(frame, 4, payloadLength);
            Encoding.UTF8.GetBytes(payload, 0, payload.Length, frame, HeaderSize);
            return frame;
        }

        int ReadInt32(int offset) =>
            buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);

        static void WriteInt32(byte[] target, int offset, int value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }
    }
}
