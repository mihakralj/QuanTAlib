using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class ReflexValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ValidationTestData _testData;
    private const int DefaultPeriod = 20;

    public ReflexValidationTests(ITestOutputHelper output)
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
    public void Reflex_BatchStreaming_Match()
    {
        // Streaming
        var streaming = new Reflex(DefaultPeriod);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        // Batch
        TSeries batchResults = Reflex.Batch(_testData.Data, DefaultPeriod);

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

        _output.WriteLine($"Reflex({DefaultPeriod}) Batch vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Reflex_SpanBatch_MatchesStreaming()
    {
        // Streaming
        var streaming = new Reflex(DefaultPeriod);
        var streamResults = new List<double>(_testData.Data.Count);
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            TValue r = streaming.Update(_testData.Data[i], isNew: true);
            streamResults.Add(r.Value);
        }

        // Span batch
        double[] output = new double[_testData.Data.Count];
        Reflex.Batch(_testData.Data.Values, output, DefaultPeriod);

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

        _output.WriteLine($"Reflex({DefaultPeriod}) Span vs Streaming: {mismatchCount} mismatches, max diff = {maxDiff:E3}");
        Assert.Equal(0, mismatchCount);
    }

    [Fact]
    public void Reflex_DifferentPeriods_ProduceDifferentResults()
    {
        TSeries result10 = Reflex.Batch(_testData.Data, 10);
        TSeries result20 = Reflex.Batch(_testData.Data, 20);

        int lastIdx = _testData.Data.Count - 1;
        _output.WriteLine($"Reflex(10) last = {result10[lastIdx].Value:F6}");
        _output.WriteLine($"Reflex(20) last = {result20[lastIdx].Value:F6}");

        Assert.NotEqual(result10[lastIdx].Value, result20[lastIdx].Value);
    }

    [Fact]
    public void Reflex_ConstantInput_ConvergesToZero()
    {
        var indicator = new Reflex(10);
        double constantVal = 100.0;

        double lastResult = double.NaN;
        for (int i = 0; i < 1000; i++)
        {
            TValue r = indicator.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constantVal));
            lastResult = r.Value;
        }

        _output.WriteLine($"Reflex(10) constant input result after 1000 bars: {lastResult:E6}");
        Assert.True(Math.Abs(lastResult) < 1e-6, $"Expected near-zero for constant input, got {lastResult}");
    }

    [Fact]
    public void Reflex_Calculate_ReturnsHotIndicator()
    {
        (TSeries results, Reflex indicator) = Reflex.Calculate(_testData.Data, DefaultPeriod);

        Assert.Equal(_testData.Data.Count, results.Count);
        Assert.True(indicator.IsHot);

        // Verify the indicator can continue streaming
        TValue next = indicator.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(next.Value));

        _output.WriteLine($"Reflex({DefaultPeriod}) Calculate: {results.Count} bars, last = {results[results.Count - 1].Value:F6}");
    }

    [Fact]
    public void Reflex_BarCorrection_ProducesConsistentResults()
    {
        // Build reference: 100 bars then bar 101
        var reference = new Reflex(DefaultPeriod);
        for (int i = 0; i < 100; i++)
        {
            reference.Update(_testData.Data[i], isNew: true);
        }
        reference.Update(new TValue(DateTime.UtcNow, 50.0), isNew: true);
        double referenceVal = reference.Last.Value;

        // Build test: 100 bars, wrong bar 101, then correct bar 101
        var test = new Reflex(DefaultPeriod);
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
    public void Reflex_SubsetValidation_StableBehavior()
    {
        using var subset = _testData.CreateSubset(200);

        TSeries results = Reflex.Batch(subset.Data, DefaultPeriod);

        int nanCount = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (!double.IsFinite(results[i].Value))
            {
                nanCount++;
            }
        }

        _output.WriteLine($"Reflex({DefaultPeriod}) on 200-bar subset: {nanCount} non-finite values");
        Assert.Equal(0, nanCount);
    }
}
