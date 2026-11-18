#if NETFRAMEWORK
namespace System
{
    internal readonly struct Index
    {
        private readonly int value;
        public Index(int value, bool fromEnd = false)
        {
            if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
            this.value = fromEnd ? ~value : value;
        }
        public int Value => value < 0 ? ~value : value;
        public bool IsFromEnd => value < 0;
        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;
        public static implicit operator Index(int value) => new Index(value);
    }
    internal readonly struct Range
    {
        public Range(Index start, Index end) { Start = start; End = end; }
        public Index Start { get; }
        public Index End { get; }
        public static Range StartAt(Index start) => new Range(start, new Index(0, true));
        public static Range EndAt(Index end) => new Range(new Index(0), end);
        public static Range All => new Range(new Index(0), new Index(0, true));
        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length), end = End.GetOffset(length);
            if (start < 0 || end > length || start > end) { throw new ArgumentOutOfRangeException(); }
            return (start, end - start);
        }
    }
}
#endif
