using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class SrsiValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ValidationTestData _testData;
    private const int DefaultEmaLength = 6;
    private const int DefaultRsiLength = 14;

    public SrsiValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData(10000);
    }

    public void Dispose()
    {
        _testData.Dispose();
    }

    // ========== Self-consistency Validation ==========

    [Fact]
    public void Srsi_BatchStreaming_Match()
    {
        // Streaming
        var streaming = new Srsi(DefaultEmaLength, DefaultRsiLength);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        // Batch
        TSeries batchResults = Srsi.Batch(_testData.Data, DefaultEmaLength, DefaultRsiLength);

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

        _output.WriteLine($"Srsi({DefaultEmaLength},{DefaultRsiLength}) Batch vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Srsi_SpanBatch_MatchesStreaming()
    {
        // Streaming
        var streaming = new Srsi(DefaultEmaLength, DefaultRsiLength);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        // Span batch
        double[] output = new double[_testData.Data.Count];
        Srsi.Batch(_testData.Data.Values, output, DefaultEmaLength, DefaultRsiLength);

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

        _output.WriteLine($"Srsi({DefaultEmaLength},{DefaultRsiLength}) Span vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Srsi_DifferentParams_ProduceDifferentResults()
    {
        TSeries resultA = Srsi.Batch(_testData.Data, 6, 14);
        TSeries resultB = Srsi.Batch(_testData.Data, 12, 28);

        int lastIdx = _testData.Data.Count - 1;
        _output.WriteLine($"Srsi(6,14) last = {resultA[lastIdx].Value:F6}");
        _output.WriteLine($"Srsi(12,28) last = {resultB[lastIdx].Value:F6}");

        Assert.NotEqual(resultA[lastIdx].Value, resultB[lastIdx].Value);
    }

    [Fact]
    public void Srsi_ConstantInput_ConvergesToFifty()
    {
        var indicator = new Srsi(6, 14);
        double constantVal = 100.0;

        double lastResult = double.NaN;
        for (int i = 0; i < 1000; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constantVal));
            lastResult = r.Value;
        }

        _output.WriteLine($"Srsi(6,14) constant input result after 1000 bars: {lastResult:F6}");
        Assert.True(Math.Abs(lastResult - 50.0) < 1e-6, $"Expected near 50 for constant input, got {lastResult}");
    }

    [Fact]
    public void Srsi_Calculate_ReturnsHotIndicator()
    {
        (TSeries results, Srsi indicator) = Srsi.Calculate(_testData.Data, DefaultEmaLength, DefaultRsiLength);

        Assert.Equal(_testData.Data.Count, results.Count);
        Assert.True(indicator.IsHot);

        // Verify the indicator can continue streaming
        TValue next = indicator.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(next.Value));

        _output.WriteLine($"Srsi({DefaultEmaLength},{DefaultRsiLength}) Calculate: {results.Count} bars, last = {results[results.Count - 1].Value:F6}");
    }

    [Fact]
    public void Srsi_BarCorrection_ProducesConsistentResults()
    {
        // Build reference: 100 bars then bar 101
        var reference = new Srsi(DefaultEmaLength, DefaultRsiLength);
        for (int i = 0; i < 100; i++)
        {
            reference.Update(_testData.Data[i], isNew: true);
        }
        reference.Update(new TValue(DateTime.UtcNow, 50.0), isNew: true);
        double referenceVal = reference.Last.Value;

        // Build test: 100 bars, wrong bar 101, then correct bar 101
        var test = new Srsi(DefaultEmaLength, DefaultRsiLength);
        for (int i = 0; i < 100; i++)
        {
            test.Update(_testData.Data[i], isNew: true);
        }
        test.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true); // wrong
        test.Update(new TValue(DateTime.UtcNow, 50.0), isNew: false); // correct

        double testVal = test.Last.Value;

        _output.WriteLine($"Reference: {referenceVal:F10}, Corrected: {testVal:F10}");
        Assert.Equal(referenceVal, testVal, 1e-10);
    }

    [Fact]
    public void Srsi_SubsetValidation_StableBehavior()
    {
        using var subset = _testData.CreateSubset(200);

        TSeries results = Srsi.Batch(subset.Data, DefaultEmaLength, DefaultRsiLength);

        int nanCount = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (!double.IsFinite(results[i].Value))
            {
                nanCount++;
            }
        }

        _output.WriteLine($"Srsi({DefaultEmaLength},{DefaultRsiLength}) on 200-bar subset: {nanCount} non-finite values");
        Assert.Equal(0, nanCount);
    }
}
