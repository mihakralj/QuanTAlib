using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// CHANGE validation tests - validates against direct mathematical computation
/// and Tulip's ROC indicator (both return decimal format: 0.1 = 10%)
/// </summary>
public class ChangeValidationTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.05, seed: 60100);
    private const double Tolerance = 1e-10;

    [Fact]
    public void Change_Batch_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 10;

        var result = Change.Batch(series, period);

        for (int i = period; i < series.Count; i++)
        {
            double current = series[i].Value;
            double past = series[i - period].Value;
            double expected = past != 0.0 ? (current - past) / past : 0.0;
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Change_Streaming_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 5;

        var indicator = new Change(period);
        var results = new List<double>();
        ReadOnlySpan<double> values = series.Values;

        for (int i = 0; i < series.Count; i++)
        {
            indicator.Update(series[i]);
            results.Add(indicator.Last.Value);
        }

        for (int i = period; i < series.Count; i++)
        {
            double current = values[i];
            double past = values[i - period];
            double expected = past != 0.0 ? (current - past) / past : 0.0;
            Assert.Equal(expected, results[i], Tolerance);
        }
    }

    [Fact]
    public void Change_Span_MatchesMathFormula()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = bars.Close.Values.ToArray();
        var output = new double[values.Length];
        int period = 10;

        Change.Batch(values, output, period);

        for (int i = period; i < values.Length; i++)
        {
            double current = values[i];
            double past = values[i - period];
            double expected = past != 0.0 ? (current - past) / past : 0.0;
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Change_Validate_Tulip_Batch()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tData = source.Values.ToArray();
        int period = 10;

        // Calculate QuanTAlib Change
        var qResult = Change.Batch(source, period);

        // Calculate Tulip ROC (returns percentage)
        var rocIndicator = Tulip.Indicators.roc;
        double[][] inputs = [tData];
        double[] options = [period];
        int lookback = period;
        double[][] outputs = [new double[tData.Length - lookback]];

        rocIndicator.Run(inputs, options, outputs);
        var tResult = outputs[0];

        // Compare (Tulip ROC returns same format as QuanTAlib CHANGE)
        for (int i = 0; i < tResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tResult[i], qResult[qIdx].Value, Tolerance);
        }
    }

    [Fact]
    public void Change_Validate_Tulip_Streaming()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tData = source.Values.ToArray();
        int period = 10;

        // Calculate QuanTAlib Change (streaming)
        var indicator = new Change(period);
        var qResults = new List<double>();
        foreach (var item in source)
        {
            qResults.Add(indicator.Update(item).Value);
        }

        // Calculate Tulip ROC
        var rocIndicator = Tulip.Indicators.roc;
        double[][] inputs = [tData];
        double[] options = [period];
        int lookback = period;
        double[][] outputs = [new double[tData.Length - lookback]];

        rocIndicator.Run(inputs, options, outputs);
        var tResult = outputs[0];

        // Compare (Tulip ROC returns same format as QuanTAlib CHANGE)
        for (int i = 0; i < tResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tResult[i], qResults[qIdx], Tolerance);
        }
    }

    [Fact]
    public void Change_ManualCalculation()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        double[] values = [100.0, 105.0, 102.0, 108.0, 104.0];

        for (int i = 0; i < values.Length; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), values[i]));

            if (i == 0)
            {
                Assert.Equal(0.0, indicator.Last.Value);
            }
            else
            {
                double expectedChange = (values[i] - values[i - 1]) / values[i - 1];
                Assert.Equal(expectedChange, indicator.Last.Value, Tolerance);
            }
        }
    }

    [Fact]
    public void Change_AllModesConsistent()
    {
        int count = 50;
        int period = 5;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60103);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Batch
        var batchResult = Change.Batch(source, period);

        // Streaming
        var streamingIndicator = new Change(period);
        var streamingResults = new double[count];
        for (int i = 0; i < source.Count; i++)
        {
            streamingIndicator.Update(source[i]);
            streamingResults[i] = streamingIndicator.Last.Value;
        }

        // Span
        var values = source.Values.ToArray();
        var spanOutput = new double[count];
        Change.Batch(values, spanOutput, period);

        // Event-driven
        var eventIndicator = new Change(period);
        var eventResults = new double[count];
        int eventIdx = 0;
        eventIndicator.Pub += (object? _, in TValueEventArgs e) => eventResults[eventIdx++] = e.Value.Value;
        for (int i = 0; i < source.Count; i++)
        {
            eventIndicator.Update(source[i]);
        }

        // Compare all modes
        for (int i = period; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], Tolerance);
            Assert.Equal(batchResult[i].Value, spanOutput[i], Tolerance);
            Assert.Equal(batchResult[i].Value, eventResults[i], Tolerance);
        }
    }

    [Fact]
    public void Change_DifferentPeriods_MatchTulip()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        var values = source.Values.ToArray();

        foreach (int period in new[] { 1, 5, 10, 20 })
        {
            var result = Change.Batch(source, period);

            // Calculate Tulip ROC
            var rocIndicator = Tulip.Indicators.roc;
            double[][] inputs = [values];
            double[] options = [period];
            int lookback = period;
            double[][] outputs = [new double[values.Length - lookback]];

            rocIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare
            for (int i = 0; i < tResult.Length; i++)
            {
                int qIdx = i + lookback;
                Assert.Equal(tResult[i], result[qIdx].Value, Tolerance);
            }
        }
    }

    [Fact]
    public void Change_KnownValues()
    {
        // Test with simple known sequence
        double[] data = [100, 110, 99, 120, 100];
        int period = 1;

        // Expected: 0, 0.1, -0.1, 0.21212..., -0.16666...
        double[] expected =
        [
            0.0,
            0.1,                        // (110-100)/100
            -0.1,                       // (99-110)/110
            120.0 / 99.0 - 1.0,         // (120-99)/99
            100.0 / 120.0 - 1.0         // (100-120)/120
        ];

        var indicator = new Change(period);
        for (int i = 0; i < data.Length; i++)
        {
            var result = indicator.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, Tolerance);
        }
    }

    [Fact]
    public void Change_Period2_KnownValues()
    {
        double[] data = [100, 105, 120, 110, 130];
        int period = 2;

        // Expected changes comparing to 2 bars ago:
        // [0]: 0 (not enough data)
        // [1]: 0 (not enough data)
        // [2]: (120-100)/100 = 0.2
        // [3]: (110-105)/105 = 0.0476...
        // [4]: (130-120)/120 = 0.0833...

        var indicator = new Change(period);
        var results = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            results[i] = indicator.Update(new TValue(DateTime.UtcNow, data[i])).Value;
        }

        Assert.Equal(0.0, results[0], Tolerance);
        Assert.Equal(0.0, results[1], Tolerance);
        Assert.Equal(0.2, results[2], Tolerance);
        Assert.Equal((110.0 - 105.0) / 105.0, results[3], Tolerance);
        Assert.Equal((130.0 - 120.0) / 120.0, results[4], Tolerance);
    }
}
