using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests
{
    public class TValueTests
    {
        [Fact]
        public void Constructor_SetsPropertiesCorrectly()
        {
            long time = DateTime.UtcNow.Ticks;
            double value = 123.45;

            var tValue = new TValue(time, value);

            Assert.Equal(time, tValue.Time);
            Assert.Equal(value, tValue.Value);
        }

        [Fact]
        public void AsDateTime_ReturnsCorrectDateTime()
        {
            DateTime dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            long ticks = dt.Ticks;
            var tValue = new TValue(ticks, 100.0);

            Assert.Equal(dt, tValue.AsDateTime);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            DateTime dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var tValue = new TValue(dt.Ticks, 123.456);

            string result = tValue.ToString();
            
            Assert.Contains(dt.ToString("yyyy-MM-dd HH:mm:ss"), result);
            Assert.Contains("123.46", result); // Default formatting usually 2 decimals or similar
        }
        
        [Fact]
        public void ImplicitConversion_ToDouble()
        {
            var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);
            double val = tValue;
            Assert.Equal(42.0, val);
        }
    }
}
