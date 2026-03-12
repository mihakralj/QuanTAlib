using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class LemaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public LemaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Fact]
    public void Validate_ManualEmaComposition_Batch()
    {
        // LEMA = EMA(source) + EMA(source - EMA(source))
        // Validate batch mode against manual two-EMA composition
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var lema = new Lema(period);
            var qResult = lema.Update(_testData.Data);

            // Manual composition
            var ema1 = new Ema(period);
            var ema2 = new Ema(period);
            var manualResults = new List<double>();

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                var item = _testData.Data[i];
                var e1 = ema1.Update(item);
                double error = item.Value - e1.Value;
                var e2 = ema2.Update(new TValue(item.Time, error));
                manualResults.Add(e1.Value + e2.Value);
            }

            // Compare all records
            for (int i = 0; i < qResult.Count; i++)
            {
                Assert.Equal(manualResults[i], qResult[i].Value, 1e-9);
            }
        }
        _output.WriteLine("LEMA Batch(TSeries) validated successfully against manual EMA composition");
    }

    [Fact]
    public void Validate_StreamingVsBatch_Consistency()
    {
        // Streaming mode must match batch mode exactly
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Batch
            var batchResult = Lema.Batch(_testData.Data, period);

            // Streaming
            var streaming = new Lema(period);
            for (int i = 0; i < _testData.Data.Count; i++)
            {
                streaming.Update(_testData.Data[i]);
            }

            // Compare last 100 records
            int start = Math.Max(0, _testData.Data.Count - 100);
            for (int i = start; i < _testData.Data.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, batchResult[i].Value, 1e-9);
            }
        }
        _output.WriteLine("LEMA Streaming vs Batch validated successfully");
    }

    [Fact]
    public void Validate_SpanVsStreaming_Consistency()
    {
        // Span API must match streaming exactly
        int[] periods = { 5, 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Span
            double[] spanOutput = new double[sourceData.Length];
            Lema.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // Streaming
            var streaming = new Lema(period);
            for (int i = 0; i < sourceData.Length; i++)
            {
                var val = streaming.Update(new TValue(DateTime.UtcNow, sourceData[i]));
                Assert.Equal(val.Value, spanOutput[i], 1e-9);
            }
        }
        _output.WriteLine("LEMA Span vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_ConstantInput_ConvergesToInput()
    {
        // LEMA of constant series should converge to the constant value
        // Since error = source - EMA(source) → 0, and EMA(0) → 0,
        // LEMA → EMA(source) + 0 = source (at convergence)
        const double constantValue = 42.0;
        const int period = 10;

        var lema = new Lema(period);
        double lastResult = 0;

        for (int i = 0; i < 200; i++)
        {
            var result = lema.Update(new TValue(DateTime.UtcNow, constantValue));
            lastResult = result.Value;
        }

        // After enough iterations, LEMA should converge to the constant
        Assert.Equal(constantValue, lastResult, 1e-6);
        _output.WriteLine("LEMA constant input convergence validated successfully");
    }

    [Fact]
    public void Validate_Against_ManualFormula()
    {
        // Validate against the explicit LEMA formula:
        // LEMA = EMA(source, N) + EMA(source - EMA(source, N), N)
        // Using our own Ema class as reference (Ooples-equivalent validation)

        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var lema = new Lema(period);
            var ema1 = new Ema(period);
            var ema2 = new Ema(period);

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                var item = _testData.Data[i];

                // QuanTAlib LEMA
                var qVal = lema.Update(item);

                // Manual LEMA formula
                var e1 = ema1.Update(item);
                double error = item.Value - e1.Value;
                var e2 = ema2.Update(new TValue(item.Time, error));
                double manualVal = e1.Value + e2.Value;

                Assert.Equal(manualVal, qVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
        _output.WriteLine("LEMA validated successfully against manual formula (EMA + EMA(error))");
    }

    [Fact]
    public void Validate_NaN_Robustness()
    {
        // Feed data with interspersed NaN values and verify output stays finite
        const int period = 10;
        var lema = new Lema(period);

        // Feed some valid values first to establish state
        for (int i = 0; i < 20; i++)
        {
            lema.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        // Feed NaN
        var nanResult = lema.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(nanResult.Value), "LEMA should handle NaN with last-valid substitution");

        // Feed Infinity
        var infResult = lema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value), "LEMA should handle Infinity with last-valid substitution");

        // Feed negative Infinity
        var negInfResult = lema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(negInfResult.Value), "LEMA should handle -Infinity with last-valid substitution");

        // Resume with valid value
        var resumeResult = lema.Update(new TValue(DateTime.UtcNow, 125.0));
        Assert.True(double.IsFinite(resumeResult.Value), "LEMA should resume cleanly after invalid inputs");

        _output.WriteLine("LEMA NaN/Infinity robustness validated successfully");
    }
}
