#if NETFRAMEWORK
namespace System.Collections.Generic
{
    internal static class FrameworkCollectionExtensions
    {
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }
        public static bool TryDequeue<T>(this Queue<T> queue, out T value)
        {
            if (queue.Count == 0) { value = default; return false; }
            value = queue.Dequeue();
            return true;
        }
    }
}
namespace System.IO
{
    internal static class FrameworkStreamExtensions
    {
        public static void Write(this Stream stream, ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty) return;
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(Math.Min(bytes.Length, 65536));
            try
            {
                while (!bytes.IsEmpty)
                {
                    int count = Math.Min(65536, bytes.Length);
                    bytes.Slice(0, count).CopyTo(buffer);
                    stream.Write(buffer, 0, count);
                    bytes = bytes.Slice(count);
                }
            }
            finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
        }
        public static void ReadExactly(this Stream stream, Span<byte> bytes)
        {
            if (bytes.IsEmpty) return;
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(Math.Min(bytes.Length, 65536));
            try
            {
                while (!bytes.IsEmpty)
                {
                    int count = stream.Read(buffer, 0, Math.Min(65536, bytes.Length));
                    if (count == 0) throw new EndOfStreamException();
                    buffer.AsSpan(0, count).CopyTo(bytes);
                    bytes = bytes.Slice(count);
                }
            }
            finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
        }
    }
}
#endif
