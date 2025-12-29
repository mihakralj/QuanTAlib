
namespace QuanTAlib.Tests;

public class TBarTests
    {
        [Fact]
        public void Constructor_SetsPropertiesCorrectly()
        {
            long time = DateTime.UtcNow.Ticks;
            double open = 100;
            double high = 110;
            double low = 90;
            double close = 105;
            double volume = 1000;

            var bar = new TBar(time, open, high, low, close, volume);

            Assert.Equal(time, bar.Time);
            Assert.Equal(open, bar.Open);
            Assert.Equal(high, bar.High);
            Assert.Equal(low, bar.Low);
            Assert.Equal(close, bar.Close);
            Assert.Equal(volume, bar.Volume);
        }

        [Fact]
        public void Constructor_WithDateTime_SetsPropertiesCorrectly()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            double open = 100;
            double high = 110;
            double low = 90;
            double close = 105;
            double volume = 1000;

            var bar = new TBar(dateTime, open, high, low, close, volume);

            Assert.Equal(dateTime.Ticks, bar.Time);
            Assert.Equal(open, bar.Open);
            Assert.Equal(high, bar.High);
            Assert.Equal(low, bar.Low);
            Assert.Equal(close, bar.Close);
            Assert.Equal(volume, bar.Volume);
        }

        [Fact]
        public void AsDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var bar = new TBar(dateTime, 100, 110, 90, 105, 1000);

            Assert.Equal(dateTime, bar.AsDateTime);
            Assert.Equal(DateTimeKind.Utc, bar.AsDateTime.Kind);
        }

        [Fact]
        public void O_Property_ReturnsTValueWithOpenPrice()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue o = bar.O;

            Assert.Equal(time, o.Time);
            Assert.Equal(100.0, o.Value);
        }

        [Fact]
        public void H_Property_ReturnsTValueWithHighPrice()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue h = bar.H;

            Assert.Equal(time, h.Time);
            Assert.Equal(110.0, h.Value);
        }

        [Fact]
        public void L_Property_ReturnsTValueWithLowPrice()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue l = bar.L;

            Assert.Equal(time, l.Time);
            Assert.Equal(90.0, l.Value);
        }

        [Fact]
        public void C_Property_ReturnsTValueWithClosePrice()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue c = bar.C;

            Assert.Equal(time, c.Time);
            Assert.Equal(105.0, c.Value);
        }

        [Fact]
        public void V_Property_ReturnsTValueWithVolume()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue v = bar.V;

            Assert.Equal(time, v.Time);
            Assert.Equal(1000.0, v.Value);
        }

        [Fact]
        public void HL2_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 105, 1000);
            Assert.Equal(100.0, bar.HL2);
        }

        [Fact]
        public void OC2_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 104, 1000);
            Assert.Equal(102.0, bar.OC2);
        }

        [Fact]
        public void OHL3_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 105, 1000);
            Assert.Equal(100.0, bar.OHL3);
        }

        [Fact]
        public void HLC3_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.HLC3);
        }

        [Fact]
        public void OHLC4_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.OHLC4);
        }

        [Fact]
        public void HLCC4_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.HLCC4);
        }

        [Fact]
        public void ImplicitConversion_ToDouble_ReturnsClosePrice()
        {
            var bar = new TBar(0, 100, 110, 90, 105, 1000);
            double closePrice = bar;
            Assert.Equal(105.0, closePrice);
        }

        [Fact]
        public void ImplicitConversion_ToTValue_ReturnsClosePriceWithTime()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            TValue tv = bar;

            Assert.Equal(time, tv.Time);
            Assert.Equal(105.0, tv.Value);
        }

        [Fact]
        public void ImplicitConversion_ToDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var bar = new TBar(dateTime, 100, 110, 90, 105, 1000);

            DateTime result = bar;

            Assert.Equal(dateTime, result);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var bar = new TBar(dateTime, 100.5, 110.25, 90.75, 105.0, 1000.0);

            string result = bar.ToString();

            Assert.Contains("2024-06-15", result, StringComparison.Ordinal);
            Assert.Contains("10:30:00", result, StringComparison.Ordinal);
            Assert.Contains("O=100.50", result, StringComparison.Ordinal);
            Assert.Contains("H=110.25", result, StringComparison.Ordinal);
            Assert.Contains("L=90.75", result, StringComparison.Ordinal);
            Assert.Contains("C=105.00", result, StringComparison.Ordinal);
            Assert.Contains("V=1000.00", result, StringComparison.Ordinal);
        }

        [Fact]
        public void Equals_TBar_SameBars_ReturnsTrue()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.True(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentTime_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12346, 100, 110, 90, 105, 1000);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentOpen_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 101, 110, 90, 105, 1000);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentHigh_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 111, 90, 105, 1000);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentLow_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 91, 105, 1000);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentClose_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 106, 1000);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_TBar_DifferentVolume_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 105, 1001);

            Assert.False(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_Object_SameTBar_ReturnsTrue()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            object bar2 = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.True(bar1.Equals(bar2));
        }

        [Fact]
        public void Equals_Object_DifferentType_ReturnsFalse()
        {
            var bar = new TBar(12345, 100, 110, 90, 105, 1000);
            object other = "not a TBar";

            Assert.False(bar.Equals(other));
        }

        [Fact]
        public void Equals_Object_Null_ReturnsFalse()
        {
            var bar = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.False(bar.Equals(null));
        }

        [Fact]
        public void GetHashCode_SameBars_ReturnsSameHashCode()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.Equal(bar1.GetHashCode(), bar2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentBars_ReturnsDifferentHashCode()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12346, 100, 110, 90, 105, 1000);

            Assert.NotEqual(bar1.GetHashCode(), bar2.GetHashCode());
        }

        [Fact]
        public void EqualityOperator_SameBars_ReturnsTrue()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.True(bar1 == bar2);
        }

        [Fact]
        public void EqualityOperator_DifferentBars_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12346, 100, 110, 90, 105, 1000);

            Assert.False(bar1 == bar2);
        }

        [Fact]
        public void InequalityOperator_SameBars_ReturnsFalse()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12345, 100, 110, 90, 105, 1000);

            Assert.False(bar1 != bar2);
        }

        [Fact]
        public void InequalityOperator_DifferentBars_ReturnsTrue()
        {
            var bar1 = new TBar(12345, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(12346, 100, 110, 90, 105, 1000);

            Assert.True(bar1 != bar2);
        }

        // Additional edge case tests
        [Fact]
        public void Constructor_WithLocalDateTime_ConvertsToUtc()
        {
            var localDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);
            var bar = new TBar(localDateTime, 100, 110, 90, 105, 1000);

            // AsDateTime should return UTC
            Assert.Equal(DateTimeKind.Utc, bar.AsDateTime.Kind);
            Assert.Equal(localDateTime.ToUniversalTime().Ticks, bar.Time);
        }

        [Fact]
        public void Constructor_WithUnspecifiedDateTime_ConvertsToUtc()
        {
            var unspecifiedDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Unspecified);
            var bar = new TBar(unspecifiedDateTime, 100, 110, 90, 105, 1000);

            // Should be converted to UTC
            Assert.Equal(DateTimeKind.Utc, bar.AsDateTime.Kind);
        }

        [Fact]
        public void DefaultTBar_HasZeroValues()
        {
            var bar = default(TBar);

            Assert.Equal(0, bar.Time);
            Assert.Equal(0.0, bar.Open);
            Assert.Equal(0.0, bar.High);
            Assert.Equal(0.0, bar.Low);
            Assert.Equal(0.0, bar.Close);
            Assert.Equal(0.0, bar.Volume);
        }

        [Fact]
        public void TBar_WithNaN_HandlesGracefully()
        {
            var bar = new TBar(12345, double.NaN, 110, 90, 105, 1000);

            Assert.True(double.IsNaN(bar.Open));
            Assert.True(double.IsNaN(bar.O.Value));
            Assert.True(double.IsNaN(bar.OHL3)); // Uses Open
            Assert.True(double.IsNaN(bar.OC2));  // Uses Open
            Assert.True(double.IsNaN(bar.OHLC4)); // Uses Open
        }

        [Fact]
        public void TBar_WithInfinity_HandlesGracefully()
        {
            var bar = new TBar(12345, 100, double.PositiveInfinity, 90, 105, 1000);

            Assert.True(double.IsPositiveInfinity(bar.High));
            Assert.True(double.IsPositiveInfinity(bar.H.Value));
            Assert.True(double.IsPositiveInfinity(bar.HL2)); // Uses High
        }

        [Fact]
        public void TBar_WithMaxValue_HandlesGracefully()
        {
            var bar = new TBar(12345, double.MaxValue, double.MaxValue, double.MinValue, 105, 1000);

            Assert.Equal(double.MaxValue, bar.Open);
            Assert.Equal(double.MaxValue, bar.High);
            Assert.Equal(double.MinValue, bar.Low);
            // HL2 calculation with extreme values
            Assert.True(double.IsFinite(bar.HL2) || double.IsInfinity(bar.HL2));
        }

        [Fact]
        public void TBar_WithEpsilon_HandlesGracefully()
        {
            var bar = new TBar(12345, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon);

            Assert.Equal(double.Epsilon, bar.Open);
            Assert.Equal(double.Epsilon, bar.Close);
            Assert.True(bar.HL2 > 0);
        }

        [Fact]
        public void HL2_WithNegativeValues_CalculatesCorrectly()
        {
            var bar = new TBar(0, -100, -90, -110, -95, 1000);

            Assert.Equal(-100.0, bar.HL2); // (-90 + -110) / 2
        }

        [Fact]
        public void OHLC4_WithNegativeValues_CalculatesCorrectly()
        {
            var bar = new TBar(0, -100, -90, -110, -100, 1000);

            Assert.Equal(-100.0, bar.OHLC4); // (-100 + -90 + -110 + -100) / 4
        }

        [Fact]
        public void ImplicitConversion_ToTValue_PreservesTimeAndClose()
        {
            long time = 12_345_678_901_234_567;
            var bar = new TBar(time, 100, 110, 90, 105.5, 1000);

            TValue tv = bar;

            Assert.Equal(time, tv.Time);
            Assert.Equal(105.5, tv.Value);
        }

        [Fact]
        public void ToString_WithNaN_DoesNotThrow()
        {
            var bar = new TBar(DateTime.UtcNow.Ticks, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

            string result = bar.ToString();

            Assert.NotNull(result);
            Assert.Contains("NaN", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void O_H_L_C_V_AllHaveSameTime()
        {
            long time = DateTime.UtcNow.Ticks;
            var bar = new TBar(time, 100, 110, 90, 105, 1000);

            Assert.Equal(time, bar.O.Time);
            Assert.Equal(time, bar.H.Time);
            Assert.Equal(time, bar.L.Time);
            Assert.Equal(time, bar.C.Time);
            Assert.Equal(time, bar.V.Time);
        }

        [Fact]
        public void HLCC4_DoubleWeightsClose()
        {
            // HLCC4 = (High + Low + Close + Close) / 4
            var bar = new TBar(0, 100, 120, 80, 100, 1000);

            // (120 + 80 + 100 + 100) / 4 = 400 / 4 = 100
            Assert.Equal(100.0, bar.HLCC4);
        }

        [Fact]
        public void OHL3_ExcludesClose()
        {
            // OHL3 = (Open + High + Low) / 3
            var bar = new TBar(0, 90, 120, 60, 999, 1000);

            // (90 + 120 + 60) / 3 = 270 / 3 = 90
            Assert.Equal(90.0, bar.OHL3);
        }
}
