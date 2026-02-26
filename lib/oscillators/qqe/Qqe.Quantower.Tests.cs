using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class QqeIndicatorTests
{
    [Fact]
    public void QqeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new QqeIndicator();

        Assert.Equal(14, indicator.RsiPeriod);
        Assert.Equal(5, indicator.SmoothFactor);
        Assert.Equal(4.236, indicator.QqeFactor);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("QQE", indicator.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void QqeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new QqeIndicator();

        Assert.Equal(0, QqeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void QqeIndicator_ShortName_IncludesParameters()
    {
        var indicator = new QqeIndicator { RsiPeriod = 14, SmoothFactor = 5, QqeFactor = 4.236 };
        indicator.Initialize();

        Assert.Contains("QQE", indicator.ShortName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void QqeIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new QqeIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Qqe", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void QqeIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new QqeIndicator();
        indicator.Initialize();

        // QQE and Signal line series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void QqeIndicator_ProcessUpdate_HistoricalBar_ComputesValues()
    {
        var indicator = new QqeIndicator { RsiPeriod = 5, SmoothFactor = 3, QqeFactor = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 60; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double qqeVal = indicator.LinesSeries[0].GetValue(0);
        double sigVal = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(qqeVal));
        Assert.True(double.IsFinite(sigVal));
    }

    [Fact]
    public void QqeIndicator_ProcessUpdate_NewBar_ComputesValues()
    {
        var indicator = new QqeIndicator { RsiPeriod = 5, SmoothFactor = 3, QqeFactor = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new (live) bar
        double newPrice = 121.0;
        indicator.HistoricalData.AddBar(now.AddMinutes(40), newPrice, newPrice + 1, newPrice - 1, newPrice);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double qqeVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(qqeVal));
    }

    [Fact]
    public void QqeIndicator_CustomParameters_Work()
    {
        var indicator = new QqeIndicator
        {
            RsiPeriod = 7,
            SmoothFactor = 3,
            QqeFactor = 2.0
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            double price = 100.0 + (i * 0.4);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double qqeVal = indicator.LinesSeries[0].GetValue(0);
        double sigVal  = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(qqeVal));
        Assert.True(double.IsFinite(sigVal));
    }

    [Fact]
    public void QqeIndicator_DifferentSource_Computes()
    {
        var indicator = new QqeIndicator
        {
            RsiPeriod = 5,
            SmoothFactor = 3,
            QqeFactor = 2.0,
            Source = SourceType.Open
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double qqeVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(qqeVal));
    }

    [Fact]
    public void QqeIndicator_ShowColdValuesFalse_DoesNotCrash()
    {
        var indicator = new QqeIndicator
        {
            RsiPeriod = 14,
            SmoothFactor = 5,
            QqeFactor = 4.236,
            ShowColdValues = false
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Should not throw — cold values suppressed but no crash
        Assert.NotNull(indicator);
    }
}
