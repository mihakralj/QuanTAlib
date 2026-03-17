using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RsihIndicatorTests
{
    [Fact]
    public void RsihIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RsihIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RSIH - Ehlers Hann-Windowed RSI", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RsihIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RsihIndicator();

        Assert.Equal(0, RsihIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RsihIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new RsihIndicator { Period = 20 };

        Assert.Contains("RSIH", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RsihIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RsihIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rsih.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RsihIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new RsihIndicator { Period = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RsihIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RsihIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void RsihIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RsihIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RsihIndicator_InternalIndicator_HandlesBarCorrection()
    {
        var ma = new Rsih(3);
        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        var now = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, prices[i]), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        ma.Update(new TValue(now.AddMinutes(9).Ticks, 100), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void RsihIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new RsihIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void RsihIndicator_MultipleHistoricalBars()
    {
        var indicator = new RsihIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void RsihIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new RsihIndicator();
        indicator.Period = 25;
        Assert.Equal(25, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }
}
