using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class EeoIndicatorTests
{
    [Fact]
    public void EeoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EeoIndicator();

        Assert.Equal(20, indicator.BandEdge);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EEO - Ehlers Elegant Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EeoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EeoIndicator();

        Assert.Equal(0, EeoIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EeoIndicator_ShortName_IncludesBandEdgeAndSource()
    {
        var indicator = new EeoIndicator { BandEdge = 30 };

        Assert.Contains("EEO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void EeoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new EeoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Eeo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void EeoIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new EeoIndicator { BandEdge = 20 };

        indicator.Initialize();

        // After init, one line series should exist (EEO is single output)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EeoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EeoIndicator { BandEdge = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void EeoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EeoIndicator { BandEdge = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EeoIndicator_InternalIndicator_HandlesBarCorrection()
    {
        // Test the underlying Eeo with isNew=false (bar correction)
        // Use zigzag data to avoid saturation
        var ma = new Eeo(3);
        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        var now = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, prices[i]), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        // Correct last bar with a moderately different value
        ma.Update(new TValue(now.AddMinutes(9).Ticks, 100), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void EeoIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new EeoIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void EeoIndicator_MultipleHistoricalBars()
    {
        var indicator = new EeoIndicator { BandEdge = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        // All values should be finite
        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void EeoIndicator_BandEdgeChange_UpdatesConfig()
    {
        var indicator = new EeoIndicator();
        indicator.BandEdge = 25;
        Assert.Equal(25, indicator.BandEdge);

        indicator.BandEdge = 50;
        Assert.Equal(50, indicator.BandEdge);
    }
}
