
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
        long[] times = [100, 200, 300];
        double[] opens = [10, 20, 30];
        double[] highs = [15, 25, 35];
        double[] lows = [5, 15, 25];
        double[] closes = [12, 22, 32];
        double[] volumes = [100, 200, 300];

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

#pragma warning disable S4158
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

    [Fact]
    public void Add_WithEnumerables_MismatchedLengths_ThrowsArgumentException()
    {
        var series = new TBarSeries();
        long[] times = [100, 200, 300];
        double[] opens = [10, 20]; // Mismatched length
        double[] highs = [15, 25, 35];
        double[] lows = [5, 15, 25];
        double[] closes = [12, 22, 32];
        double[] volumes = [100, 200, 300];

        Assert.Throws<ArgumentException>(() =>
            series.Add(times, opens, highs, lows, closes, volumes));
    }

    [Fact]
    public void Indexer_OutOfBounds_ThrowsException()
    {
        var series = new TBarSeries();

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[0]);
    }

    [Fact]
    public void Indexer_NegativeIndex_ThrowsException()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        const int invalidIndex = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[invalidIndex]);
    }

    [Fact]
    public void Indexer_BeyondCount_ThrowsException()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[1]);
    }

    [Fact]
    public void Add_WithNaN_PreservesNaN()
    {
        var series = new TBarSeries();
        var bar = new TBar(DateTime.UtcNow.Ticks, double.NaN, 110, 90, 105, 1000);

        series.Add(bar, isNew: true);

        Assert.True(double.IsNaN(series.Last.Open));
        Assert.True(double.IsNaN(series.Open.Last.Value));
    }

    [Fact]
    public void Add_WithInfinity_PreservesInfinity()
    {
        var series = new TBarSeries();
        var bar = new TBar(DateTime.UtcNow.Ticks, 100, double.PositiveInfinity, 90, 105, 1000);

        series.Add(bar, isNew: true);

        Assert.True(double.IsPositiveInfinity(series.Last.High));
        Assert.True(double.IsPositiveInfinity(series.High.Last.Value));
    }

    [Fact]
    public void SubSeries_EmptySeries_HaveZeroCount()
    {
        var series = new TBarSeries();

        Assert.Empty(series.Open);
        Assert.Empty(series.High);
        Assert.Empty(series.Low);
        Assert.Empty(series.Close);
        Assert.Empty(series.Volume);
    }

    [Fact]
    public void SubSeries_ValuesSpan_ReturnsCorrectData()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> closeValues = series.Close.Values;

        Assert.Equal(2, closeValues.Length);
        Assert.Equal(12.0, closeValues[0]);
        Assert.Equal(22.0, closeValues[1]);
    }

    [Fact]
    public void SubSeries_TimesSpan_ReturnsCorrectData()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<long> times = series.Close.Times;

        Assert.Equal(2, times.Length);
        Assert.Equal(100, times[0]);
        Assert.Equal(200, times[1]);
    }

    [Fact]
    public void Pub_EventArgs_ContainsIsNewFlag()
    {
        var series = new TBarSeries();
        bool? receivedIsNew = null;
        series.Pub += (object? sender, in TBarEventArgs args) => receivedIsNew = args.IsNew;

        series.Add(new TBar(100, 10, 15, 5, 12, 100), isNew: true);

        Assert.True(receivedIsNew);

        series.Add(new TBar(100, 10, 18, 5, 15, 150), isNew: false);

        Assert.False(receivedIsNew);
    }

    [Fact]
    public void Add_WithEnumerables_EmptyArrays_AddsNothing()
    {
        var series = new TBarSeries();
        var empty = Array.Empty<long>();
        var emptyD = Array.Empty<double>();

        series.Add(empty, emptyD, emptyD, emptyD, emptyD, emptyD);

        Assert.Empty(series);
    }

    [Fact]
    public void Constructor_WithCapacity_DoesNotAffectCount()
    {
        var series = new TBarSeries(1000);

        Assert.Empty(series);
    }

    [Fact]
    public void GetEnumerator_ExplicitGenericInterface_Works()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);
        series.Add(300, 30, 35, 25, 32, 300);

        // Explicitly call IEnumerable<TBar>.GetEnumerator() through interface cast
        IEnumerable<TBar> genericEnumerable = series;
        using var enumerator = genericEnumerable.GetEnumerator();

        var closes = new List<double>();
        while (enumerator.MoveNext())
        {
            closes.Add(enumerator.Current.Close);
        }

        Assert.Equal(3, closes.Count);
        Assert.Equal(12.0, closes[0]);
        Assert.Equal(22.0, closes[1]);
        Assert.Equal(32.0, closes[2]);
    }

    [Fact]
    public void GetEnumerator_ExplicitNonGenericInterface_Works()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        // Explicitly call IEnumerable.GetEnumerator() through interface cast
        IEnumerable nonGenericEnumerable = series;
        var enumerator = nonGenericEnumerable.GetEnumerator();

        var closes = new List<double>();
        while (enumerator.MoveNext())
        {
            var bar = (TBar)enumerator.Current;
            closes.Add(bar.Close);
        }

        Assert.Equal(2, closes.Count);
        Assert.Equal(12.0, closes[0]);
        Assert.Equal(22.0, closes[1]);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: TryGetLast
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGetLast_EmptySeries_ReturnsFalseAndDefault()
    {
        var series = new TBarSeries();

        bool result = series.TryGetLast(out TBar bar);

        Assert.False(result);
        Assert.Equal(0, bar.Time);
        Assert.Equal(0.0, bar.Close);
    }

    [Fact]
    public void TryGetLast_NonEmptySeries_ReturnsTrueAndLastBar()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        bool result = series.TryGetLast(out TBar bar);

        Assert.True(result);
        Assert.Equal(200, bar.Time);
        Assert.Equal(20.0, bar.Open);
        Assert.Equal(25.0, bar.High);
        Assert.Equal(15.0, bar.Low);
        Assert.Equal(22.0, bar.Close);
        Assert.Equal(200.0, bar.Volume);
    }

    [Fact]
    public void TryGetLast_SingleBar_ReturnsOnlyBar()
    {
        var series = new TBarSeries();
        series.Add(999, 50, 60, 40, 55, 500);

        bool result = series.TryGetLast(out TBar bar);

        Assert.True(result);
        Assert.Equal(999, bar.Time);
        Assert.Equal(55.0, bar.Close);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: Span Accessors (Times, OpenValues, HighValues, etc.)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Times_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);
        series.Add(300, 30, 35, 25, 32, 300);

        ReadOnlySpan<long> times = series.Times;

        Assert.Equal(3, times.Length);
        Assert.Equal(100, times[0]);
        Assert.Equal(200, times[1]);
        Assert.Equal(300, times[2]);
    }

    [Fact]
    public void Times_EmptySeries_ReturnsEmptySpan()
    {
        var series = new TBarSeries();

        ReadOnlySpan<long> times = series.Times;

        Assert.Equal(0, times.Length);
    }

    [Fact]
    public void OpenValues_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> opens = series.OpenValues;

        Assert.Equal(2, opens.Length);
        Assert.Equal(10.0, opens[0]);
        Assert.Equal(20.0, opens[1]);
    }

    [Fact]
    public void HighValues_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> highs = series.HighValues;

        Assert.Equal(2, highs.Length);
        Assert.Equal(15.0, highs[0]);
        Assert.Equal(25.0, highs[1]);
    }

    [Fact]
    public void LowValues_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> lows = series.LowValues;

        Assert.Equal(2, lows.Length);
        Assert.Equal(5.0, lows[0]);
        Assert.Equal(15.0, lows[1]);
    }

    [Fact]
    public void CloseValues_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> closes = series.CloseValues;

        Assert.Equal(2, closes.Length);
        Assert.Equal(12.0, closes[0]);
        Assert.Equal(22.0, closes[1]);
    }

    [Fact]
    public void VolumeValues_ReturnsCorrectSpan()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        ReadOnlySpan<double> vols = series.VolumeValues;

        Assert.Equal(2, vols.Length);
        Assert.Equal(100.0, vols[0]);
        Assert.Equal(200.0, vols[1]);
    }

    [Fact]
    public void SpanAccessors_EmptySeries_AllReturnEmptySpans()
    {
        var series = new TBarSeries();

        Assert.Equal(0, series.OpenValues.Length);
        Assert.Equal(0, series.HighValues.Length);
        Assert.Equal(0, series.LowValues.Length);
        Assert.Equal(0, series.CloseValues.Length);
        Assert.Equal(0, series.VolumeValues.Length);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: AddRange with 6 span parameters
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddRange_WithSpans_AddsAllBars()
    {
        var series = new TBarSeries();
        long[] times = [100, 200, 300];
        double[] opens = [10, 20, 30];
        double[] highs = [15, 25, 35];
        double[] lows = [5, 15, 25];
        double[] closes = [12, 22, 32];
        double[] volumes = [1000, 2000, 3000];

        series.AddRange(times, opens, highs, lows, closes, volumes);

        Assert.Equal(3, series.Count);
        Assert.Equal(100, series[0].Time);
        Assert.Equal(10.0, series[0].Open);
        Assert.Equal(15.0, series[0].High);
        Assert.Equal(5.0, series[0].Low);
        Assert.Equal(12.0, series[0].Close);
        Assert.Equal(1000.0, series[0].Volume);

        Assert.Equal(300, series[2].Time);
        Assert.Equal(32.0, series[2].Close);
    }

    [Fact]
    public void AddRange_WithSpans_EmptySpans_DoesNothing()
    {
        var series = new TBarSeries();
        ReadOnlySpan<long> t = ReadOnlySpan<long>.Empty;
        ReadOnlySpan<double> d = ReadOnlySpan<double>.Empty;

        series.AddRange(t, d, d, d, d, d);

        Assert.Empty(series);
    }

    [Fact]
    public void AddRange_WithSpans_MismatchedLengths_ThrowsArgumentException()
    {
        var series = new TBarSeries();
        long[] times = [100, 200, 300];
        double[] opens = [10, 20]; // Mismatched
        double[] highs = [15, 25, 35];
        double[] lows = [5, 15, 25];
        double[] closes = [12, 22, 32];
        double[] volumes = [1000, 2000, 3000];

        Assert.Throws<ArgumentException>(() =>
            series.AddRange(times, opens, highs, lows, closes, volumes));
    }

    [Fact]
    public void AddRange_WithSpans_DoesNotFirePubEvent()
    {
        var series = new TBarSeries();
        int pubCount = 0;
        series.Pub += (object? sender, in TBarEventArgs args) => pubCount++;

        long[] times = [100, 200, 300];
        double[] opens = [10, 20, 30];
        double[] highs = [15, 25, 35];
        double[] lows = [5, 15, 25];
        double[] closes = [12, 22, 32];
        double[] volumes = [1000, 2000, 3000];

        series.AddRange(times, opens, highs, lows, closes, volumes);

        Assert.Equal(0, pubCount);
        Assert.Equal(3, series.Count);
    }

    [Fact]
    public void AddRange_WithSpans_AppendsToExistingData()
    {
        var series = new TBarSeries();
        series.Add(50, 5, 8, 3, 6, 500);

        long[] times = [100, 200];
        double[] opens = [10, 20];
        double[] highs = [15, 25];
        double[] lows = [5, 15];
        double[] closes = [12, 22];
        double[] volumes = [1000, 2000];

        series.AddRange(times, opens, highs, lows, closes, volumes);

        Assert.Equal(3, series.Count);
        Assert.Equal(50, series[0].Time);
        Assert.Equal(100, series[1].Time);
        Assert.Equal(200, series[2].Time);
    }

    [Fact]
    public void AddRange_WithSpans_SubSeriesReflectData()
    {
        var series = new TBarSeries();
        long[] times = [100, 200];
        double[] opens = [10, 20];
        double[] highs = [15, 25];
        double[] lows = [5, 15];
        double[] closes = [12, 22];
        double[] volumes = [1000, 2000];

        series.AddRange(times, opens, highs, lows, closes, volumes);

        // Sub-series share underlying storage, so they reflect AddRange data
        Assert.Equal(2, series.Open.Count);
        Assert.Equal(10.0, series.Open[0].Value);
        Assert.Equal(20.0, series.Open[1].Value);
        Assert.Equal(22.0, series.Close[1].Value);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: AddRange with ReadOnlySpan<TBar>
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddRange_WithTBarSpan_AddsAllBars()
    {
        var series = new TBarSeries();
        TBar[] bars =
        [
            new TBar(100, 10, 15, 5, 12, 1000),
            new TBar(200, 20, 25, 15, 22, 2000),
            new TBar(300, 30, 35, 25, 32, 3000),
        ];

        series.AddRange(bars);

        Assert.Equal(3, series.Count);
        Assert.Equal(100, series[0].Time);
        Assert.Equal(12.0, series[0].Close);
        Assert.Equal(300, series[2].Time);
        Assert.Equal(32.0, series[2].Close);
        Assert.Equal(3000.0, series[2].Volume);
    }

    [Fact]
    public void AddRange_WithTBarSpan_EmptySpan_DoesNothing()
    {
        var series = new TBarSeries();

        series.AddRange(ReadOnlySpan<TBar>.Empty);

        Assert.Empty(series);
    }

    [Fact]
    public void AddRange_WithTBarSpan_AppendsToExistingData()
    {
        var series = new TBarSeries();
        series.Add(50, 5, 8, 3, 6, 500);

        TBar[] bars =
        [
            new TBar(100, 10, 15, 5, 12, 1000),
            new TBar(200, 20, 25, 15, 22, 2000),
        ];

        series.AddRange(bars);

        Assert.Equal(3, series.Count);
        Assert.Equal(50, series[0].Time);
        Assert.Equal(100, series[1].Time);
        Assert.Equal(200, series[2].Time);
    }

    [Fact]
    public void AddRange_WithTBarSpan_DoesNotFirePubEvent()
    {
        var series = new TBarSeries();
        int pubCount = 0;
        series.Pub += (object? sender, in TBarEventArgs args) => pubCount++;

        TBar[] bars = [new TBar(100, 10, 15, 5, 12, 1000)];

        series.AddRange(bars);

        Assert.Equal(0, pubCount);
        Assert.Single(series);
    }

    [Fact]
    public void AddRange_WithTBarSpan_CorrectlySplitsToSoA()
    {
        var series = new TBarSeries();
        TBar[] bars =
        [
            new TBar(100, 10, 15, 5, 12, 1000),
            new TBar(200, 20, 25, 15, 22, 2000),
        ];

        series.AddRange(bars);

        // Verify SoA layout via span accessors
        Assert.Equal(100, series.Times[0]);
        Assert.Equal(200, series.Times[1]);
        Assert.Equal(10.0, series.OpenValues[0]);
        Assert.Equal(20.0, series.OpenValues[1]);
        Assert.Equal(15.0, series.HighValues[0]);
        Assert.Equal(25.0, series.HighValues[1]);
        Assert.Equal(5.0, series.LowValues[0]);
        Assert.Equal(15.0, series.LowValues[1]);
        Assert.Equal(12.0, series.CloseValues[0]);
        Assert.Equal(22.0, series.CloseValues[1]);
        Assert.Equal(1000.0, series.VolumeValues[0]);
        Assert.Equal(2000.0, series.VolumeValues[1]);
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: TBarEventArgs struct
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TBarEventArgs_Equals_SameValues_ReturnsTrue()
    {
        var bar = new TBar(100, 10, 15, 5, 12, 1000);
        var a = new TBarEventArgs { Value = bar, IsNew = true };
        var b = new TBarEventArgs { Value = bar, IsNew = true };

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void TBarEventArgs_Equals_DifferentIsNew_ReturnsFalse()
    {
        var bar = new TBar(100, 10, 15, 5, 12, 1000);
        var a = new TBarEventArgs { Value = bar, IsNew = true };
        var b = new TBarEventArgs { Value = bar, IsNew = false };

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void TBarEventArgs_Equals_DifferentValue_ReturnsFalse()
    {
        var bar1 = new TBar(100, 10, 15, 5, 12, 1000);
        var bar2 = new TBar(200, 20, 25, 15, 22, 2000);
        var a = new TBarEventArgs { Value = bar1, IsNew = true };
        var b = new TBarEventArgs { Value = bar2, IsNew = true };

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void TBarEventArgs_Equals_Object_SameValues_ReturnsTrue()
    {
        var bar = new TBar(100, 10, 15, 5, 12, 1000);
        var a = new TBarEventArgs { Value = bar, IsNew = true };
        object b = new TBarEventArgs { Value = bar, IsNew = true };

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void TBarEventArgs_Equals_Object_DifferentType_ReturnsFalse()
    {
        var bar = new TBar(100, 10, 15, 5, 12, 1000);
        var a = new TBarEventArgs { Value = bar, IsNew = true };

        Assert.False(a.Equals("not a TBarEventArgs"));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void TBarEventArgs_GetHashCode_EqualObjects_SameHash()
    {
        var bar = new TBar(100, 10, 15, 5, 12, 1000);
        var a = new TBarEventArgs { Value = bar, IsNew = true };
        var b = new TBarEventArgs { Value = bar, IsNew = true };

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TBarEventArgs_GetHashCode_DifferentObjects_LikelyDifferentHash()
    {
        var bar1 = new TBar(100, 10, 15, 5, 12, 1000);
        var bar2 = new TBar(200, 20, 25, 15, 22, 2000);
        var a = new TBarEventArgs { Value = bar1, IsNew = true };
        var b = new TBarEventArgs { Value = bar2, IsNew = false };

        // Not guaranteed but extremely likely for distinct values
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: TBarSeriesEnumerator struct
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TBarSeriesEnumerator_Reset_AllowsReIteration()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        var enumerator = series.GetEnumerator();

        // First pass
        int count1 = 0;
        while (enumerator.MoveNext()) { count1++; }
        Assert.Equal(2, count1);

        // Reset and re-iterate
        enumerator.Reset();
        int count2 = 0;
        while (enumerator.MoveNext()) { count2++; }
        Assert.Equal(2, count2);
    }

    [Fact]
    public void TBarSeriesEnumerator_Dispose_DoesNotThrow()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        var enumerator = series.GetEnumerator();
        enumerator.MoveNext();
        enumerator.Dispose(); // Should be no-op

        Assert.Equal(12.0, enumerator.Current.Close);
    }

    [Fact]
    public void TBarSeriesEnumerator_Equals_SameState_ReturnsTrue()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        var a = series.GetEnumerator();
        var b = series.GetEnumerator();

        // Both at initial state (_index = -1)
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void TBarSeriesEnumerator_Equals_DifferentState_ReturnsFalse()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        var a = series.GetEnumerator();
        var b = series.GetEnumerator();

        a.MoveNext(); // a is at index 0, b is at -1

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void TBarSeriesEnumerator_Equals_Object_SameState_ReturnsTrue()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        var a = series.GetEnumerator();
        object b = series.GetEnumerator();

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void TBarSeriesEnumerator_Equals_Object_DifferentType_ReturnsFalse()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        var a = series.GetEnumerator();

        Assert.False(a.Equals("not an enumerator"));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void TBarSeriesEnumerator_GetHashCode_SameState_SameHash()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        var a = series.GetEnumerator();
        var b = series.GetEnumerator();

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TBarSeriesEnumerator_GetHashCode_DifferentState_LikelyDifferentHash()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);
        series.Add(200, 20, 25, 15, 22, 200);

        var a = series.GetEnumerator();
        var b = series.GetEnumerator();
        a.MoveNext();

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TBarSeriesEnumerator_Current_ViaIEnumerator_ReturnsBoxedTBar()
    {
        var series = new TBarSeries();
        series.Add(100, 10, 15, 5, 12, 100);

        IEnumerator enumerator = series.GetEnumerator();
        enumerator.MoveNext();

        object current = enumerator.Current;
        Assert.IsType<TBar>(current);
        Assert.Equal(12.0, ((TBar)current).Close);
    }

    [Fact]
    public void TBarSeriesEnumerator_DifferentSeries_NotEqual()
    {
        var series1 = new TBarSeries();
        series1.Add(100, 10, 15, 5, 12, 100);

        var series2 = new TBarSeries();
        series2.Add(100, 10, 15, 5, 12, 100);

        var a = series1.GetEnumerator();
        var b = series2.GetEnumerator();

        // Different underlying list references -> not equal
        Assert.False(a.Equals(b));
    }

    // ────────────────────────────────────────────────────────────────────
    // NEW TESTS: AddRange large data set (capacity pre-alloc path)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddRange_LargeDataSet_AllBarsAccessible()
    {
        var series = new TBarSeries();
        const int N = 1000;
        var times = new long[N];
        var opens = new double[N];
        var highs = new double[N];
        var lows = new double[N];
        var closes = new double[N];
        var volumes = new double[N];

        for (int i = 0; i < N; i++)
        {
            times[i] = i;
            opens[i] = i * 10.0;
            highs[i] = (i * 10.0) + 5.0;
            lows[i] = (i * 10.0) - 5.0;
            closes[i] = (i * 10.0) + 2.0;
            volumes[i] = i * 100.0;
        }

        series.AddRange(times, opens, highs, lows, closes, volumes);

        Assert.Equal(N, series.Count);
        Assert.Equal(0, series[0].Time);
        Assert.Equal(((N - 1) * 10.0) + 2.0, series[N - 1].Close);
        Assert.Equal((N - 1) * 100.0, series[N - 1].Volume);
    }

    [Fact]
    public void AddRange_TBarSpan_LargeDataSet_AllBarsAccessible()
    {
        var series = new TBarSeries();
        const int N = 500;
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = new TBar(i, i * 10.0, (i * 10.0) + 5.0, (i * 10.0) - 5.0, (i * 10.0) + 2.0, i * 100.0);
        }

        series.AddRange(bars);

        Assert.Equal(N, series.Count);
        Assert.Equal(0, series[0].Time);
        Assert.Equal(2.0, series[0].Close);
        Assert.Equal(((N - 1) * 10.0) + 2.0, series[N - 1].Close);
    }
}
