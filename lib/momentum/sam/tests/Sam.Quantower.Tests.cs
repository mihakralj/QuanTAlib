using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SamIndicatorTests
{
    [Fact]
    public void SamIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SamIndicator();

        Assert.Equal(0.07, indicator.Alpha);
        Assert.Equal(8, indicator.Cutoff);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SAM - Smoothed Adaptive Momentum", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void SamIndicator_MinHistoryDepths_Is100()
    {
        var indicator = new SamIndicator();
        Assert.Equal(100, indicator.MinHistoryDepths);
    }

    [Fact]
    public void SamIndicator_ShortName_IncludesParams()
    {
        var indicator = new SamIndicator { Alpha = 0.1, Cutoff = 12 };
        Assert.Equal("SAM(0.1,12)", indicator.ShortName);
    }

    [Fact]
    public void SamIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("SAM", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void SamIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void SamIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SamIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SamIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.Equal(0, indicator.LinesSeries[1].GetValue(i));
        }
    }

    [Fact]
    public void SamIndicator_DifferentSourceTypes_Work()
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
            var indicator = new SamIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void SamIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new SamIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SamIndicator_FlatPrices_ProducesZeroSam()
    {
        var indicator = new SamIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Feed enough flat bars to pass warmup (100+)
        for (int i = 0; i < 150; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastSam = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastSam, 5);
    }

    [Fact]
    public void SamIndicator_DifferentAlphas_Work()
    {
        var alphas = new[] { 0.01, 0.07, 0.2, 0.5, 1.0 };

        foreach (var alpha in alphas)
        {
            var indicator = new SamIndicator { Alpha = alpha };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(10, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void SamIndicator_DifferentCutoffs_Work()
    {
        var cutoffs = new[] { 2, 8, 16, 30 };

        foreach (var cutoff in cutoffs)
        {
            var indicator = new SamIndicator { Cutoff = cutoff };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(10, indicator.LinesSeries[0].Count);
        }
    }
}
