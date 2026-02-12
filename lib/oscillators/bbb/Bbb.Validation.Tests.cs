using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class BbbValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BbbValidationTests(ITestOutputHelper output)
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
    public void Validate_Streaming_Batch_Span_Agree()
    {
        int period = 20;
        double multiplier = 2.0;

        // Streaming
        var streaming = new Bbb(period, multiplier);
        var streamValues = new List<double>(_testData.Data.Count);
        foreach (var item in _testData.Data)
        {
            streamValues.Add(streaming.Update(item).Value);
        }

        // Batch (TSeries)
        TSeries batchSeries = Bbb.Batch(_testData.Data, period, multiplier);

        // Span
        double[] src = _testData.RawData.ToArray();
        double[] spanOutput = new double[src.Length];
        Bbb.Batch(src.AsSpan(), spanOutput.AsSpan(), period, multiplier);

        // Compare last 200 samples for stability
        int start = Math.Max(0, src.Length - 200);
        for (int i = start; i < src.Length; i++)
        {
            Assert.Equal(batchSeries[i].Value, streamValues[i], 9);
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 9);
        }

        _output.WriteLine("BBB validation: streaming, batch, and span outputs agree.");
    }

    [Fact]
    public void Validate_Skender_PercentB()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            // QuanTAlib
            var bbb = new Bbb(period, multiplier);
            var qResult = bbb.Update(_testData.Data);

            // Skender Bollinger Bands PercentB
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            ValidationHelper.VerifyData(qResult, sResult, s => s.PercentB);
        }

        _output.WriteLine("BBB validated successfully against Skender PercentB.");
    }
}
