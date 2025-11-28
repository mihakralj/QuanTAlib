using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests
{
    public class TBarSeriesTests
    {
        [Fact]
        public void Add_NewBar_IncreasesCount()
        {
            var series = new TBarSeries();
            var bar = new TBar(DateTime.UtcNow.Ticks, 100, 110, 90, 105, 1000);
            
            series.Add(bar, isNew: true);
            
            Assert.Single(series);
            Assert.Equal(105.0, series.Last.Close);
        }

        [Fact]
        public void Add_UpdateBar_DoesNotIncreaseCount()
        {
            var series = new TBarSeries();
            long time = DateTime.UtcNow.Ticks;
            var bar1 = new TBar(time, 100, 110, 90, 105, 1000);
            var bar2 = new TBar(time, 100, 112, 90, 108, 1200);
            
            series.Add(bar1, isNew: true);
            series.Add(bar2, isNew: false);
            
            Assert.Single(series);
            Assert.Equal(108.0, series.Last.Close);
            Assert.Equal(112.0, series.Last.High);
        }

        [Fact]
        public void SubSeries_AreUpdated()
        {
            var series = new TBarSeries();
            var bar = new TBar(DateTime.UtcNow.Ticks, 100, 110, 90, 105, 1000);
            
            series.Add(bar, isNew: true);
            
            Assert.Single(series.Open);
            Assert.Single(series.High);
            Assert.Single(series.Low);
            Assert.Single(series.Close);
            Assert.Single(series.Volume);
            
            Assert.Equal(100.0, series.Open.Last.Value);
            Assert.Equal(110.0, series.High.Last.Value);
            Assert.Equal(90.0, series.Low.Last.Value);
            Assert.Equal(105.0, series.Close.Last.Value);
            Assert.Equal(1000.0, series.Volume.Last.Value);
        }
    }
}
