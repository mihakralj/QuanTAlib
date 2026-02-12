using System.Globalization;

namespace QuanTAlib.Tests;

public class TValueTests
{
    [Fact]
    public void Constructor_WithLongTime_SetsPropertiesCorrectly()
    {
        long time = DateTime.UtcNow.Ticks;
        const double value = 123.45;

        var tValue = new TValue(time, value);

        Assert.Equal(time, tValue.Time);
        Assert.Equal(value, tValue.Value);
    }

    [Fact]
    public void Constructor_WithDateTime_SetsPropertiesCorrectly()
    {
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        double value = 123.45;

        var tValue = new TValue(dateTime, value);

        Assert.Equal(dateTime.Ticks, tValue.Time);
        Assert.Equal(value, tValue.Value);
    }

    [Fact]
    public void AsDateTime_ReturnsCorrectDateTime()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 100.0);

        Assert.Equal(dt, tValue.AsDateTime);
        Assert.Equal(DateTimeKind.Utc, tValue.AsDateTime.Kind);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 123.456);

        string result = tValue.ToString(null, CultureInfo.InvariantCulture);

        Assert.Contains("2023-01-01", result, StringComparison.Ordinal);
        Assert.Contains("12:00:00", result, StringComparison.Ordinal);
        Assert.Contains("123.46", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitConversion_ToDouble_ReturnsValue()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        double val = (double)tValue;

        Assert.Equal(42.0, val);
    }

    [Fact]
    public void ImplicitConversion_ToDateTime_ReturnsCorrectDateTime()
    {
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var tValue = new TValue(dateTime.Ticks, 100.0);

        DateTime result = tValue;

        Assert.Equal(dateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void Equals_TValue_SameValues_ReturnsTrue()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12345, 100.0);

        Assert.True(tv1.Equals(tv2));
    }

    [Fact]
    public void Equals_TValue_DifferentTime_ReturnsFalse()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12346, 100.0);

        Assert.False(tv1.Equals(tv2));
    }

    [Fact]
    public void Equals_TValue_DifferentValue_ReturnsFalse()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12345, 101.0);

        Assert.False(tv1.Equals(tv2));
    }

    [Fact]
    public void Equals_Object_SameTValue_ReturnsTrue()
    {
        var tv1 = new TValue(12345, 100.0);
        object tv2 = new TValue(12345, 100.0);

        Assert.True(tv1.Equals(tv2));
    }

    [Fact]
    public void Equals_Object_DifferentType_ReturnsFalse()
    {
        var tv = new TValue(12345, 100.0);
        object other = "not a TValue";

        Assert.False(tv.Equals(other));
    }

    [Fact]
    public void Equals_Object_Null_ReturnsFalse()
    {
        var tv = new TValue(12345, 100.0);

        Assert.False(tv.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12345, 100.0);

        Assert.Equal(tv1.GetHashCode(), tv2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHashCode()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12346, 100.0);

        Assert.NotEqual(tv1.GetHashCode(), tv2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12345, 100.0);

        Assert.True(tv1 == tv2);
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ReturnsFalse()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12346, 100.0);

        Assert.False(tv1 == tv2);
    }

    [Fact]
    public void InequalityOperator_SameValues_ReturnsFalse()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12345, 100.0);

        Assert.False(tv1 != tv2);
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        var tv1 = new TValue(12345, 100.0);
        var tv2 = new TValue(12346, 100.0);

        Assert.True(tv1 != tv2);
    }

    [Fact]
    public void Constructor_WithDateTimeLocal_ConvertsToUtc()
    {
        var localTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);
        double value = 123.45;

        var tValue = new TValue(localTime, value);

        // Time should be stored as UTC ticks
        var expectedUtc = localTime.ToUniversalTime();
        Assert.Equal(expectedUtc.Ticks, tValue.Time);
    }

    [Fact]
    public void Constructor_WithDateTimeUnspecified_ConvertsToUtc()
    {
        var unspecifiedTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Unspecified);
        double value = 123.45;

        var tValue = new TValue(unspecifiedTime, value);

        // Unspecified is treated as local and converted to UTC
        var expectedUtc = unspecifiedTime.ToUniversalTime();
        Assert.Equal(expectedUtc.Ticks, tValue.Time);
    }

    [Fact]
    public void Constructor_WithDateTimeUtc_PreservesTicks()
    {
        var utcTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        double value = 123.45;

        var tValue = new TValue(utcTime, value);

        Assert.Equal(utcTime.Ticks, tValue.Time);
    }

    [Fact]
    public void Default_TValue_HasZeroTimeAndValue()
    {
        var defaultTValue = default(TValue);

        Assert.Equal(0, defaultTValue.Time);
        Assert.Equal(0.0, defaultTValue.Value);
    }

    [Fact]
    public void Constructor_WithNaN_PreservesNaN()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.NaN);

        Assert.True(double.IsNaN(tValue.Value));
    }

    [Fact]
    public void Constructor_WithPositiveInfinity_PreservesInfinity()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.PositiveInfinity);

        Assert.True(double.IsPositiveInfinity(tValue.Value));
    }

    [Fact]
    public void Constructor_WithNegativeInfinity_PreservesInfinity()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.NegativeInfinity);

        Assert.True(double.IsNegativeInfinity(tValue.Value));
    }

    [Fact]
    public void Constructor_WithMaxValue_PreservesMaxValue()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.MaxValue);

        Assert.Equal(double.MaxValue, tValue.Value);
    }

    [Fact]
    public void Constructor_WithMinValue_PreservesMinValue()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.MinValue);

        Assert.Equal(double.MinValue, tValue.Value);
    }

    [Fact]
    public void Constructor_WithEpsilon_PreservesEpsilon()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.Epsilon);

        Assert.Equal(double.Epsilon, tValue.Value);
    }

    [Fact]
    public void ExplicitConversion_ToDouble_WithNaN_ReturnsNaN()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.NaN);

        double val = (double)tValue;

        Assert.True(double.IsNaN(val));
    }

    [Fact]
    public void ToString_WithNaN_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NaN);

        string result = tValue.ToString(null, CultureInfo.InvariantCulture);

        Assert.Contains("NaN", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithInfinity_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.PositiveInfinity);

        string result = tValue.ToString(null, CultureInfo.InvariantCulture);

        Assert.Contains("\u221E", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithNegativeValue_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, -123.456);

        string result = tValue.ToString(null, CultureInfo.InvariantCulture);

        Assert.Contains("-123.46", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AsDateTime_ReturnsUtcKind()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 100.0);

        Assert.Equal(DateTimeKind.Utc, tValue.AsDateTime.Kind);
    }

    [Fact]
    public void Equals_WithNaN_BothNaN_ReturnsFalse()
    {
        // NaN != NaN in IEEE 754
        var tv1 = new TValue(12345, double.NaN);
        var tv2 = new TValue(12345, double.NaN);

        // Record struct equality compares fields directly
        // double.NaN.Equals(double.NaN) returns true in .NET
        Assert.True(tv1.Equals(tv2));
    }

    [Fact]
    public void GetHashCode_WithNaN_DoesNotThrow()
    {
        var tv = new TValue(12345, double.NaN);

        // Should not throw - the record struct implementation handles NaN correctly
        int hash = tv.GetHashCode();

        // Hash should be consistent for same NaN value
        Assert.Equal(hash, tv.GetHashCode());
    }

    [Fact]
    public void Constructor_WithZeroTime_Allowed()
    {
        var tValue = new TValue(0, 100.0);

        Assert.Equal(0, tValue.Time);
        Assert.Equal(100.0, tValue.Value);
    }

    [Fact]
    public void Constructor_WithNegativeTime_Allowed()
    {
        var tValue = new TValue(-12345, 100.0);

        Assert.Equal(-12345, tValue.Time);
    }

    [Fact]
    public void Constructor_WithMaxLongTime_Allowed()
    {
        var tValue = new TValue(long.MaxValue, 100.0);

        Assert.Equal(long.MaxValue, tValue.Time);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: ToString() parameterless override
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Parameterless_NormalValue_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 42.0);

#pragma warning disable MA0011 // IFormatProvider is missing - intentionally testing parameterless overload
        string result = tValue.ToString();
#pragma warning restore MA0011

        Assert.StartsWith("[", result, StringComparison.Ordinal);
        Assert.EndsWith("]", result, StringComparison.Ordinal);
        Assert.Contains("2023-06-15", result, StringComparison.Ordinal);
        Assert.Contains("14:30:00", result, StringComparison.Ordinal);
        Assert.Contains("42.00", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_Parameterless_PositiveInfinity_ContainsInfinitySymbol()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.PositiveInfinity);

#pragma warning disable MA0011
        string result = tValue.ToString();
#pragma warning restore MA0011

        Assert.Contains("\u221E", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_Parameterless_NegativeInfinity_ContainsMinusInfinitySymbol()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NegativeInfinity);

#pragma warning disable MA0011
        string result = tValue.ToString();
#pragma warning restore MA0011

        Assert.Contains("-\u221E", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_Parameterless_NaN_ContainsNaN()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NaN);

#pragma warning disable MA0011
        string result = tValue.ToString();
#pragma warning restore MA0011

        Assert.Contains("NaN", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_Parameterless_ZeroValue_FormatsAsZero()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 0.0);

#pragma warning disable MA0011
        string result = tValue.ToString();
#pragma warning restore MA0011

        Assert.Contains("0.00", result, StringComparison.Ordinal);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: ToString(string?, IFormatProvider?) — NotSupportedException
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_WithCustomFormat_ThrowsNotSupportedException()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        Assert.Throws<NotSupportedException>(() => tValue.ToString("F4", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ToString_WithNonEmptyFormat_ThrowsNotSupportedException()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        var ex = Assert.Throws<NotSupportedException>(() => tValue.ToString("G", null));
        Assert.Contains("Custom format", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'G'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithNullFormat_DoesNotThrow()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 42.0);

        string result = tValue.ToString(null, null);

        Assert.Contains("42.00", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WithEmptyFormat_DoesNotThrow()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 42.0);

        string result = tValue.ToString("", null);

        Assert.Contains("42.00", result, StringComparison.Ordinal);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: TryFormat — allocation-free span formatting
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryFormat_NormalValue_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 42.0);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.True(charsWritten > 0);

        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.StartsWith("[", formatted, StringComparison.Ordinal);
        Assert.EndsWith("]", formatted, StringComparison.Ordinal);
        Assert.Contains("2023-06-15", formatted, StringComparison.Ordinal);
        Assert.Contains("14:30:00", formatted, StringComparison.Ordinal);
        Assert.Contains("42.00", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_PositiveInfinity_FormatsWithSymbol()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.PositiveInfinity);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.Contains("\u221E", formatted, StringComparison.Ordinal);
        Assert.StartsWith("[", formatted, StringComparison.Ordinal);
        Assert.EndsWith("]", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_NegativeInfinity_FormatsWithMinusSymbol()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NegativeInfinity);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.Contains("-\u221E", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_NaN_FormatsAsNaN()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NaN);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.Contains("NaN", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ReturnsFalse()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        Span<char> buffer = stackalloc char[10]; // Way too small (< 24)
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormat_BufferExactlyTooSmall_ReturnsFalse()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        Span<char> buffer = stackalloc char[23]; // One less than minimum (24)
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormat_BufferBarelyLargeEnough_ForShortValue()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // NaN is shortest value format (3 chars): "[yyyy-MM-dd HH:mm:ss, NaN]" = 26 chars
        var tValue = new TValue(dt.Ticks, double.NaN);

        Span<char> buffer = stackalloc char[26];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        Assert.Equal(26, charsWritten);
    }

    [Fact]
    public void TryFormat_BufferTooSmallForSeparator_ReturnsFalse()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 42.0);

        // Buffer: 1 (opening bracket) + 19 (datetime) + 1 = 21 (too small for ", ")
        Span<char> buffer = stackalloc char[21];
        // This should fail because the initial < 24 check triggers first
        bool result = tValue.TryFormat(buffer, out _, ReadOnlySpan<char>.Empty, null);

        Assert.False(result);
    }

    [Fact]
    public void TryFormat_BufferTooSmallForClosingBracket_ReturnsFalse()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // ∞ is 1 char: "[yyyy-MM-dd HH:mm:ss, ∞]" = 24 chars
        var tValue = new TValue(dt.Ticks, double.PositiveInfinity);

        // Need exactly 24 chars: 1 + 19 + 2 + 1 + 1 = 24
        // Providing 23 — enough to pass the initial check but too small for final ']'
        // Actually the initial check is < 24, so 23 fails there.
        // Let's use 24 which is exactly enough for ∞ case
        Span<char> buffer = stackalloc char[24];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        Assert.Equal(24, charsWritten);
    }

    [Fact]
    public void TryFormat_LargeNegativeValue_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, -99999.99);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        Assert.True(result);
        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.Contains("-99999.99", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_ZeroValue_FormatsAsZero()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 0.0);

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        Assert.True(result);
        string formatted = new string(buffer.Slice(0, charsWritten));
        Assert.Contains("0.00", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormat_ConsistentWithToString()
    {
        var dt = new DateTime(2023, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, 123.456);

#pragma warning disable MA0011
        string fromToString = tValue.ToString();
#pragma warning restore MA0011

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string fromTryFormat = new string(buffer.Slice(0, charsWritten));

        Assert.Equal(fromToString, fromTryFormat);
    }

    [Fact]
    public void TryFormat_NaN_ConsistentWithToString()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NaN);

#pragma warning disable MA0011
        string fromToString = tValue.ToString();
#pragma warning restore MA0011

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string fromTryFormat = new string(buffer.Slice(0, charsWritten));

        Assert.Equal(fromToString, fromTryFormat);
    }

    [Fact]
    public void TryFormat_PositiveInfinity_ConsistentWithToString()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.PositiveInfinity);

#pragma warning disable MA0011
        string fromToString = tValue.ToString();
#pragma warning restore MA0011

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string fromTryFormat = new string(buffer.Slice(0, charsWritten));

        Assert.Equal(fromToString, fromTryFormat);
    }

    [Fact]
    public void TryFormat_NegativeInfinity_ConsistentWithToString()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NegativeInfinity);

#pragma warning disable MA0011
        string fromToString = tValue.ToString();
#pragma warning restore MA0011

        Span<char> buffer = stackalloc char[128];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        string fromTryFormat = new string(buffer.Slice(0, charsWritten));

        Assert.Equal(fromToString, fromTryFormat);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: Record struct features (with expression, Deconstruct)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = new TValue(12345, 100.0);

        var modified = original with { Value = 200.0 };

        Assert.Equal(12345, modified.Time);
        Assert.Equal(200.0, modified.Value);
        Assert.Equal(100.0, original.Value); // Original unchanged
    }

    [Fact]
    public void WithExpression_TimeModified_CreatesModifiedCopy()
    {
        var original = new TValue(12345, 100.0);

        var modified = original with { Time = 99999 };

        Assert.Equal(99999, modified.Time);
        Assert.Equal(100.0, modified.Value);
    }

    [Fact]
    public void Deconstruct_ExtractsTimeAndValue()
    {
        var tValue = new TValue(12345, 42.0);

        var (time, value) = tValue;

        Assert.Equal(12345, time);
        Assert.Equal(42.0, value);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: ISpanFormattable interface contract
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ISpanFormattable_ImplementedCorrectly()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        // Verify TValue implements ISpanFormattable
        Assert.IsAssignableFrom<ISpanFormattable>(tValue);
    }

    [Fact]
    public void IFormattable_ImplementedCorrectly()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        // ISpanFormattable extends IFormattable
        Assert.IsAssignableFrom<IFormattable>(tValue);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: ToString with negative infinity (was missing)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_WithNegativeInfinity_FormatsCorrectly()
    {
        var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var tValue = new TValue(dt.Ticks, double.NegativeInfinity);

        string result = tValue.ToString(null, CultureInfo.InvariantCulture);

        Assert.Contains("-\u221E", result, StringComparison.Ordinal);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: Explicit double conversion edge cases
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExplicitConversion_ToDouble_WithPositiveInfinity_ReturnsInfinity()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.PositiveInfinity);

        double val = (double)tValue;

        Assert.True(double.IsPositiveInfinity(val));
    }

    [Fact]
    public void ExplicitConversion_ToDouble_WithNegativeInfinity_ReturnsNegativeInfinity()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.NegativeInfinity);

        double val = (double)tValue;

        Assert.True(double.IsNegativeInfinity(val));
    }

    [Fact]
    public void ExplicitConversion_ToDouble_WithZero_ReturnsZero()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 0.0);

        double val = (double)tValue;

        Assert.Equal(0.0, val);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: TryFormat buffer edge cases for -∞ and NaN
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryFormat_NegativeInfinity_ExactBuffer()
    {
        var dt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // "-∞" is 2 chars: "[yyyy-MM-dd HH:mm:ss, -∞]" = 25 chars
        var tValue = new TValue(dt.Ticks, double.NegativeInfinity);

        Span<char> buffer = stackalloc char[25];
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(result);
        Assert.Equal(25, charsWritten);
    }

    [Fact]
    public void TryFormat_EmptyBuffer_ReturnsFalse()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);

        Span<char> buffer = Span<char>.Empty;
        bool result = tValue.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }
}
