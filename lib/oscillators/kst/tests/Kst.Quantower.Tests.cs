using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class KstIndicatorTests
{
    [Fact]
    public void KstIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KstIndicator();

        Assert.Equal(10, indicator.R1);
        Assert.Equal(15, indicator.R2);
        Assert.Equal(20, indicator.R3);
        Assert.Equal(30, indicator.R4);
        Assert.Equal(10, indicator.S1);
        Assert.Equal(10, indicator.S2);
        Assert.Equal(10, indicator.S3);
        Assert.Equal(15, indicator.S4);
        Assert.Equal(9, indicator.SignalPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("KST - Know Sure Thing Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void KstIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new KstIndicator();

        Assert.Equal(0, KstIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void KstIndicator_ShortName_IncludesParameters()
    {
        var indicator = new KstIndicator { R1 = 10, R2 = 15, R3 = 20, R4 = 30 };
        indicator.Initialize();

        Assert.Contains("KST", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void KstIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new KstIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Kst", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void KstIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new KstIndicator { R1 = 5, R2 = 7, R3 = 9, R4 = 11 };
        indicator.Initialize();

        // KST line + Signal line
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void KstIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KstIndicator { R1 = 3, R2 = 4, R3 = 5, R4 = 6, S1 = 2, S2 = 2, S3 = 2, S4 = 2, SignalPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double kst = indicator.LinesSeries[0].GetValue(0);
        double sig = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(kst));
        Assert.True(double.IsFinite(sig));
    }

    [Fact]
    public void KstIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KstIndicator { R1 = 3, R2 = 4, R3 = 5, R4 = 6, S1 = 2, S2 = 2, S3 = 2, S4 = 2, SignalPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(15), 115, 125, 105, 120);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double kst = indicator.LinesSeries[0].GetValue(0);
        double sig = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(kst));
        Assert.True(double.IsFinite(sig));
    }

    [Fact]
    public void KstIndicator_DifferentSourceTypes_ProcessCorrectly()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new KstIndicator
            {
                R1 = 3, R2 = 4, R3 = 5, R4 = 6,
                S1 = 2, S2 = 2, S3 = 2, S4 = 2,
                SignalPeriod = 2,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 20; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + (i * 0.5), 110 + (i * 0.5), 90 + (i * 0.5), 105 + (i * 0.5));
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
        }
    }
}
