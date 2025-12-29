using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class RsiTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Rsi(0));
        Assert.Throws<ArgumentException>(() => new Rsi(-1));
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < series.Count; i++)
        {
            rsi.Update(series.Close[i]);
        }

        Assert.True(double.IsFinite(rsi.Last.Value));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var rsi = new Rsi(14);
        Assert.Equal("Rsi(14)", rsi.Name);
        Assert.False(rsi.IsHot);
        Assert.Equal(0, rsi.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(rsi.IsHot);

        // RSI wraps two RMA indicators (gain and loss)
        // RMA wraps EMA with alpha = 1/period
        // EMA becomes hot when E <= 0.05, which occurs after ~-ln(0.05)/ln(1-alpha) values
        // For period=14: alpha=1/14, needs ~40 values to become hot
        // Both RMAs must be hot for RSI to be hot
        for (int i = 0; i < 45; i++)
        {
            rsi.Update(series.Close[i]);
            if (i < 40)
            {
                Assert.False(rsi.IsHot, $"Should not be hot at index {i}");
            }
        }

        // After sufficient warmup, IsHot should be true
        Assert.True(rsi.IsHot);
    }

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var rsi = new Rsi(5);
        var gbm = new GBM();
        var series = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 9; i++)
        {
            rsi.Update(series.Close[i], isNew: true);
        }

        var val1 = rsi.Update(series.Close[9], isNew: true);
        var nextTime = series.Close[9].Time + TimeSpan.FromMinutes(1).Ticks;
        var val2 = rsi.Update(new TValue(nextTime, series.Close[9].Value + 1), isNew: true);

        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var rsi = new Rsi(5);
        var gbm = new GBM();
        var series = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 9; i++)
        {
            rsi.Update(series.Close[i]);
        }

        var val1 = rsi.Update(series.Close[9], isNew: true);
        var modifiedValue = new TValue(series.Close[9].Time, series.Close[9].Value + 5);
        var val2 = rsi.Update(modifiedValue, isNew: false);

        // Should update the same bar
        Assert.Equal(val1.Time, val2.Time);
        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            rsi.Update(series.Close[i]);
        }

        // Update with 100th point (isNew=true)
        rsi.Update(series.Close[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedValue = new TValue(series.Close[99].Time, series.Close[99].Value + 2.0);
        var val2 = rsi.Update(modifiedValue, false);

        // Create new instance and feed up to modified
        var rsi2 = new Rsi(14);
        for (int i = 0; i < 99; i++)
        {
            rsi2.Update(series.Close[i]);
        }
        var val3 = rsi2.Update(modifiedValue, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var rsi = new Rsi(10);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed N values
        for (int i = 0; i < 30; i++)
        {
            rsi.Update(series.Close[i]);
        }

        var originalValue = rsi.Last;

        // Make M updates with isNew=false
        for (int m = 0; m < 5; m++)
        {
            var modifiedValue = new TValue(series.Close[29].Time, series.Close[29].Value + m);
            rsi.Update(modifiedValue, isNew: false);
        }

        // Restore with original 30th value
        var restoredValue = rsi.Update(series.Close[29], isNew: false);

        Assert.Equal(originalValue.Value, restoredValue.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < series.Count; i++)
        {
            rsi.Update(series.Close[i]);
        }

        Assert.True(rsi.IsHot);
        var valueBefore = rsi.Last.Value;

        rsi.Reset();

        Assert.False(rsi.IsHot);
        Assert.Equal(0, rsi.Last.Value);

        // Feed again
        for (int i = 0; i < series.Count; i++)
        {
            rsi.Update(series.Close[i]);
        }

        Assert.True(rsi.IsHot);
        Assert.Equal(valueBefore, rsi.Last.Value, 1e-9);
    }

    [Fact]
    public void NaN_Input_DoesNotCrash()
    {
        var rsi = new Rsi(10);
        var gbm = new GBM();
        var series = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            rsi.Update(series.Close[i]);
        }

        // Feed NaN
        var nanValue = new TValue(DateTime.UtcNow, double.NaN);
        var result = rsi.Update(nanValue);

        // Should not crash and should return finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_DoesNotCrash()
    {
        var rsi = new Rsi(10);
        var gbm = new GBM();
        var series = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            rsi.Update(series.Close[i]);
        }

        var infValue = new TValue(DateTime.UtcNow, double.PositiveInfinity);
        var result = rsi.Update(infValue);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesCorrectly()
    {
        var rsi = new Rsi(10);
        var gbm = new GBM();
        var series = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 20; i++)
        {
            rsi.Update(series.Close[i]);
        }

        // Feed multiple NaN
        for (int i = 0; i < 5; i++)
        {
            var nanValue = new TValue(DateTime.UtcNow.AddMinutes(i), double.NaN);
            var result = rsi.Update(nanValue);
            Assert.True(double.IsFinite(result.Value));
        }

        // Continue with valid data
        for (int i = 20; i < 30; i++)
        {
            var result = rsi.Update(series.Close[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void HandlesFlatLine()
    {
        var rsi = new Rsi(5);
        var series = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        var result = rsi.Update(series);

        // Flat line: no gains or losses, RSI = 50
        Assert.Equal(50, result.Last.Value);
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(rsi.Update(series.Close[i]).Value);
        }

        var rsi2 = new Rsi(14);
        var seriesResults = rsi2.Update(series.Close);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var rsi = new Rsi(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(rsi.Update(series.Close[i]).Value);
        }

        var batchResults = Rsi.Batch(series.Close, 14);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void SpanCalc_ValidatesInput()
    {
        var source = new double[10];
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Rsi.Calculate(source, output, 14));
    }

    [Fact]
    public void SpanCalc_InvalidPeriod_Throws()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentException>(() => Rsi.Calculate(source, output, 0));
        Assert.Throws<ArgumentException>(() => Rsi.Calculate(source, output, -1));
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var gbm = new GBM();
        var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var batchResults = Rsi.Batch(series.Close, 14);

        var output = new double[series.Count];
        Rsi.Calculate(series.Close.Values, output, 14);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.Equal(batchResults.Values[i], output[i], 1e-9);
        }
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        var source = new double[20];
        var gbm = new GBM();
        var series = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 20; i++)
        {
            source[i] = series.Close[i].Value;
        }

        source[10] = double.NaN;
        source[15] = double.NaN;

        var output = new double[20];
        Rsi.Calculate(source, output, 10);

        // Should not crash and produce finite results
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]) || Math.Abs(output[i]) < 1e-10);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 14;
        var gbm = new GBM();
        var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchSeries = Rsi.Batch(series.Close, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var spanOutput = new double[series.Count];
        Rsi.Calculate(series.Close.Values, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingRsi = new Rsi(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingRsi.Update(series.Close[i]);
        }
        double streamingResult = streamingRsi.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingRsi = new Rsi(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series.Close[i]);
        }
        double eventingResult = eventingRsi.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 9);
        Assert.Equal(expected, streamingResult, 9);
        Assert.Equal(expected, eventingResult, 9);
    }

    [Fact]
    public void Chainability_Works()
    {
        var rsi = new Rsi(14);
        var gbm = new GBM();
        var series = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TSeries chain
        var result = rsi.Update(series.Close);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TValue chain (returns TValue)
        var result2 = rsi.Update(series.Close[0]);
        Assert.IsType<TValue>(result2);
    }
}
