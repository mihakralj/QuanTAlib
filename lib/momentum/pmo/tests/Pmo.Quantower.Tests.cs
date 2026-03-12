using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PmoIndicatorTests
{
    [Fact]
    public void PmoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PmoIndicator();

        Assert.Equal("PMO - Price Momentum Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(35, indicator.RocPeriod);
        Assert.Equal(20, indicator.Smooth1Period);
        Assert.Equal(10, indicator.Smooth2Period);
    }

    [Fact]
    public void PmoIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new PmoIndicator();

        Assert.Equal(0, PmoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PmoIndicator_ShortName_IncludesPeriods()
    {
        var indicator = new PmoIndicator();
        indicator.Initialize();

        Assert.Equal("PMO(35,20,10):Close", indicator.ShortName);
    }

    [Fact]
    public void PmoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PmoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pmo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PmoIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new PmoIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("PMO", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void PmoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PmoIndicator
        {
            RocPeriod = 5,
            Smooth1Period = 3,
            Smooth2Period = 3,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100 + i);
        }

        var args = new UpdateArgs(UpdateReason.HistoricalBar);

        for (int i = 0; i < 20; i++)
        {
            indicator.ProcessUpdate(args);
        }

        double pmo = indicator.LinesSeries[0].GetValue(0);
        Assert.False(double.IsNaN(pmo));
    }

    [Fact]
    public void PmoIndicator_MultipleUpdates_ProducesFiniteSequence()
    {
        var indicator = new PmoIndicator
        {
            RocPeriod = 3,
            Smooth1Period = 3,
            Smooth2Period = 3,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(30, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 30; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.Equal(0, indicator.LinesSeries[1].GetValue(i));
        }
    }

    [Fact]
    public void PmoIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new PmoIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void PmoIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new PmoIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }
}
