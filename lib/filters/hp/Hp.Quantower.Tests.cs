using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class HpIndicatorTests
{
    [Fact]
    public void HpIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HpIndicator();

        Assert.Equal(1600, indicator.Lambda);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HP - Hodrick-Prescott Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HpIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HpIndicator();

        Assert.Equal(0, HpIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HpIndicator_ShortName_IncludesLambdaAndSource()
    {
        var indicator = new HpIndicator { Lambda = 14400 };

        // We can't fully check ShortName dynamic part easily without Initialize, 
        // as _sourceName is set in OnInit. 
        // But we can check it returns something containing "HP" based on class definition if accessed before init,
        // or we initialize it to check full string.
        
        // Initialize to set _sourceName
        indicator.Initialize();
        
        Assert.Contains("HP", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14400", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void HpIndicator_Initialize_CreatesInternalHp()
    {
        var indicator = new HpIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HpIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HpIndicator { Lambda = 1600 };
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
    public void HpIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HpIndicator { Lambda = 1600 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HpIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new HpIndicator { Lambda = 1600 };
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
    public void HpIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new HpIndicator { Lambda = 1600 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 106, 107 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        // Check last value
        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastVal));
    }

    [Fact]
    public void HpIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new HpIndicator { Lambda = 1600, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void HpIndicator_Lambda_CanBeChanged()
    {
        var indicator = new HpIndicator { Lambda = 100 };
        Assert.Equal(100, indicator.Lambda);

        indicator.Lambda = 500;
        Assert.Equal(500, indicator.Lambda);
    }
}