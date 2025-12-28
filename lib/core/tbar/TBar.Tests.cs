
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
}
