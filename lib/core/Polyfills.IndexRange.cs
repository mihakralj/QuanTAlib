#if NETFRAMEWORK
using System.Runtime.InteropServices;

namespace System;

/// <summary>
/// net48 polyfill for <see cref="Index"/>. Meziantou.Polyfill can supply these, but it generates
/// them as internal, which conflicts with the public <c>RingBuffer.this[Index]</c> indexer. These
/// copies are public so the library's public surface is identical on every target framework.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Index : IEquatable<Index>
{
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        _value = fromEnd ? ~value : value;
    }

    public static Index Start => new(0);

    public static Index End => new(0, fromEnd: true);

    public int Value => _value < 0 ? ~_value : _value;

    public bool IsFromEnd => _value < 0;

    public static implicit operator Index(int value) => new(value);

    public int GetOffset(int length)
    {
        int offset = IsFromEnd ? length - Value : Value;
        if ((uint)offset > (uint)length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return offset;
    }

    public bool Equals(Index other) => _value == other._value;

    public override bool Equals(object? obj) => obj is Index other && Equals(other);

    public override int GetHashCode() => _value;

    public override string ToString() => IsFromEnd ? "^" + Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool operator ==(Index left, Index right) => left.Equals(right);

    public static bool operator !=(Index left, Index right) => !(left == right);
}

/// <summary>net48 polyfill for <see cref="Range"/>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Range : IEquatable<Range>
{
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    public Index Start { get; }

    public Index End { get; }

    public static Range StartAt(Index start) => new(start, Index.End);

    public static Range EndAt(Index end) => new(Index.Start, end);

    public static Range All => new(Index.Start, Index.End);

    public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

    public override bool Equals(object? obj) => obj is Range other && Equals(other);

    public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();

    public override string ToString() => Start + ".." + End;

    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        int start = Start.GetOffset(length);
        int end = End.GetOffset(length);
        if ((uint)end > (uint)length || end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return (start, end - start);
    }

    public static bool operator ==(Range left, Range right) => left.Equals(right);

    public static bool operator !=(Range left, Range right) => !(left == right);
}
#endif
