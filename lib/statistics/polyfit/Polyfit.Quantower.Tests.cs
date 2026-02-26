using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PolyfitIndicatorTests
{
    // ── 1. Constructor defaults ───────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultValues()
    {
        var ind = new PolyfitIndicator();
        Assert.Equal(20, ind.Period);
        Assert.Equal(2, ind.Degree);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Polyfit - Polynomial Fitting", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
        Assert.Equal(SourceType.Close, ind.Source);
    }

    [Fact]
    public void Constructor_ShortName_IncludesPeriodDegree()
    {
        var ind = new PolyfitIndicator { Period = 10, Degree = 3 };
        Assert.Equal("Polyfit 10,3", ind.ShortName);
    }

    // ── 2. MinHistoryDepths ───────────────────────────────────────────────────

    [Fact]
    public void MinHistoryDepths_IsZero()
    {
        Assert.Equal(0, PolyfitIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MinHistoryDepths_InterfaceImplementation()
    {
        IWatchlistIndicator ind = new PolyfitIndicator();
        Assert.Equal(0, ind.MinHistoryDepths);
    }

    // ── 3. Initialize creates internal indicator and line series ──────────────

    [Fact]
    public void Initialize_CreatesLineSeries()
    {
        var ind = new PolyfitIndicator { Period = 10 };
        ind.Initialize();

        Assert.Single(ind.LinesSeries);
        Assert.Equal("Polyfit", ind.LinesSeries[0].Name);
    }

    [Fact]
    public void Initialize_CustomPeriodDegree()
    {
        var ind = new PolyfitIndicator { Period = 8, Degree = 3 };
        ind.Initialize();
        Assert.Equal("Polyfit 8,3", ind.ShortName);
    }

    // ── 4. ProcessUpdate — historical data ────────────────────────────────────

    [Fact]
    public void ProcessUpdate_HistoricalBars_ProducesFiniteValues()
    {
        var ind = new PolyfitIndicator { Period = 5, Degree = 2 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            ind.ProcessUpdate(args);
        }

        double val = ind.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void ProcessUpdate_NewBar_UpdatesValue()
    {
        var ind = new PolyfitIndicator { Period = 5, Degree = 2 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Fill warmup with historical bars
        for (int i = 0; i < 5; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = ind.LinesSeries[0].GetValue(0);

        // Add one more new bar
        ind.HistoricalData.AddBar(now.AddMinutes(5), 110, 120, 100, 115);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double val2 = ind.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }

    [Fact]
    public void ProcessUpdate_SameBarUpdate_ProducesFiniteValue()
    {
        var ind = new PolyfitIndicator { Period = 5, Degree = 2 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Non-new bar update (bar correction)
        ind.HistoricalData.AddBar(now.AddMinutes(4), 108, 118, 98, 112);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double val = ind.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(val));
    }

    // ── 5. Different source types ─────────────────────────────────────────────

    [Theory]
    [InlineData(SourceType.Close)]
    [InlineData(SourceType.Open)]
    [InlineData(SourceType.High)]
    [InlineData(SourceType.Low)]
    [InlineData(SourceType.HL2)]
    public void DifferentSourceTypes_ProducesFiniteValues(SourceType sourceType)
    {
        var ind = new PolyfitIndicator { Period = 5, Degree = 2, Source = sourceType };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = ind.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    // ── 6. Different degree variants ─────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DifferentDegrees_ProducesFiniteValues(int degree)
    {
        var ind = new PolyfitIndicator { Period = 10, Degree = degree };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = ind.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val > 0, "Expected positive overlay value");
    }

    // ── 7. SeparateWindow and SourceCodeLink ──────────────────────────────────

    [Fact]
    public void SeparateWindow_IsFalse_Overlay()
    {
        var ind = new PolyfitIndicator();
        Assert.False(ind.SeparateWindow);
    }

    [Fact]
    public void SourceCodeLink_ContainsPolyfit()
    {
        var ind = new PolyfitIndicator();
        Assert.Contains("Polyfit", ind.SourceCodeLink, StringComparison.Ordinal);
    }
}
