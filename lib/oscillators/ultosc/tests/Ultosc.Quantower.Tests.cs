using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class UltoscIndicatorTests
{
    [Fact]
    public void UltoscIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UltoscIndicator();

        Assert.Equal(7, indicator.Period1);
        Assert.Equal(14, indicator.Period2);
        Assert.Equal(28, indicator.Period3);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ULTOSC - Ultimate Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void UltoscIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new UltoscIndicator { Period1 = 7, Period2 = 14, Period3 = 28 };

        Assert.Equal(0, UltoscIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void UltoscIndicator_ShortName_IncludesParameters()
    {
        var indicator = new UltoscIndicator { Period1 = 5, Period2 = 10, Period3 = 20 };
        indicator.Initialize();

        Assert.Contains("ULTOSC", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void UltoscIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new UltoscIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ultosc", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void UltoscIndicator_Initialize_CreatesInternalUltosc()
    {
        var indicator = new UltoscIndicator { Period1 = 7, Period2 = 14, Period3 = 28 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void UltoscIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UltoscIndicator { Period1 = 3, Period2 = 5, Period3 = 7 };
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
    public void UltoscIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UltoscIndicator { Period1 = 3, Period2 = 5, Period3 = 7 };
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

        double value = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(value));
    }
}
