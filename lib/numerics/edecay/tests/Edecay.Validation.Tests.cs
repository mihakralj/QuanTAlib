using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for EDECAY (Exponential Decay) against the Tulip Indicators algorithm.
/// The Tulip .NET binding does not expose decay/edecay directly, so validation
/// uses manual computation of the Tulip ti_edecay algorithm:
///   output[0] = input[0]
///   output[i] = max(input[i], output[i-1] * (period-1)/period)
/// </summary>
public sealed class EdecayValidationTests(ITestOutputHelper output) : IDisposable
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
    /// Reference implementation of Tulip ti_edecay for validation.
    /// </summary>
    private static double[] TulipEdecay(double[] input, int period)
    {
        double[] output = new double[input.Length];
        double scale = (period - 1.0) / period;
        output[0] = input[0];
        for (int i = 1; i < input.Length; i++)
        {
            double d = output[i - 1] * scale;
            output[i] = input[i] > d ? input[i] : d;
        }
        return output;
    }

    #region Tulip Algorithm Validation

    [Fact]
    public void Edecay_MatchesTulipEdecay_Batch()
    {
        double[] input = _testData.RawData.ToArray();

        var quantResult = Edecay.Batch(_testData.Data, TestPeriod);
        double[] tulipResult = TulipEdecay(input, TestPeriod);

        int count = quantResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(quantResult[i].Value - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantResult[i].Value:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Edecay Batch validated successfully against Tulip edecay algorithm");
    }

    [Fact]
    public void Edecay_MatchesTulipEdecay_Streaming()
    {
        double[] input = _testData.RawData.ToArray();

        var edecay = new Edecay(TestPeriod);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(edecay.Update(item).Value);
        }

        double[] tulipResult = TulipEdecay(input, TestPeriod);

        int count = streamingResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(streamingResults[i] - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={streamingResults[i]:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Edecay Streaming validated successfully against Tulip edecay algorithm");
    }

    [Fact]
    public void Edecay_MatchesTulipEdecay_Span()
    {
        double[] input = _testData.RawData.ToArray();

        var quantOutput = new double[input.Length];
        Edecay.Batch(new ReadOnlySpan<double>(input), quantOutput, TestPeriod);

        double[] tulipResult = TulipEdecay(input, TestPeriod);

        int count = quantOutput.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            Assert.True(
                Math.Abs(quantOutput[i] - tulipResult[i]) <= TulipTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantOutput[i]:G17}, Tulip={tulipResult[i]:G17}");
        }

        _output.WriteLine("Edecay Span validated successfully against Tulip edecay algorithm");
    }

    #endregion

    #region Different Periods

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Edecay_MatchesTulipEdecay_DifferentPeriods(int period)
    {
        double[] input = _testData.RawData.ToArray();

        var quantResult = Edecay.Batch(_testData.Data, period);
        double[] tulipResult = TulipEdecay(input, period);

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
    public void Edecay_HandlesConstantValues()
    {
        var constantData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            constantData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        var result = Edecay.Batch(constantData, TestPeriod);

        // Constant input: output always equals input since input >= decayed
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(100.0, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Edecay_HandlesExponentiallyDecreasing()
    {
        double[] input = new double[20];
        for (int i = 0; i < 20; i++)
        {
            input[i] = 100.0 * Math.Pow(0.9, i);
        }

        var quantOutput = new double[20];
        Edecay.Batch(input, quantOutput, TestPeriod);

        double[] tulipResult = TulipEdecay(input, TestPeriod);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(tulipResult[i], quantOutput[i], TulipTolerance);
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var batchResult = Edecay.Batch(_testData.Data, TestPeriod);

        var edecay = new Edecay(TestPeriod);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(edecay.Update(item).Value);
        }

        int count = _testData.Data.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("Edecay Batch vs Streaming consistency validated");
    }

    [Fact]
    public void Edecay_OutputAlwaysGreaterOrEqualInput()
    {
        double[] input = _testData.RawData.ToArray();
        var quantOutput = new double[input.Length];
        Edecay.Batch(input, quantOutput, TestPeriod);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.True(quantOutput[i] >= input[i] - 1e-15,
                $"Output {quantOutput[i]} must be >= input {input[i]} at index {i}");
        }
    }

    [Fact]
    public void Edecay_DecayIsMultiplicative()
    {
        // With period=5, scale = 4/5 = 0.8
        // After a spike, each subsequent bar without new highs should multiply by 0.8
        double[] input = [100.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        double[] tulipResult = TulipEdecay(input, TestPeriod);

        // output[0] = 100.0
        // output[1] = max(0, 100 * 0.8) = 80.0
        // output[2] = max(0, 80 * 0.8) = 64.0
        // output[3] = max(0, 64 * 0.8) = 51.2
        // output[4] = max(0, 51.2 * 0.8) = 40.96
        // output[5] = max(0, 40.96 * 0.8) = 32.768
        Assert.Equal(100.0, tulipResult[0], TulipTolerance);
        Assert.Equal(80.0, tulipResult[1], TulipTolerance);
        Assert.Equal(64.0, tulipResult[2], TulipTolerance);
        Assert.Equal(51.2, tulipResult[3], TulipTolerance);
        Assert.Equal(40.96, tulipResult[4], TulipTolerance);
        Assert.Equal(32.768, tulipResult[5], TulipTolerance);

        var quantOutput = new double[6];
        Edecay.Batch(input, quantOutput, TestPeriod);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(tulipResult[i], quantOutput[i], TulipTolerance);
        }
    }

    #endregion
}
