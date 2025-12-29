
namespace QuanTAlib.Tests;

public class TSeriesTests
{
        [Fact]
        public void Constructor_Default_CreatesEmptySeries()
        {
            var series = new TSeries();

            Assert.Empty(series);
        }

        [Fact]
        public void Constructor_WithCapacity_CreatesEmptySeries()
        {
            var series = new TSeries(100);

            Assert.Empty(series);
        }

        [Fact]
        public void Constructor_WithLists_WrapsExistingData()
        {
            var times = new List<long> { 100, 200, 300 };
            var values = new List<double> { 1.0, 2.0, 3.0 };

            var series = new TSeries(times, values);

            Assert.Equal(3, series.Count);
            Assert.Equal(1.0, series[0].Value);
            Assert.Equal(2.0, series[1].Value);
            Assert.Equal(3.0, series[2].Value);
        }

        [Fact]
        public void Name_DefaultValue_IsData()
        {
            var series = new TSeries();

            Assert.Equal("Data", series.Name);
        }

        [Fact]
        public void Name_CanBeSet()
        {
            var series = new TSeries { Name = "TestSeries" };

            Assert.Equal("TestSeries", series.Name);
        }

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

        [Fact]
        public void Add_TValue_AddsNewItem()
        {
            var series = new TSeries();
            var tv = new TValue(DateTime.UtcNow.Ticks, 42.0);

            series.Add(tv);

            Assert.Single(series);
            Assert.Equal(42.0, series.Last.Value);
        }

        [Fact]
        public void Add_TValueWithIsNew_AddsOrUpdates()
        {
            var series = new TSeries();
            var tv1 = new TValue(DateTime.UtcNow.Ticks, 42.0);
            var tv2 = new TValue(DateTime.UtcNow.Ticks, 43.0);

            series.Add(tv1, isNew: true);
            series.Add(tv2, isNew: false);

            Assert.Single(series);
            Assert.Equal(43.0, series.Last.Value);
        }

        [Fact]
        public void Add_WithDateTime_AddsValue()
        {
            var series = new TSeries();
            var dt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

            series.Add(dt, 100.0, isNew: true);

            Assert.Single(series);
            Assert.Equal(dt.Ticks, series.Last.Time);
            Assert.Equal(100.0, series.Last.Value);
        }

        [Fact]
        public void Add_EnumerableDoubles_AddsAllValues()
        {
            var series = new TSeries();
            var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

            series.Add(values);

            Assert.Equal(5, series.Count);
            Assert.Equal(1.0, series[0].Value);
            Assert.Equal(5.0, series[4].Value);
        }

        [Fact]
        public void Add_UpdateOnEmptySeries_AddsNewItem()
        {
            var series = new TSeries();
            var tv = new TValue(DateTime.UtcNow.Ticks, 42.0);

            series.Add(tv, isNew: false);

            Assert.Single(series);
            Assert.Equal(42.0, series.Last.Value);
        }

        [Fact]
        public void Last_EmptySeries_ReturnsDefault()
        {
            var series = new TSeries();

            var last = series.Last;

            Assert.Equal(0, last.Time);
            Assert.Equal(0.0, last.Value);
        }

        [Fact]
        public void Last_NonEmptySeries_ReturnsLastValue()
        {
            var series = new TSeries();
            series.Add(100, 10.0);
            series.Add(200, 20.0);

            var last = series.Last;

            Assert.Equal(200, last.Time);
            Assert.Equal(20.0, last.Value);
        }

        [Fact]
        public void LastValue_EmptySeries_ReturnsNaN()
        {
            var series = new TSeries();

            Assert.True(double.IsNaN(series.LastValue));
        }

        [Fact]
        public void LastValue_NonEmptySeries_ReturnsLastValue()
        {
            var series = new TSeries();
            series.Add(100, 10.0);
            series.Add(200, 20.0);

            Assert.Equal(20.0, series.LastValue);
        }

        [Fact]
        public void LastTime_EmptySeries_ReturnsZero()
        {
            var series = new TSeries();

            Assert.Equal(0, series.LastTime);
        }

        [Fact]
        public void LastTime_NonEmptySeries_ReturnsLastTime()
        {
            var series = new TSeries();
            series.Add(100, 10.0);
            series.Add(200, 20.0);

            Assert.Equal(200, series.LastTime);
        }

        [Fact]
        public void Values_ReturnsReadOnlySpanOfValues()
        {
            var series = new TSeries();
            series.Add(100, 1.0);
            series.Add(200, 2.0);
            series.Add(300, 3.0);

            ReadOnlySpan<double> values = series.Values;

            Assert.Equal(3, values.Length);
            Assert.Equal(1.0, values[0]);
            Assert.Equal(2.0, values[1]);
            Assert.Equal(3.0, values[2]);
        }

        [Fact]
        public void Times_ReturnsReadOnlySpanOfTimes()
        {
            var series = new TSeries();
            series.Add(100, 1.0);
            series.Add(200, 2.0);
            series.Add(300, 3.0);

            ReadOnlySpan<long> times = series.Times;

            Assert.Equal(3, times.Length);
            Assert.Equal(100, times[0]);
            Assert.Equal(200, times[1]);
            Assert.Equal(300, times[2]);
        }

        [Fact]
        public void Indexer_ReturnsCorrectTValue()
        {
            var series = new TSeries();
            series.Add(100, 10.0);
            series.Add(200, 20.0);
            series.Add(300, 30.0);

            Assert.Equal(100, series[0].Time);
            Assert.Equal(10.0, series[0].Value);
            Assert.Equal(200, series[1].Time);
            Assert.Equal(20.0, series[1].Value);
            Assert.Equal(300, series[2].Time);
            Assert.Equal(30.0, series[2].Value);
        }

        [Fact]
        public void GetEnumerator_IteratesAllValues()
        {
            var series = new TSeries();
            series.Add(100, 1.0);
            series.Add(200, 2.0);
            series.Add(300, 3.0);

            var list = series.ToList();

            Assert.Equal(3, list.Count);
            Assert.Equal(1.0, list[0].Value);
            Assert.Equal(2.0, list[1].Value);
            Assert.Equal(3.0, list[2].Value);
        }

        [Fact]
        public void GetEnumerator_NonGeneric_Works()
        {
            var series = new TSeries();
            series.Add(100, 1.0);
            series.Add(200, 2.0);

            var list = new List<object>();
            IEnumerable enumerable = series;
#pragma warning disable S4158
        foreach (var item in enumerable)
            {
                list.Add(item);
            }

            Assert.Equal(2, series.Count);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void Pub_Event_IsRaisedOnAdd()
        {
            var series = new TSeries();
            TValue? received = null;
            series.Pub += (object? sender, in TValueEventArgs args) => received = args.Value;

            series.Add(100, 42.0);

            Assert.NotNull(received);
            Assert.Equal(100, received.Value.Time);
            Assert.Equal(42.0, received.Value.Value);
        }

        [Fact]
        public void Pub_Event_IsRaisedOnUpdate()
        {
            var series = new TSeries();
            TValue? received = null;
            series.Add(100, 42.0);
            series.Pub += (object? sender, in TValueEventArgs args) => received = args.Value;

            series.Add(100, 43.0, isNew: false);

            Assert.NotNull(received);
            Assert.Equal(43.0, received.Value.Value);
        }

        [Fact]
        public void Count_ReturnsCorrectValue()
        {
            var series = new TSeries();

            series.Add(100, 1.0);
            Assert.Single(series);

            series.Add(200, 2.0);
            Assert.Equal(2, series.Count);
    }

    [Fact]
    public void Constructor_WithMismatchedLists_WrapsData()
    {
        // TSeries wraps the lists directly if they're List<T>, no length validation
        var times = new List<long> { 100, 200, 300 };
        var values = new List<double> { 1.0, 2.0 }; // Different length

        var series = new TSeries(times, values);

        // Count is based on values list
        Assert.Equal(2, series.Count);
    }

    [Fact]
    public void Indexer_OutOfBounds_ThrowsException()
    {
        var series = new TSeries();

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[0]);
    }

    [Fact]
    public void Indexer_NegativeIndex_ThrowsException()
    {
        var series = new TSeries();
        series.Add(100, 1.0);

#pragma warning disable DS003 // Invalid index - intentional for testing exception
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[-1]);
#pragma warning restore DS003
    }

    [Fact]
    public void Indexer_BeyondCount_ThrowsException()
    {
        var series = new TSeries();
        series.Add(100, 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[1]);
    }

    [Fact]
    public void Values_EmptySeries_ReturnsEmptySpan()
    {
        var series = new TSeries();

        ReadOnlySpan<double> values = series.Values;

        Assert.Equal(0, values.Length);
    }

    [Fact]
    public void Times_EmptySeries_ReturnsEmptySpan()
    {
        var series = new TSeries();

        ReadOnlySpan<long> times = series.Times;

        Assert.Equal(0, times.Length);
    }

    [Fact]
    public void Add_WithNaN_PreservesNaN()
    {
        var series = new TSeries();

        series.Add(100, double.NaN);

        Assert.True(double.IsNaN(series.Last.Value));
        Assert.True(double.IsNaN(series.LastValue));
    }

    [Fact]
    public void Add_WithInfinity_PreservesInfinity()
    {
        var series = new TSeries();

        series.Add(100, double.PositiveInfinity);

        Assert.True(double.IsPositiveInfinity(series.Last.Value));
        Assert.True(double.IsPositiveInfinity(series.LastValue));
    }

    [Fact]
    public void Add_WithNegativeInfinity_PreservesNegativeInfinity()
    {
        var series = new TSeries();

        series.Add(100, double.NegativeInfinity);

        Assert.True(double.IsNegativeInfinity(series.Last.Value));
    }

    [Fact]
    public void Add_EnumerableDoubles_GeneratesIncreasingTimes()
    {
        var series = new TSeries();
        var values = new[] { 1.0, 2.0, 3.0 };

        series.Add(values);

        Assert.Equal(3, series.Count);
        // Times should be increasing by TicksPerMinute
        Assert.True(series[1].Time > series[0].Time);
        Assert.True(series[2].Time > series[1].Time);
        Assert.Equal(TimeSpan.TicksPerMinute, series[1].Time - series[0].Time);
    }

    [Fact]
    public void Add_EnumerableDoubles_EmptyArray_AddsNothing()
    {
        var series = new TSeries();

        series.Add(Array.Empty<double>());

        Assert.Empty(series);
    }

    [Fact]
    public void Pub_EventArgs_ContainsIsNewFlag()
    {
        var series = new TSeries();
        bool? receivedIsNew = null;
        series.Pub += (object? sender, in TValueEventArgs args) => receivedIsNew = args.IsNew;

        series.Add(new TValue(100, 42.0), isNew: true);

        Assert.True(receivedIsNew);

        series.Add(new TValue(100, 43.0), isNew: false);

        Assert.False(receivedIsNew);
    }

    [Fact]
    public void Constructor_WithCapacity_DoesNotAffectCount()
    {
        var series = new TSeries(1000);

        Assert.Empty(series);
    }

    [Fact]
    public void Add_WithDateTimeLocal_ConvertsToUtc()
    {
        var series = new TSeries();
        var localTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        series.Add(localTime, 100.0);

        // The stored time should be UTC
        var storedTime = new DateTime(series.Last.Time, DateTimeKind.Utc);
        Assert.Equal(DateTimeKind.Utc, storedTime.Kind);
    }

    [Fact]
    public void Add_WithDateTimeUnspecified_TreatsAsLocal()
    {
        var series = new TSeries();
        var unspecifiedTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Unspecified);

        series.Add(unspecifiedTime, 100.0);

        Assert.Single(series);
    }

    [Fact]
    public void Values_ModifyingUnderlyingList_ReflectsInSpan()
    {
        var series = new TSeries();
        series.Add(100, 1.0);
        series.Add(200, 2.0);

        // Get the span
        ReadOnlySpan<double> values1 = series.Values;
        Assert.Equal(2, values1.Length);

        // Add more data
        series.Add(300, 3.0);

        // Get new span - should reflect the change
        ReadOnlySpan<double> values2 = series.Values;
        Assert.Equal(3, values2.Length);
        Assert.Equal(3.0, values2[2]);
    }

    [Fact]
    public void Constructor_WithReadOnlyLists_CopiesData()
    {
        // Using arrays which implement IReadOnlyList but aren't List<T>
        IReadOnlyList<long> times = new long[] { 100, 200, 300 };
        IReadOnlyList<double> values = [1.0, 2.0, 3.0];

        var series = new TSeries(times, values);

        Assert.Equal(3, series.Count);
        Assert.Equal(1.0, series[0].Value);
        Assert.Equal(3.0, series[2].Value);
    }
}
