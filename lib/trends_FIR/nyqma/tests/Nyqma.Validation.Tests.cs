using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class NyqmaValidationTests : IDisposable
{
    private const int DefaultPeriod = 10;
    private const int DefaultNyquistPeriod = 4;
    private const double ValidationTolerance = 1e-9;
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public NyqmaValidationTests(ITestOutputHelper output)
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
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    // NYQMA has no direct external library equivalent, so we validate
    // by verifying NYQMA = (1+α)·WMA(src,N1) − α·WMA(WMA(src,N1),N2) (component consistency)

    [Fact]
    public void Validate_NyqmaEqualsComponentFormula_Batch()
    {
        int period = DefaultPeriod;
        int nyquistPeriod = DefaultNyquistPeriod;
        double alpha = (double)nyquistPeriod / (period - nyquistPeriod);
        var source = _testData.Data;

        // Compute WMA1 and WMA2(WMA1) separately
        var wma1Result = Wma.Batch(source, period);
        var wma2Result = Wma.Batch(wma1Result, nyquistPeriod);

        // Compute NYQMA
        var nyqmaResult = Nyqma.Batch(source, period, nyquistPeriod);

        // NYQMA = (1+α)·WMA1 − α·WMA2
        int count = nyqmaResult.Count;
        int warmup = period + nyquistPeriod - 1;
        for (int i = warmup; i < count; i++)
        {
            double expected = Math.FusedMultiplyAdd(1.0 + alpha, wma1Result[i].Value, -alpha * wma2Result[i].Value);
            Assert.Equal(expected, nyqmaResult[i].Value, ValidationTolerance);
        }

        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) component consistency: PASS ({count - warmup} bars validated at {ValidationTolerance})");
    }

    [Fact]
    public void Validate_NyqmaEqualsComponentFormula_Streaming()
    {
        int period = DefaultPeriod;
        int nyquistPeriod = DefaultNyquistPeriod;
        double alpha = (double)nyquistPeriod / (period - nyquistPeriod);
        var source = _testData.Data;

        var wma1 = new Wma(period);
        var wma2 = new Wma(nyquistPeriod);
        var nyqma = new Nyqma(period, nyquistPeriod);

        int warmup = period + nyquistPeriod - 1;
        int validated = 0;

        for (int i = 0; i < source.Count; i++)
        {
            var w1Val = wma1.Update(source[i]);
            var w2Val = wma2.Update(w1Val);
            var nyqmaVal = nyqma.Update(source[i]);

            if (i >= warmup)
            {
                double expected = Math.FusedMultiplyAdd(1.0 + alpha, w1Val.Value, -alpha * w2Val.Value);
                Assert.Equal(expected, nyqmaVal.Value, ValidationTolerance);
                validated++;
            }
        }

        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) streaming component consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_NyqmaEqualsComponentFormula_Span()
    {
        int period = DefaultPeriod;
        int nyquistPeriod = DefaultNyquistPeriod;
        double alpha = (double)nyquistPeriod / (period - nyquistPeriod);
        var rawData = _testData.RawData;

        var wma1Output = new double[rawData.Length];
        var wma2Output = new double[rawData.Length];
        var nyqmaOutput = new double[rawData.Length];

        Wma.Batch(rawData.Span, wma1Output.AsSpan(), period);
        Wma.Batch(wma1Output.AsSpan(), wma2Output.AsSpan(), nyquistPeriod);
        Nyqma.Batch(rawData.Span, nyqmaOutput.AsSpan(), period, nyquistPeriod);

        int warmup = period + nyquistPeriod - 1;
        int validated = 0;

        for (int i = warmup; i < rawData.Length; i++)
        {
            double expected = Math.FusedMultiplyAdd(1.0 + alpha, wma1Output[i], -alpha * wma2Output[i]);
            Assert.Equal(expected, nyqmaOutput[i], ValidationTolerance);
            validated++;
        }

        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) span component consistency: PASS ({validated} bars validated)");
    }

    [Fact]
    public void Validate_BatchAndStreamingMatch()
    {
        int period = DefaultPeriod;
        int nyquistPeriod = DefaultNyquistPeriod;
        var source = _testData.Data;

        // Batch
        var batchResult = Nyqma.Batch(source, period, nyquistPeriod);

        // Streaming
        var streaming = new Nyqma(period, nyquistPeriod);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 1e-6);
        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) batch/streaming match: PASS");
    }

    [Fact]
    public void Validate_BatchAndSpanMatch()
    {
        int period = DefaultPeriod;
        int nyquistPeriod = DefaultNyquistPeriod;
        var source = _testData.Data;
        var rawData = _testData.RawData;

        var batchResult = Nyqma.Batch(source, period, nyquistPeriod);

        var spanOutput = new double[rawData.Length];
        Nyqma.Batch(rawData.Span, spanOutput.AsSpan(), period, nyquistPeriod);

        Assert.Equal(batchResult.Last.Value, spanOutput[^1], ValidationTolerance);
        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) batch/span match: PASS");
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(10, 3)]
    [InlineData(21, 8)]
    [InlineData(89, 21)]
    public void Validate_DifferentPeriods_ConstantInputConverges(int period, int nyquistPeriod)
    {
        double constant = 55.0;
        var nyqma = new Nyqma(period, nyquistPeriod);

        double last = 0;
        for (int i = 0; i < period * 3; i++)
        {
            last = nyqma.Update(new TValue(DateTime.UtcNow, constant)).Value;
        }

        Assert.Equal(constant, last, 1e-6);
        _output.WriteLine($"NYQMA({period},{nyquistPeriod}) constant convergence: PASS (converged to {last:F9})");
    }

    [Fact]
    public void Validate_NyquistClamping_AlphaBoundsCorrect()
    {
        // When nyquistPeriod is at maximum (period/2), alpha = (N1/2)/(N1 - N1/2) = 1
        var nyqma = new Nyqma(10, 5);
        // alpha = 5/(10-5) = 1.0
        // NYQMA = 2*WMA1 - WMA2 (same as PMA when nyquistPeriod = period)

        double constant = 100.0;
        double last = 0;
        for (int i = 0; i < 50; i++)
        {
            last = nyqma.Update(new TValue(DateTime.UtcNow, constant)).Value;
        }

        Assert.Equal(constant, last, 1e-9);
        _output.WriteLine("NYQMA(10,5) alpha=1.0 convergence: PASS");
    }
}
