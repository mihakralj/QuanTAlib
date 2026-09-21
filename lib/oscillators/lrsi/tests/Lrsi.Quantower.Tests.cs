using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class LrsiIndicatorTests
{
    [Fact]
    public void LrsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LrsiIndicator();

        Assert.Equal(0.5, indicator.Gamma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LRSI - Laguerre RSI", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LrsiIndicator_MinHistoryDepths_EqualsFour()
    {
        var indicator = new LrsiIndicator();

        Assert.Equal(4, LrsiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(4, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void LrsiIndicator_ShortName_IncludesGamma()
    {
        var indicator = new LrsiIndicator { Gamma = 0.75 };
        indicator.Initialize();

        Assert.Contains("LRSI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.75", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void LrsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new LrsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Lrsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void LrsiIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new LrsiIndicator { Gamma = 0.5 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void LrsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LrsiIndicator { Gamma = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (Math.Sin(i * 0.3) * 10.0) + (i * 0.1);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price + 5, price + 10, price - 5, price);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.True(value >= 0.0 && value <= 1.0, $"LRSI={value} out of [0,1]");
    }

    [Fact]
    public void LrsiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LrsiIndicator { Gamma = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price + 3, price + 6, price - 3, price);

            var reason = i < 19 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            var args = new UpdateArgs(reason);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.True(value >= 0.0 && value <= 1.0, $"LRSI={value} out of [0,1]");
    }

    [Fact]
    public void LrsiIndicator_DifferentSourceTypes_ComputeWithoutError()
    {
        foreach (var sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new LrsiIndicator
            {
                Gamma = 0.5,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 20; i++)
            {
                double price = 100.0 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price + 1);

                var args = new UpdateArgs(UpdateReason.HistoricalBar);
                indicator.ProcessUpdate(args);
            }

            double value = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value), $"SourceType {sourceType}: value={value}");
            Assert.True(value >= 0.0 && value <= 1.0, $"SourceType {sourceType}: LRSI={value} out of [0,1]");
        }
    }

    [Fact]
    public void LrsiIndicator_OutputInRange_ExtendedSeries()
    {
        var indicator = new LrsiIndicator { Gamma = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Feed a volatile sine wave to exercise full range
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (Math.Sin(i * 0.2) * 20.0);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price + 5, price + 10, price - 5, price);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);

            double v = indicator.LinesSeries[0].GetValue(0);
            if (double.IsFinite(v))
            {
                Assert.True(v >= 0.0 && v <= 1.0, $"Bar {i}: LRSI={v} out of [0,1]");
            }
        }
    }
}
