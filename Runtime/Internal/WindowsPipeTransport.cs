#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>
    /// Talks to \\.\pipe\discord-ipc-N through kernel32 directly. This avoids the
    /// NamedPipeClientStream quirks of Unity's Mono and lets us poll with PeekNamedPipe,
    /// so reads never block and one thread can both read and write.
    /// </summary>
    internal sealed class WindowsPipeTransport : IIpcTransport
    {
        const uint GenericRead = 0x80000000;
        const uint GenericWrite = 0x40000000;
        const uint OpenExisting = 3;

        SafeFileHandle handle;

        public bool TryConnect(int index)
        {
            try
            {
                SafeFileHandle h = CreateFileW(@"\\.\pipe\discord-ipc-" + index, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (h.IsInvalid)
                {
                    h.Dispose();
                    return false;
                }
                handle = h;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public int Read(byte[] buffer)
        {
            if (!PeekNamedPipe(handle, IntPtr.Zero, 0, IntPtr.Zero, out uint available, IntPtr.Zero))
            {
                // Discord hung up. Anything it wrote right before closing may still be buffered,
                // and ReadFile returns immediately on a broken pipe, so drain it first.
                int error = Marshal.GetLastWin32Error();
                if (ReadFile(handle, buffer, (uint)buffer.Length, out uint leftover, IntPtr.Zero) && leftover > 0)
                    return (int)leftover;
                throw new IOException("Discord pipe closed (Win32 error " + error + ").");
            }
            if (available == 0) return 0;

            uint toRead = Math.Min(available, (uint)buffer.Length);
            if (!ReadFile(handle, buffer, toRead, out uint read, IntPtr.Zero))
                throw new IOException("Reading from the Discord pipe failed (Win32 error " + Marshal.GetLastWin32Error() + ").");
            return (int)read;
        }

        public void Write(byte[] buffer, int count)
        {
            GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr start = pin.AddrOfPinnedObject();
                int offset = 0;
                while (offset < count)
                {
                    if (!WriteFile(handle, IntPtr.Add(start, offset), (uint)(count - offset), out uint written, IntPtr.Zero) || written == 0)
                        throw new IOException("Writing to the Discord pipe failed (Win32 error " + Marshal.GetLastWin32Error() + ").");
                    offset += (int)written;
                }
            }
            finally
            {
                pin.Free();
            }
        }

        public void Dispose()
        {
            handle?.Dispose();
            handle = null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PeekNamedPipe(SafeFileHandle pipe, IntPtr buffer, uint bufferSize, IntPtr bytesRead, out uint totalBytesAvailable, IntPtr bytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ReadFile(SafeFileHandle file, [Out] byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool WriteFile(SafeFileHandle file, IntPtr buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);
    }
}
#endif
