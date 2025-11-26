using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests
{
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
        public void HL2_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 105, 1000);
            Assert.Equal(100.0, bar.HL2); // (110 + 90) / 2
        }

        [Fact]
        public void OHL3_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 105, 1000);
            Assert.Equal(100.0, bar.OHL3); // (100 + 110 + 90) / 3
        }

        [Fact]
        public void HLC3_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.HLC3); // (110 + 90 + 100) / 3
        }

        [Fact]
        public void OHLC4_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.OHLC4); // (100 + 110 + 90 + 100) / 4
        }

        [Fact]
        public void HLCC4_CalculatesCorrectly()
        {
            var bar = new TBar(0, 100, 110, 90, 100, 1000);
            Assert.Equal(100.0, bar.HLCC4); // (110 + 90 + 100 + 100) / 4
        }
    }
}
