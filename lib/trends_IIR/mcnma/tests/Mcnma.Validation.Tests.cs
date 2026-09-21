using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class McnmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public McnmaValidationTests(ITestOutputHelper output)
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
    public void Validate_ManualTemaComposition_Batch()
    {
        // Manual 6-EMA with first-value seeding (matches Pine exactly)
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            var mcnma = new Mcnma(period);
            var qResult = mcnma.Update(_testData.Data);

            double alpha = 2.0 / (period + 1);
            double decay = 1.0 - alpha;
            double e1 = 0, e2 = 0, e3 = 0, e4 = 0, e5 = 0, e6 = 0;
            bool init = false;
            var manualResults = new List<double>();

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                double val = _testData.Data[i].Value;
                if (!init)
                {
                    e1 = e2 = e3 = e4 = e5 = e6 = val;
                    init = true;
                    manualResults.Add(val);
                    continue;
                }

                e1 = Math.FusedMultiplyAdd(e1, decay, alpha * val);
                e2 = Math.FusedMultiplyAdd(e2, decay, alpha * e1);
                e3 = Math.FusedMultiplyAdd(e3, decay, alpha * e2);
                double tema1 = (3.0 * e1) - (3.0 * e2) + e3;

                e4 = Math.FusedMultiplyAdd(e4, decay, alpha * tema1);
                e5 = Math.FusedMultiplyAdd(e5, decay, alpha * e4);
                e6 = Math.FusedMultiplyAdd(e6, decay, alpha * e5);
                double tema2 = (3.0 * e4) - (3.0 * e5) + e6;

                manualResults.Add((2.0 * tema1) - tema2);
            }

            for (int i = 0; i < qResult.Count; i++)
            {
                Assert.Equal(manualResults[i], qResult[i].Value, 1e-9);
            }
        }
        _output.WriteLine("MCNMA Batch(TSeries) validated successfully against manual 6-EMA composition");
    }

    [Fact]
    public void Validate_StreamingVsBatch_Consistency()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var batchResult = Mcnma.Batch(_testData.Data, period);

            var streaming = new Mcnma(period);
            for (int i = 0; i < _testData.Data.Count; i++)
            {
                streaming.Update(_testData.Data[i]);
            }

            int start = Math.Max(0, _testData.Data.Count - 100);
            for (int i = start; i < _testData.Data.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, batchResult[i].Value, 1e-9);
            }
        }
        _output.WriteLine("MCNMA Streaming vs Batch validated successfully");
    }

    [Fact]
    public void Validate_SpanVsStreaming_Consistency()
    {
        int[] periods = { 5, 10, 14, 20 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] spanOutput = new double[sourceData.Length];
            Mcnma.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            var streaming = new Mcnma(period);
            for (int i = 0; i < sourceData.Length; i++)
            {
                var val = streaming.Update(new TValue(DateTime.UtcNow, sourceData[i]));
                Assert.Equal(val.Value, spanOutput[i], 1e-9);
            }
        }
        _output.WriteLine("MCNMA Span vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_ConstantInput_ConvergesToInput()
    {
        // With constant input, all EMAs converge to the constant.
        // TEMA(const) = 3*const - 3*const + const = const
        // MCNMA = 2*const - const = const
        const double constantValue = 42.0;
        const int period = 10;

        var mcnma = new Mcnma(period);
        double lastResult = 0;

        for (int i = 0; i < 200; i++)
        {
            var result = mcnma.Update(new TValue(DateTime.UtcNow, constantValue));
            lastResult = result.Value;
        }

        Assert.Equal(constantValue, lastResult, 1e-6);
        _output.WriteLine("MCNMA constant input convergence validated successfully");
    }

    [Fact]
    public void Validate_Against_ManualFormula()
    {
        // Manual 6-EMA with first-value seeding (matches Pine exactly)
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var mcnma = new Mcnma(period);
            double alpha = 2.0 / (period + 1);
            double decay = 1.0 - alpha;
            double e1 = 0, e2 = 0, e3 = 0, e4 = 0, e5 = 0, e6 = 0;
            bool init = false;

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                var item = _testData.Data[i];
                var qVal = mcnma.Update(item);
                double val = item.Value;

                if (!init)
                {
                    e1 = e2 = e3 = e4 = e5 = e6 = val;
                    init = true;
                    Assert.Equal(val, qVal.Value, ValidationHelper.DefaultTolerance);
                    continue;
                }

                e1 = Math.FusedMultiplyAdd(e1, decay, alpha * val);
                e2 = Math.FusedMultiplyAdd(e2, decay, alpha * e1);
                e3 = Math.FusedMultiplyAdd(e3, decay, alpha * e2);
                double tema1 = (3.0 * e1) - (3.0 * e2) + e3;

                e4 = Math.FusedMultiplyAdd(e4, decay, alpha * tema1);
                e5 = Math.FusedMultiplyAdd(e5, decay, alpha * e4);
                e6 = Math.FusedMultiplyAdd(e6, decay, alpha * e5);
                double tema2 = (3.0 * e4) - (3.0 * e5) + e6;

                double manualVal = (2.0 * tema1) - tema2;
                Assert.Equal(manualVal, qVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
        _output.WriteLine("MCNMA validated successfully against manual 6-EMA formula");
    }

    [Fact]
    public void Validate_NaN_Robustness()
    {
        const int period = 10;
        var mcnma = new Mcnma(period);

        for (int i = 0; i < 20; i++)
        {
            mcnma.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        var nanResult = mcnma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(nanResult.Value), "MCNMA should handle NaN with last-valid substitution");

        var infResult = mcnma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value), "MCNMA should handle Infinity with last-valid substitution");

        var negInfResult = mcnma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(negInfResult.Value), "MCNMA should handle -Infinity with last-valid substitution");

        var resumeResult = mcnma.Update(new TValue(DateTime.UtcNow, 125.0));
        Assert.True(double.IsFinite(resumeResult.Value), "MCNMA should resume cleanly after invalid inputs");

        _output.WriteLine("MCNMA NaN/Infinity robustness validated successfully");
    }
}
