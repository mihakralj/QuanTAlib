using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LmsIndicatorTests
{
    [Fact]
    public void LmsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LmsIndicator();

        Assert.Equal(16, indicator.Order);
        Assert.Equal(0.5, indicator.Mu);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LMS - Least Mean Squares Adaptive Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LmsIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LmsIndicator { Order = 16, Mu = 0.5 };

        Assert.Equal(0, LmsIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void LmsIndicator_ShortName_IncludesParameters()
    {
        var indicator = new LmsIndicator { Order = 16, Mu = 0.5 };

        Assert.Contains("LMS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("16", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void LmsIndicator_Initialize_CreatesInternalLms()
    {
        var indicator = new LmsIndicator { Order = 16, Mu = 0.5 };

        indicator.Initialize();

        _ = Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void LmsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LmsIndicator { Order = 4, Mu = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void LmsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LmsIndicator { Order = 4, Mu = 0.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LmsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LmsIndicator { Order = 4, Mu = 0.5 };
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
    public void LmsIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new LmsIndicator { Order = 4, Mu = 0.5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void LmsIndicator_Parameters_CanBeChanged()
    {
        var indicator = new LmsIndicator { Order = 16, Mu = 0.5 };
        Assert.Equal(16, indicator.Order);
        Assert.Equal(0.5, indicator.Mu);

        indicator.Order = 8;
        indicator.Mu = 0.3;
        Assert.Equal(8, indicator.Order);
        Assert.Equal(0.3, indicator.Mu);
    }
}
