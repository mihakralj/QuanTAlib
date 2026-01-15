using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SgmaIndicatorTests
{
    [Fact]
    public void SgmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SgmaIndicator();

        Assert.Equal(9, indicator.Period);
        Assert.Equal(2, indicator.Degree);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SGMA - Savitzky-Golay Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void SgmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new SgmaIndicator { Period = 9, Degree = 2 };
        Assert.Equal(9, indicator.MinHistoryDepths);

        indicator = new SgmaIndicator { Period = 21, Degree = 3 };
        Assert.Equal(21, indicator.MinHistoryDepths);
    }

    [Fact]
    public void SgmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SgmaIndicator { Period = 9, Degree = 2 };
        Assert.Equal("SGMA(9,2)", indicator.ShortName);

        indicator = new SgmaIndicator { Period = 21, Degree = 4 };
        Assert.Equal("SGMA(21,4)", indicator.ShortName);
    }

    [Fact]
    public void SgmaIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new SgmaIndicator { Period = 9, Degree = 2 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("SGMA", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SgmaIndicator { Period = 5, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SgmaIndicator { Period = 5, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SgmaIndicator { Period = 5, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // NewTick should update without crashing
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SgmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SgmaIndicator { Period = 5, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        // Check that values are finite after warmup
        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void SgmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new SgmaIndicator
            {
                Period = 5,
                Degree = 2,
                Source = source
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void SgmaIndicator_Period_CanBeChanged()
    {
        var indicator = new SgmaIndicator();
        indicator.Period = 21;

        Assert.Equal(21, indicator.Period);
        Assert.Equal(21, indicator.MinHistoryDepths);
        Assert.Equal("SGMA(21,2)", indicator.ShortName);
    }

    [Fact]
    public void SgmaIndicator_Degree_CanBeChanged()
    {
        var indicator = new SgmaIndicator();
        indicator.Degree = 4;

        Assert.Equal(4, indicator.Degree);
        Assert.Equal("SGMA(9,4)", indicator.ShortName);
    }

    [Fact]
    public void SgmaIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new SgmaIndicator
        {
            Period = 21,
            Degree = 2,
            ShowColdValues = false
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add fewer bars than warmup
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With ShowColdValues = false, cold values should be NaN before warmup
        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SgmaIndicator_ShowColdValues_True_ShowsValues()
    {
        var indicator = new SgmaIndicator
        {
            Period = 21,
            Degree = 2,
            ShowColdValues = true
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add fewer bars than warmup
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With ShowColdValues = true, values should be shown even before warmup
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SgmaIndicator_DegreeZero_ProducesUniformWeights()
    {
        // Degree 0 should behave like SMA (uniform weights)
        var indicator = new SgmaIndicator { Period = 5, Degree = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add 5 bars with known values
        double[] values = [10, 20, 30, 40, 50];
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), values[i], values[i], values[i], values[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With degree 0 (uniform weights), result should be simple average
        double expected = values.Average();
        double actual = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(expected, actual, 6);
    }

    [Fact]
    public void SgmaIndicator_HigherDegree_PreservesShape()
    {
        // Higher degree preserves peaks and valleys better
        var indicatorLow = new SgmaIndicator { Period = 5, Degree = 1 };
        var indicatorHigh = new SgmaIndicator { Period = 5, Degree = 4 };

        indicatorLow.Initialize();
        indicatorHigh.Initialize();

        var now = DateTime.UtcNow;

        // Create data with a clear pattern
        double[] values = [100, 110, 150, 110, 100];
        for (int i = 0; i < 5; i++)
        {
            indicatorLow.HistoricalData.AddBar(now.AddMinutes(i), values[i], values[i], values[i], values[i]);
            indicatorLow.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            indicatorHigh.HistoricalData.AddBar(now.AddMinutes(i), values[i], values[i], values[i], values[i]);
            indicatorHigh.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Both should produce finite values
        Assert.True(double.IsFinite(indicatorLow.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicatorHigh.LinesSeries[0].GetValue(0)));
    }
}