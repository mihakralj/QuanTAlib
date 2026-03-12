using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class CcorIndicatorTests
{
    [Fact]
    public void CcorIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CcorIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(9.0, indicator.Threshold);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CCOR - Ehlers Correlation Cycle", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CcorIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CcorIndicator();

        Assert.Equal(0, CcorIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CcorIndicator_ShortName_IncludesPeriodAndThreshold()
    {
        var indicator = new CcorIndicator { Period = 20, Threshold = 9.0 };

        Assert.True(indicator.ShortName.Contains("CCOR", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("20", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("9.0", StringComparison.Ordinal));
    }

    [Fact]
    public void CcorIndicator_Initialize_CreatesInternalCcor()
    {
        var indicator = new CcorIndicator { Period = 20, Threshold = 9.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Real + Imag + Angle + State)
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void CcorIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CcorIndicator { Period = 20, Threshold = 9.0 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // All 4 line series should have a value
        for (int s = 0; s < 4; s++)
        {
            Assert.Equal(1, indicator.LinesSeries[s].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[s].GetValue(0)),
                $"Line series {s} should be finite");
        }
    }

    [Fact]
    public void CcorIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CcorIndicator { Period = 20, Threshold = 9.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        for (int s = 0; s < 4; s++)
        {
            Assert.Equal(2, indicator.LinesSeries[s].Count);
        }
    }

    [Fact]
    public void CcorIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CcorIndicator { Period = 20, Threshold = 9.0 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists
        Assert.NotNull(indicator);
    }

    [Fact]
    public void CcorIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CcorIndicator();

        Assert.False(string.IsNullOrEmpty(indicator.SourceCodeLink));
        Assert.Contains("Ccor.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CcorIndicator_MultipleHistoricalBars_AllFinite()
    {
        var indicator = new CcorIndicator { Period = 10, Threshold = 9.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + (5 * Math.Sin(2 * Math.PI * i / 20.0));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        for (int s = 0; s < 4; s++)
        {
            Assert.Equal(30, indicator.LinesSeries[s].Count);
            for (int i = 0; i < 30; i++)
            {
                Assert.True(double.IsFinite(indicator.LinesSeries[s].GetValue(i)),
                    $"Line series {s} at bar {i} should be finite");
            }
        }
    }

    [Fact]
    public void CcorIndicator_CustomPeriod_ReflectedInShortName()
    {
        var indicator = new CcorIndicator { Period = 30, Threshold = 5.0 };
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5.0", indicator.ShortName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SourceType.Open)]
    [InlineData(SourceType.High)]
    [InlineData(SourceType.Low)]
    [InlineData(SourceType.Close)]
    public void CcorIndicator_DifferentSources_DoNotThrow(SourceType sourceType)
    {
        var indicator = new CcorIndicator
        {
            Period = 20,
            Threshold = 9.0,
            Source = sourceType
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }
}
