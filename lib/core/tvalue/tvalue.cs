using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// A lightweight struct representing a time-value pair.
/// Pure data type: 16 bytes (long + double).
/// Implements ISpanFormattable for allocation-free formatting.
/// </summary>
[SkipLocalsInit]
[StructLayout(LayoutKind.Auto)]
public readonly record struct TValue(long Time, double Value) : ISpanFormattable
{
    public DateTime AsDateTime => new(Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue(DateTime time, double value)
        : this(time.Kind == DateTimeKind.Utc ? time.Ticks : time.ToUniversalTime().Ticks, value)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator double(TValue tv) => tv.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TValue tv) => new(tv.Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        string valueStr = Value switch
        {
            double.PositiveInfinity => ((char)0x221E).ToString(),
            double.NegativeInfinity => "-" + (char)0x221E,
            _ when double.IsNaN(Value) => "NaN",
            _ => Value.ToString("F2"),
        };
        return $"[{AsDateTime:yyyy-MM-dd HH:mm:ss}, {valueStr}]";
    }

    /// <summary>
    /// Formats the TValue using the specified format string.
    /// Note: Custom format and formatProvider are not supported by TValue.
    /// If a non-null/non-empty format is provided, a NotSupportedException is thrown.
    /// </summary>
    /// <param name="format">Must be null or empty; custom formats are not supported.</param>
    /// <param name="formatProvider">Ignored; TValue uses its own fixed format.</param>
    /// <returns>The string representation of this TValue.</returns>
    /// <exception cref="NotSupportedException">Thrown when a non-null/non-empty format is provided.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (!string.IsNullOrEmpty(format))
            throw new NotSupportedException($"Custom format '{format}' is not supported by TValue. Use ToString() for the default format.");

        return ToString();
    }

    /// <summary>
    /// Formats the TValue into the provided span without heap allocation.
    /// Format: "[yyyy-MM-dd HH:mm:ss, value]"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;

        // Early reject for buffers too small to hold even the timestamp portion.
        // This is a heuristic check; actual buffer-overflow protection is performed
        // by the explicit length checks that guard each write operation below.
        if (destination.Length < 24)
            return false;

        // Write opening bracket
        destination[0] = '[';
        int pos = 1;

        // Format datetime: yyyy-MM-dd HH:mm:ss (19 chars)
        if (!AsDateTime.TryFormat(destination.Slice(pos), out int dtChars, "yyyy-MM-dd HH:mm:ss", provider))
            return false;
        pos += dtChars;

        // Write separator
        if (pos + 2 > destination.Length)
            return false;
        destination[pos++] = ',';
        destination[pos++] = ' ';

        // Format value
        if (double.IsPositiveInfinity(Value))
        {
            if (pos + 1 > destination.Length)
                return false;
            destination[pos++] = (char)0x221E; // 
        }
        else if (double.IsNegativeInfinity(Value))
        {
            if (pos + 2 > destination.Length)
                return false;
            destination[pos++] = '-';
            destination[pos++] = (char)0x221E; // -
        }
        else if (double.IsNaN(Value))
        {
            if (pos + 3 > destination.Length)
                return false;
            destination[pos++] = 'N';
            destination[pos++] = 'a';
            destination[pos++] = 'N';
        }
        else
        {
            if (!Value.TryFormat(destination.Slice(pos), out int valueChars, "F2", provider))
                return false;
            pos += valueChars;
        }

        // Write closing bracket
        if (pos + 1 > destination.Length)
            return false;
        destination[pos++] = ']';

        charsWritten = pos;
        return true;
    }
}
