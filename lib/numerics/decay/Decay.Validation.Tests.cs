using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for DECAY (Linear Decay) against the Tulip Indicators algorithm.
/// The Tulip .NET binding does not expose decay/edecay directly, so validation
/// uses manual computation of the Tulip ti_decay algorithm:
///   output[0] = input[0]
///   output[i] = max(input[i], output[i-1] - 1.0/period)
/// </summary>
public sealed class DecayValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 5;
    private const double TulipTolerance = 1e-9;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    /// <summary>
    /// Reference implementation of Tulip ti_decay for validation.
    /// </summary>
    private static double[] TulipDecay(double[] input, int period)
    {
        double[] output = new double[input.Length];
        double scale = 1.0 / period;
        output[0] = input[0];
        for (int i = 1; i < input.Length; i++)
        {
            double d = output[i - 1] - scale;
            output[i] = input[i] > d ? input[i] : d;
        }
        return output;
    }

    #region Tulip Algorithm Validation

    [Fact]
    public void Decay_MatchesTulipDecay_Batch()
    {
        double[] input = _testData.RawData.ToArray();

        var quantResult = Decay.Batch(_testData.Data, TestPeriod);
        double[] tulipResult = TulipDecay(input, TestPeriod);

        int count = quantResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(quantResult[i].Value - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantResult[i].Value:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Decay Batch validated successfully against Tulip decay algorithm");
    }

    [Fact]
    public void Decay_MatchesTulipDecay_Streaming()
    {
        double[] input = _testData.RawData.ToArray();

        var decay = new Decay(TestPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(decay.Update(item).Value);
        }

        double[] tulipResult = TulipDecay(input, TestPeriod);

        int count = streamingResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(streamingResults[i] - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={streamingResults[i]:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Decay Streaming validated successfully against Tulip decay algorithm");
    }

    [Fact]
    public void Decay_MatchesTulipDecay_Span()
    {
        double[] input = _testData.RawData.ToArray();

        var quantOutput = new double[input.Length];
        Decay.Batch(new ReadOnlySpan<double>(input), quantOutput, TestPeriod);

        double[] tulipResult = TulipDecay(input, TestPeriod);

        int count = quantOutput.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(quantOutput[i] - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantOutput[i]:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Decay Span validated successfully against Tulip decay algorithm");
    }

    #endregion

    #region Different Periods

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Decay_MatchesTulipDecay_DifferentPeriods(int period)
    {
        double[] input = _testData.RawData.ToArray();

        var quantResult = Decay.Batch(_testData.Data, period);
        double[] tulipResult = TulipDecay(input, period);

        int count = quantResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(quantResult[i].Value - tulipResult[i]) <= TulipTolerance,
                $"Period={period}, Mismatch at index {i}: QuanTAlib={quantResult[i].Value:G17}, Tulip={tulipResult[i]:G17}");
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Decay_HandlesConstantValues()
    {
        var constantData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            constantData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        var result = Decay.Batch(constantData, TestPeriod);

        // Constant input: output always equals input since input >= decayed
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(100.0, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Decay_HandlesLinearlyDecreasing()
    {
        double[] input = new double[20];
        for (int i = 0; i < 20; i++)
        {
            input[i] = 100.0 - i;
        }

        var quantOutput = new double[20];
        Decay.Batch(input, quantOutput, TestPeriod);

        double[] tulipResult = TulipDecay(input, TestPeriod);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(tulipResult[i], quantOutput[i], TulipTolerance);
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var batchResult = Decay.Batch(_testData.Data, TestPeriod);

        var decay = new Decay(TestPeriod);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(decay.Update(item).Value);
        }

        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("Decay Batch vs Streaming consistency validated");
    }

    [Fact]
    public void Decay_OutputAlwaysGreaterOrEqualInput()
    {
        double[] input = _testData.RawData.ToArray();
        var quantOutput = new double[input.Length];
        Decay.Batch(input, quantOutput, TestPeriod);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.True(quantOutput[i] >= input[i] - 1e-15,
                $"Output {quantOutput[i]} must be >= input {input[i]} at index {i}");
        }
    }

    #endregion
}
