using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class DoscValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ValidationTestData _testData;
    private const int DefaultRsi = 14;
    private const int DefaultEma1 = 5;
    private const int DefaultEma2 = 3;
    private const int DefaultSig = 9;

    public DoscValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData(5000);
    }

    public void Dispose()
    {
        _testData.Dispose();
    }

    // ========== Self-consistency Validation ==========

    [Fact]
    public void Dosc_BatchStreaming_Match()
    {
        var streaming = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        TSeries batchResults = Dosc.Batch(_testData.Data, DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        int mismatchCount = 0;
        double maxDiff = 0;
        for (int i = 0; i < streamResults.Count; i++)
        {
            double diff = Math.Abs(streamResults[i] - batchResults[i].Value);
            if (diff > 1e-10)
            {
                mismatchCount++;
                maxDiff = Math.Max(maxDiff, diff);
            }
        }

        _output.WriteLine($"Dosc({DefaultRsi},{DefaultEma1},{DefaultEma2},{DefaultSig}) Batch vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Dosc_SpanBatch_MatchesStreaming()
    {
        var streaming = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        double[] output = new double[_testData.Data.Count];
        Dosc.Batch(_testData.Data.Values, output, DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        int mismatchCount = 0;
        double maxDiff = 0;
        for (int i = 0; i < streamResults.Count; i++)
        {
            double diff = Math.Abs(streamResults[i] - output[i]);
            if (diff > 1e-10)
            {
                mismatchCount++;
                maxDiff = Math.Max(maxDiff, diff);
            }
        }

        _output.WriteLine($"Dosc({DefaultRsi},{DefaultEma1},{DefaultEma2},{DefaultSig}) Span vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Dosc_DifferentParams_ProduceDifferentResults()
    {
        TSeries result1 = Dosc.Batch(_testData.Data, rsiPeriod: 7, ema1Period: 3, ema2Period: 2, sigPeriod: 5);
        TSeries result2 = Dosc.Batch(_testData.Data, rsiPeriod: 14, ema1Period: 5, ema2Period: 3, sigPeriod: 9);

        int lastIdx = _testData.Data.Count - 1;
        _output.WriteLine($"Dosc(7,3,2,5) last = {result1[lastIdx].Value:F6}");
        _output.WriteLine($"Dosc(14,5,3,9) last = {result2[lastIdx].Value:F6}");

        Assert.NotEqual(result1[lastIdx].Value, result2[lastIdx].Value);
    }

    [Fact]
    public void Dosc_ConstantInput_ConvergesToZero()
    {
        var indicator = new Dosc(rsiPeriod: 5, ema1Period: 3, ema2Period: 2, sigPeriod: 5);
        double constantVal = 100.0;

        double lastResult = double.NaN;
        for (int i = 0; i < 1000; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constantVal));
            lastResult = r.Value;
        }

        _output.WriteLine($"Dosc(5,3,2,5) constant input result after 1000 bars: {lastResult:E6}");
        Assert.True(Math.Abs(lastResult) < 1e-6, $"Expected near-zero for constant input, got {lastResult}");
    }

    [Fact]
    public void Dosc_Calculate_ReturnsHotIndicator()
    {
        (TSeries results, Dosc indicator) = Dosc.Calculate(_testData.Data, DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        Assert.Equal(_testData.Data.Count, results.Count);
        Assert.True(indicator.IsHot);

        TValue next = indicator.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(next.Value));

        _output.WriteLine($"Dosc Calculate: {results.Count} bars, last = {results[results.Count - 1].Value:F6}");
    }

    [Fact]
    public void Dosc_BarCorrection_ProducesConsistentResults()
    {
        var reference = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        for (int i = 0; i < 100; i++)
        {
            reference.Update(_testData.Data[i], isNew: true);
        }
        reference.Update(new TValue(DateTime.UtcNow, 50.0), isNew: true);
        double referenceVal = reference.Last.Value;

        var test = new Dosc(DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);
        for (int i = 0; i < 100; i++)
        {
            test.Update(_testData.Data[i], isNew: true);
        }
        test.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true);  // wrong
        test.Update(new TValue(DateTime.UtcNow, 50.0), isNew: false);  // correct
        double testVal = test.Last.Value;

        _output.WriteLine($"Reference: {referenceVal:F10}, Corrected: {testVal:F10}");
        Assert.Equal(referenceVal, testVal, 1e-10);
    }

    [Fact]
    public void Dosc_SubsetValidation_StableBehavior()
    {
        using var subset = _testData.CreateSubset(200);

        TSeries results = Dosc.Batch(subset.Data, DefaultRsi, DefaultEma1, DefaultEma2, DefaultSig);

        int nanCount = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (!double.IsFinite(results[i].Value))
            {
                nanCount++;
            }
        }

        _output.WriteLine($"Dosc on 200-bar subset: {nanCount} non-finite values");
        Assert.Equal(0, nanCount);
    }
}
