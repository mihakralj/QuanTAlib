using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class PlusDmIndicatorTests
{
    [Fact]
    public void PlusDmIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PlusDmIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("+DM - Plus Directional Movement", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PlusDmIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PlusDmIndicator { Period = 20 };

        Assert.Equal(0, PlusDmIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PlusDmIndicator_Initialize_CreatesInternal()
    {
        var indicator = new PlusDmIndicator { Period = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PlusDmIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PlusDmIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void PlusDmIndicator_ShortName_IsCorrect()
    {
        var indicator = new PlusDmIndicator { Period = 20 };
        Assert.Equal("+DM 20", indicator.ShortName);
    }

    [Fact]
    public void PlusDmIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PlusDmIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PlusDm.Quantower.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }
}
