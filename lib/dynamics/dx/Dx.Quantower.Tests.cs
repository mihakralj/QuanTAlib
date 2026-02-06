using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class DxIndicatorTests
{
    [Fact]
    public void DxIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DxIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DX - Directional Movement Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DxIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DxIndicator { Period = 20 };

        Assert.Equal(0, DxIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DxIndicator_Initialize_CreatesInternalDx()
    {
        var indicator = new DxIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (DX, +DI, -DI)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void DxIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DxIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double dx = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(dx));
    }

    [Fact]
    public void DxIndicator_ShortName_IsCorrect()
    {
        var indicator = new DxIndicator { Period = 20 };
        Assert.Equal("DX 20", indicator.ShortName);
    }

    [Fact]
    public void DxIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DxIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dx.Quantower.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }
}
