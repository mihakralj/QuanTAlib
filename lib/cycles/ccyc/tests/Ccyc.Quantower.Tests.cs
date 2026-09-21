using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class CcycIndicatorTests
{
    [Fact]
    public void CcycIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CcycIndicator();

        Assert.Equal(0.07, indicator.Alpha);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CCYC - Ehlers Cyber Cycle", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CcycIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CcycIndicator();

        Assert.Equal(0, CcycIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CcycIndicator_ShortName_IncludesAlpha()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };

        Assert.True(indicator.ShortName.Contains("CCYC", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("0.07", StringComparison.Ordinal));
    }

    [Fact]
    public void CcycIndicator_Initialize_CreatesInternalCcyc()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Cycle + Trigger)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void CcycIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };
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
        Assert.Equal(1, indicator.LinesSeries[1].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void CcycIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void CcycIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists
        Assert.NotNull(indicator);
    }

    [Fact]
    public void CcycIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CcycIndicator();

        Assert.False(string.IsNullOrEmpty(indicator.SourceCodeLink));
        Assert.Contains("Ccyc.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CcycIndicator_MultipleHistoricalBars_AllFinite()
    {
        var indicator = new CcycIndicator { Alpha = 0.07 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + (5 * Math.Sin(2 * Math.PI * i / 20.0));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);
        Assert.Equal(20, indicator.LinesSeries[1].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(i)));
        }
    }

    [Fact]
    public void CcycIndicator_CustomAlpha_ReflectedInShortName()
    {
        var indicator = new CcycIndicator { Alpha = 0.15 };
        Assert.Contains("0.15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SourceType.Open)]
    [InlineData(SourceType.High)]
    [InlineData(SourceType.Low)]
    [InlineData(SourceType.Close)]
    public void CcycIndicator_DifferentSources_DoNotThrow(SourceType sourceType)
    {
        var indicator = new CcycIndicator
        {
            Alpha = 0.07,
            Source = sourceType
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }
}
