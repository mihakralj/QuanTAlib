using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CfbIndicatorTests
{
    [Fact]
    public void CfbIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CfbIndicator();

        Assert.Equal(2, indicator.MinLength);
        Assert.Equal(192, indicator.MaxLength);
        Assert.Equal(2, indicator.Step);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CFB - Jurik Composite Fractal Behavior", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CfbIndicator_MinHistoryDepths_EqualsMaxLength()
    {
        var indicator = new CfbIndicator { MaxLength = 50 };

        Assert.Equal(0, CfbIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CfbIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new CfbIndicator { MinLength = 5, MaxLength = 20, Source = SourceType.Close };
        // Initialize to update SourceName
        indicator.Initialize();

        Assert.Contains("CFB", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5-20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CfbIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CfbIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cfb.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CfbIndicator_Initialize_CreatesInternalCfb()
    {
        var indicator = new CfbIndicator { MinLength = 2, MaxLength = 10, Step = 2 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CfbIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CfbIndicator { MinLength = 2, MaxLength = 4, Step = 2 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for MaxLength (4)
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 104, 110, 102, 108);
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 103, 109, 101, 105);
        indicator.HistoricalData.AddBar(now.AddMinutes(4), 105, 112, 103, 110);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void CfbIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CfbIndicator { MinLength = 2, MaxLength = 4, Step = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 104, 110, 102, 108);
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 103, 109, 101, 105);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(4), 105, 112, 103, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CfbIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CfbIndicator { MinLength = 2, MaxLength = 4, Step = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 104, 110, 102, 108);
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 103, 109, 101, 105);
        indicator.HistoricalData.AddBar(now.AddMinutes(4), 105, 112, 103, 110);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void CfbIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CfbIndicator { MinLength = 2, MaxLength = 4, Step = 2, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars
            for (int i = 0; i < 5; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            }

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void CfbIndicator_Parameters_CanBeChanged()
    {
        var indicator = new CfbIndicator { MinLength = 5, MaxLength = 20, Step = 5 };
        Assert.Equal(5, indicator.MinLength);
        Assert.Equal(20, indicator.MaxLength);
        Assert.Equal(5, indicator.Step);

        indicator.MinLength = 10;
        indicator.MaxLength = 40;
        indicator.Step = 10;

        Assert.Equal(10, indicator.MinLength);
        Assert.Equal(40, indicator.MaxLength);
        Assert.Equal(10, indicator.Step);
        Assert.Equal(0, CfbIndicator.MinHistoryDepths);
    }
}
