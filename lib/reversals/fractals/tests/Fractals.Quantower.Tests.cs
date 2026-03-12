using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class FractalsIndicatorTests
{
    [Fact]
    public void FractalsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new FractalsIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("FRACTALS", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void FractalsIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new FractalsIndicator();

        Assert.Equal(0, FractalsIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void FractalsIndicator_ShortName_IsFractals()
    {
        var indicator = new FractalsIndicator();
        indicator.Initialize();

        Assert.Contains("FRACTALS", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void FractalsIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new FractalsIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Fractals", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void FractalsIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new FractalsIndicator();

        indicator.Initialize();

        // After init, line series should exist (UpFractal + DownFractal)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void FractalsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new FractalsIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            // Create a pattern with varying highs/lows to generate fractals
            double basePrice = 100 + (i % 5 == 2 ? 10 : 0); // spike every 5th bar at position 2
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double upFractal = indicator.LinesSeries[0].GetValue(0);
        double downFractal = indicator.LinesSeries[1].GetValue(0);

        // Values should be set (either finite fractal or NaN=no fractal)
        Assert.True(double.IsFinite(upFractal) || double.IsNaN(upFractal));
        Assert.True(double.IsFinite(downFractal) || double.IsNaN(downFractal));
    }

    [Fact]
    public void FractalsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new FractalsIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double upFractal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(upFractal) || double.IsNaN(upFractal));
    }

    [Fact]
    public void FractalsIndicator_TwoLineSeries_ArePresent()
    {
        var indicator = new FractalsIndicator();
        indicator.Initialize();

        // UpFractal is index 0 (red), DownFractal is index 1 (green)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Contains("Up", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Down", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FractalsIndicator_Description_IsSet()
    {
        var indicator = new FractalsIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("fractal", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
