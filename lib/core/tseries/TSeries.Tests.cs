using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests
{
    public class TSeriesTests
    {
        [Fact]
        public void Add_NewValue_IncreasesCount()
        {
            var series = new TSeries();
            long time = DateTime.UtcNow.Ticks;
            
            series.Add(time, 10.0, isNew: true);
            
            Assert.Single(series);
            Assert.Equal(10.0, series.Last.Value);
        }

        [Fact]
        public void Add_UpdateValue_DoesNotIncreaseCount()
        {
            var series = new TSeries();
            long time = DateTime.UtcNow.Ticks;
            
            series.Add(time, 10.0, isNew: true);
            series.Add(time, 11.0, isNew: false);
            
            Assert.Single(series);
            Assert.Equal(11.0, series.Last.Value);
        }

        [Fact]
        public void Add_MultipleValues_MaintainsOrder()
        {
            var series = new TSeries();
            long t0 = DateTime.UtcNow.Ticks;
            long t1 = t0 + TimeSpan.TicksPerMinute;
            
            series.Add(t0, 10.0, isNew: true);
            series.Add(t1, 20.0, isNew: true);
            
            Assert.Equal(2, series.Count);
            Assert.Equal(10.0, series[0].Value);
            Assert.Equal(20.0, series[1].Value);
        }
    }
}
