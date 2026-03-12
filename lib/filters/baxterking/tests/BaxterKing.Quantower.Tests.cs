using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BaxterKingIndicatorTests
{
    [Fact]
    public void BaxterKingIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BaxterKingIndicator();

        Assert.Equal(6, indicator.PLow);
        Assert.Equal(32, indicator.PHigh);
        Assert.Equal(12, indicator.K);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BK - Baxter-King Band-Pass Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BaxterKingIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };

        Assert.Equal(0, BaxterKingIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BaxterKingIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };

        Assert.Contains("BK", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("6", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("32", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("12", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BaxterKingIndicator_Initialize_CreatesInternalBK()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BaxterKingIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void BaxterKingIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BaxterKingIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void BaxterKingIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BaxterKingIndicator_Parameters_CanBeChanged()
    {
        var indicator = new BaxterKingIndicator { PLow = 6, PHigh = 32, K = 12 };
        Assert.Equal(6, indicator.PLow);
        Assert.Equal(32, indicator.PHigh);
        Assert.Equal(12, indicator.K);

        indicator.PLow = 10;
        indicator.PHigh = 50;
        indicator.K = 20;
        Assert.Equal(10, indicator.PLow);
        Assert.Equal(50, indicator.PHigh);
        Assert.Equal(20, indicator.K);
    }
}
