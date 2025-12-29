using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BetaIndicatorTests
{
    [Fact]
    public void BetaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BetaIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.AssetSource);
        Assert.Equal(SourceType.Close, indicator.MarketSource);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Beta Coefficient", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BetaIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new BetaIndicator { Period = 20 };

        Assert.Equal(2, BetaIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(2, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BetaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BetaIndicator { Period = 14 };

        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Beta", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BetaIndicator_Initialize_CreatesInternalBeta()
    {
        var indicator = new BetaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Beta", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void BetaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BetaIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data - need enough bars for warmup
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double beta = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(beta));
    }

    [Fact]
    public void BetaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BetaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        
        // Add initial bars
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Add a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(11, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void BetaIndicator_DifferentSourceTypes_Work()
    {
        var assetSources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
        };

        foreach (var source in assetSources)
        {
            var indicator = new BetaIndicator { Period = 5, AssetSource = source, MarketSource = SourceType.Close };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"AssetSource {source} should produce finite value");
        }
    }
}
