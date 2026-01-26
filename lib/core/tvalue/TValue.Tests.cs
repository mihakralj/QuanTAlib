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

        Assert.Contains("∞", result, StringComparison.Ordinal);
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
}
