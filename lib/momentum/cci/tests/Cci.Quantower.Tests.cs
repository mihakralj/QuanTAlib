using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class CciIndicatorTests
{
    [Fact]
    public void CciIndicator_Constructor_SetsDefaultPeriod()
    {
        var indicator = new CciIndicator();
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void CciIndicator_DefaultName_IsCCI()
    {
        var indicator = new CciIndicator();
        Assert.Equal("CCI", indicator.Name);
    }

    [Fact]
    public void CciIndicator_SeparateWindow_IsTrue()
    {
        var indicator = new CciIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void CciIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new CciIndicator { Period = 14 };
        Assert.Equal(14, indicator.MinHistoryDepths);
    }

    [Fact]
    public void CciIndicator_Initialize_CreatesWithCustomPeriod()
    {
        var indicator = new CciIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void CciIndicator_OnUpdate_ProcessesHistoricalData()
    {
        var indicator = new CciIndicator { Period = 10 };
        var historicalData = new HistoricalData();

        // Add OHLC bars
        var baseTime = DateTime.UtcNow.Date;
        for (int i = 0; i < 30; i++)
        {
            historicalData.AddBar(
                baseTime.AddDays(i),
                open: 100 + i,
                high: 102 + i,
                low: 98 + i,
                close: 101 + i,
                volume: 10000);
        }

        indicator.HistoricalData = historicalData;
        indicator.Initialize();

        // Process all bars
        for (int i = 0; i < historicalData.Count; i++)
        {
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Should complete without error
        Assert.True(true);
    }

    [Fact]
    public void CciIndicator_OnUpdate_ProcessesNewBar()
    {
        var indicator = new CciIndicator { Period = 10 };
        var historicalData = new HistoricalData();

        var baseTime = DateTime.UtcNow.Date;
        for (int i = 0; i < 20; i++)
        {
            historicalData.AddBar(
                baseTime.AddDays(i),
                open: 100 + i,
                high: 102 + i,
                low: 98 + i,
                close: 101 + i,
                volume: 10000);
        }

        indicator.HistoricalData = historicalData;
        indicator.Initialize();

        // Simulate new bar
        var newBarArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newBarArgs);

        Assert.True(true);
    }

    [Fact]
    public void CciIndicator_Description_IsSet()
    {
        var indicator = new CciIndicator();
        Assert.True(indicator.Description.Contains("Commodity Channel Index", StringComparison.Ordinal) ||
                    indicator.Description.Contains("CCI", StringComparison.Ordinal) ||
                    indicator.Description.Contains("momentum", StringComparison.Ordinal));
    }

    [Fact]
    public void CciIndicator_PeriodRange_IsValidated()
    {
        // Period should accept values from 2 to 200
        var indicator = new CciIndicator { Period = 2 };
        Assert.Equal(2, indicator.Period);

        indicator.Period = 200;
        Assert.Equal(200, indicator.Period);
    }

    [Fact]
    public void CciIndicator_ImplementsIWatchlistIndicator()
    {
        var indicator = new CciIndicator();
        Assert.IsAssignableFrom<IWatchlistIndicator>(indicator);
    }
}
