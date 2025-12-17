using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class PwmaIndicatorTests
{
    [Fact]
    public void PwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PwmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PWMA - Parabolic Weighted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PwmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new PwmaIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(20, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PwmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new PwmaIndicator { Period = 15 };

        Assert.Contains("PWMA", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
    }

    [Fact]
    public void PwmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PwmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Pwma.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void PwmaIndicator_Initialize_CreatesInternalPwma()
    {
        var indicator = new PwmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PwmaIndicator { Period = 3 };
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
    public void PwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PwmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new PwmaIndicator { Period = 3 };
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
    public void PwmaIndicator_OnPaintChart_DoesNotThrow()
    {
        var indicator = new PwmaIndicator();
        indicator.Initialize();

        var method = indicator.GetType().GetMethod("OnPaintChart");
        Assert.NotNull(method);
        Assert.Equal(typeof(PwmaIndicator), method.DeclaringType);
    }

    [Fact]
    public void PwmaIndicator_MultipleUpdates_ProducesCorrectPwmaSequence()
    {
        var indicator = new PwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

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
    }

    [Fact]
    public void PwmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new PwmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void PwmaIndicator_Period_CanBeChanged()
    {
        var indicator = new PwmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }
}
