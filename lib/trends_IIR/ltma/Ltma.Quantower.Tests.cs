using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LtmaIndicatorTests
{
    [Fact]
    public void LtmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LtmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LTMA - Linear Trend Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LtmaIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new LtmaIndicator { Period = 20 };

        Assert.Equal(0, LtmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void LtmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new LtmaIndicator { Period = 14 };

        Assert.Contains("LTMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void LtmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new LtmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ltma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void LtmaIndicator_Initialize_CreatesInternalLtma()
    {
        var indicator = new LtmaIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void LtmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LtmaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void LtmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LtmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add another bar
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LtmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LtmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void LtmaIndicator_MultipleHistoricalBars_ComputesAll()
    {
        var indicator = new LtmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 3, price - 2, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void LtmaIndicator_PeriodChange_ReinitializesOnInit()
    {
        var indicator = new LtmaIndicator { Period = 10 };
        indicator.Initialize();

        indicator.Period = 20;
        indicator.Initialize();

        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void LtmaIndicator_Description_IsSet()
    {
        var indicator = new LtmaIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.Description));
    }
}
