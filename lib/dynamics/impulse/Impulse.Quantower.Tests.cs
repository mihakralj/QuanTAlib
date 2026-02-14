using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class ImpulseIndicatorTests
{
    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var indicator = new ImpulseIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("Elder Impulse System", indicator.Name);
    }

    [Fact]
    public void Constructor_SetsDescription()
    {
        var indicator = new ImpulseIndicator();
        Assert.Contains("Elder", indicator.Description, StringComparison.Ordinal);
        Assert.Contains("Impulse", indicator.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultParameters_AreCorrect()
    {
        var indicator = new ImpulseIndicator();
        Assert.Equal(13, indicator.EmaPeriod);
        Assert.Equal(12, indicator.MacdFast);
        Assert.Equal(26, indicator.MacdSlow);
        Assert.Equal(9, indicator.MacdSignal);
    }

    [Fact]
    public void DefaultShowColdValues_IsTrue()
    {
        var indicator = new ImpulseIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new ImpulseIndicator { EmaPeriod = 8, MacdFast = 5, MacdSlow = 20, MacdSignal = 7 };
        Assert.Equal("IMPULSE(8,5,20,7)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_EqualsZero()
    {
        var indicator = new ImpulseIndicator();
        Assert.Equal(0, ImpulseIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SeparateWindow_IsFalse()
    {
        var indicator = new ImpulseIndicator();
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void OnBackGround_IsTrue()
    {
        var indicator = new ImpulseIndicator();
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void Constructor_AddsOneLineSeries()
    {
        var indicator = new ImpulseIndicator();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Parameters_CanBeChanged()
    {
        var indicator = new ImpulseIndicator();

        indicator.EmaPeriod = 20;
        indicator.MacdFast = 8;
        indicator.MacdSlow = 30;
        indicator.MacdSignal = 5;

        Assert.Equal(20, indicator.EmaPeriod);
        Assert.Equal(8, indicator.MacdFast);
        Assert.Equal(30, indicator.MacdSlow);
        Assert.Equal(5, indicator.MacdSignal);
    }

    [Fact]
    public void ShowColdValues_CanBeChanged()
    {
        var indicator = new ImpulseIndicator();
        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void Initialize_CreatesInternalIndicator()
    {
        var indicator = new ImpulseIndicator { EmaPeriod = 13 };
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ImpulseIndicator { EmaPeriod = 13 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ImpulseIndicator { EmaPeriod = 13 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }
}
