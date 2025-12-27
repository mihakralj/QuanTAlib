
namespace QuanTAlib.Tests;

public class TBarSeriesTests
{
    [Fact]
        public void Constructor_Default_CreatesEmptySeries()
        {
            var series = new TBarSeries();

            Assert.Empty(series);
        }

        [Fact]
        public void Constructor_WithCapacity_CreatesEmptySeries()
        {
            var series = new TBarSeries(100);

            Assert.Empty(series);
        }

        [Fact]
        public void Name_DefaultValue_IsBar()
        {
            var series = new TBarSeries();

            Assert.Equal("Bar", series.Name);
        }

        [Fact]
        public void Name_CanBeSet()
        {
            var series = new TBarSeries { Name = "TestBars" };

            Assert.Equal("TestBars", series.Name);
        }

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
        public void Add_UpdateOnEmptySeries_AddsNewBar()
        {
            var series = new TBarSeries();
            var bar = new TBar(DateTime.UtcNow.Ticks, 100, 110, 90, 105, 1000);
            
            series.Add(bar, isNew: false);
            
            Assert.Single(series);
        }

        [Fact]
        public void Add_WithLongTime_AddsBar()
        {
            var series = new TBarSeries();
            long time = DateTime.UtcNow.Ticks;
            
            series.Add(time, 100, 110, 90, 105, 1000, isNew: true);
            
            Assert.Single(series);
            Assert.Equal(time, series.Last.Time);
        }

        [Fact]
        public void Add_WithDateTime_AddsBar()
        {
            var series = new TBarSeries();
            var dt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            
            series.Add(dt, 100, 110, 90, 105, 1000, isNew: true);
            
            Assert.Single(series);
            Assert.Equal(dt.Ticks, series.Last.Time);
        }

        [Fact]
        public void Add_WithEnumerables_AddsMultipleBars()
        {
            var series = new TBarSeries();
            var times = new long[] { 100, 200, 300 };
            var opens = new double[] { 10, 20, 30 };
            var highs = new double[] { 15, 25, 35 };
            var lows = new double[] { 5, 15, 25 };
            var closes = new double[] { 12, 22, 32 };
            var volumes = new double[] { 100, 200, 300 };
            
            series.Add(times, opens, highs, lows, closes, volumes);
            
            Assert.Equal(3, series.Count);
            Assert.Equal(10, series[0].Open);
            Assert.Equal(32, series[2].Close);
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

        [Fact]
        public void SubSeries_Aliases_Work()
        {
            var series = new TBarSeries();
            var bar = new TBar(DateTime.UtcNow.Ticks, 100, 110, 90, 105, 1000);
            series.Add(bar, isNew: true);
            
            Assert.Same(series.Open, series.O);
            Assert.Same(series.High, series.H);
            Assert.Same(series.Low, series.L);
            Assert.Same(series.Close, series.C);
            Assert.Same(series.Volume, series.V);
        }

        [Fact]
        public void SubSeries_HaveCorrectNames()
        {
            var series = new TBarSeries();
            
            Assert.Equal("Open", series.Open.Name);
            Assert.Equal("High", series.High.Name);
            Assert.Equal("Low", series.Low.Name);
            Assert.Equal("Close", series.Close.Name);
            Assert.Equal("Volume", series.Volume.Name);
        }

        [Fact]
        public void Last_EmptySeries_ReturnsDefault()
        {
            var series = new TBarSeries();
            
            var last = series.Last;
            
            Assert.Equal(0, last.Time);
            Assert.Equal(0.0, last.Open);
            Assert.Equal(0.0, last.Close);
        }

        [Fact]
        public void Last_NonEmptySeries_ReturnsLastBar()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            var last = series.Last;
            
            Assert.Equal(200, last.Time);
            Assert.Equal(22.0, last.Close);
        }

        [Fact]
        public void LastTime_EmptySeries_ReturnsZero()
        {
            var series = new TBarSeries();
            Assert.Equal(0, series.LastTime);
        }

        [Fact]
        public void LastTime_NonEmptySeries_ReturnsLastTime()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(200, series.LastTime);
        }

        [Fact]
        public void LastOpen_EmptySeries_ReturnsNaN()
        {
            var series = new TBarSeries();
            Assert.True(double.IsNaN(series.LastOpen));
        }

        [Fact]
        public void LastOpen_NonEmptySeries_ReturnsLastOpen()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(20.0, series.LastOpen);
        }

        [Fact]
        public void LastHigh_EmptySeries_ReturnsNaN()
        {
            var series = new TBarSeries();
            Assert.True(double.IsNaN(series.LastHigh));
        }

        [Fact]
        public void LastHigh_NonEmptySeries_ReturnsLastHigh()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(25.0, series.LastHigh);
        }

        [Fact]
        public void LastLow_EmptySeries_ReturnsNaN()
        {
            var series = new TBarSeries();
            Assert.True(double.IsNaN(series.LastLow));
        }

        [Fact]
        public void LastLow_NonEmptySeries_ReturnsLastLow()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(15.0, series.LastLow);
        }

        [Fact]
        public void LastClose_EmptySeries_ReturnsNaN()
        {
            var series = new TBarSeries();
            Assert.True(double.IsNaN(series.LastClose));
        }

        [Fact]
        public void LastClose_NonEmptySeries_ReturnsLastClose()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(22.0, series.LastClose);
        }

        [Fact]
        public void LastVolume_EmptySeries_ReturnsNaN()
        {
            var series = new TBarSeries();
            Assert.True(double.IsNaN(series.LastVolume));
        }

        [Fact]
        public void LastVolume_NonEmptySeries_ReturnsLastVolume()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(200.0, series.LastVolume);
        }

        [Fact]
        public void Indexer_ReturnsCorrectBar()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            series.Add(300, 30, 35, 25, 32, 300);
            
            Assert.Equal(100, series[0].Time);
            Assert.Equal(10.0, series[0].Open);
            Assert.Equal(200, series[1].Time);
            Assert.Equal(22.0, series[1].Close);
            Assert.Equal(300, series[2].Time);
            Assert.Equal(32.0, series[2].Close);
        }

        [Fact]
        public void Count_ReturnsCorrectValue()
        {
            var series = new TBarSeries();
            
            Assert.Empty(series);
            
            series.Add(100, 10, 15, 5, 12, 100);
            Assert.Single(series);
            
            series.Add(200, 20, 25, 15, 22, 200);
            Assert.Equal(2, series.Count);
        }

        [Fact]
        public void GetEnumerator_IteratesAllBars()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            series.Add(300, 30, 35, 25, 32, 300);
            
            var list = series.ToList();
            
            Assert.Equal(3, list.Count);
            Assert.Equal(10.0, list[0].Open);
            Assert.Equal(22.0, list[1].Close);
            Assert.Equal(32.0, list[2].Close);
        }

        [Fact]
        public void GetEnumerator_NonGeneric_Works()
        {
            var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100, isNew: true);
        series.Add(200, 20, 25, 15, 22, 200, isNew: true);

        var list = new List<object>();

        foreach (var item in (IEnumerable)series)
        {
                list.Add(item);
            }
            
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void Pub_Event_IsRaisedOnAdd()
        {
            var series = new TBarSeries();
            TBar? received = null;
            series.Pub += (object? sender, in TBarEventArgs args) => received = args.Value;

            var barToAdd = new TBar(100, 10, 15, 5, 12, 100);
            series.Add(barToAdd, isNew: true);
            
            Assert.NotNull(received);
            Assert.Equal(100, received.Value.Time);
            Assert.Equal(12.0, received.Value.Close);
        }

        [Fact]
        public void Pub_Event_IsRaisedOnUpdate()
        {
            var series = new TBarSeries();
            TBar? received = null;
            series.Add(100, 10, 15, 5, 12, 100);
            series.Pub += (object? sender, in TBarEventArgs args) => received = args.Value;

            series.Add(100, 10, 18, 5, 15, 150, isNew: false);
            
            Assert.NotNull(received);
            Assert.Equal(15.0, received.Value.Close);
            Assert.Equal(18.0, received.Value.High);
        }

        [Fact]
        public void SubSeries_ShareSameTimeArray()
        {
            var series = new TBarSeries();
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            
            Assert.Equal(series.Open.Times[0], series.Close.Times[0]);
            Assert.Equal(series.High.Times[1], series.Volume.Times[1]);
        }

        [Fact]
        public void Add_MultipleBars_MaintainsOrder()
        {
            var series = new TBarSeries();
            
            series.Add(100, 10, 15, 5, 12, 100);
            series.Add(200, 20, 25, 15, 22, 200);
            series.Add(300, 30, 35, 25, 32, 300);
            
            Assert.Equal(3, series.Count);
            Assert.Equal(100, series[0].Time);
            Assert.Equal(200, series[1].Time);
        Assert.Equal(300, series[2].Time);
    }
}
