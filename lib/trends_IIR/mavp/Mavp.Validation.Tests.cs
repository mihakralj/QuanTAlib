using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for MAVP.
/// MAVP with a fixed period should produce identical results to EMA with the same period.
/// Cross-validated against Skender EMA and TA-Lib EMA when period is constant.
/// </summary>
public sealed class MavpValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MavpValidationTests(ITestOutputHelper output)
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
    public void Validate_FixedPeriod_MatchesEma_Batch()
    {
        int[] periods = { 10, 14, 20 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (batch)
            var ema = new Ema(period);
            var emaResult = ema.Update(_testData.Data);

            // Calculate QuanTAlib MAVP with fixed period (batch)
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var mavpResult = mavp.Update(_testData.Data);

            // Compare: MAVP with fixed period == EMA with same period
            // Tolerance 1e-7: both use compensated EMA but FMA operation
            // ordering causes sub-ULP differences over 5000 bars
            Assert.Equal(emaResult.Count, mavpResult.Count);
            for (int i = 0; i < emaResult.Count; i++)
            {
                Assert.Equal(emaResult[i].Value, mavpResult[i].Value, 1e-7);
            }
        }
        _output.WriteLine("MAVP fixed-period validated successfully against EMA");
    }

    [Fact]
    public void Validate_FixedPeriod_MatchesEma_Streaming()
    {
        int[] periods = { 10, 14, 20 };

        foreach (var period in periods)
        {
            var ema = new Ema(period);
            var mavp = new Mavp(2, 50);
            mavp.Period = period;

            var emaResults = new List<double>();
            var mavpResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                emaResults.Add(ema.Update(item).Value);
                mavpResults.Add(mavp.Update(item).Value);
            }

            Assert.Equal(emaResults.Count, mavpResults.Count);
            for (int i = 0; i < emaResults.Count; i++)
            {
                Assert.Equal(emaResults[i], mavpResults[i], 1e-9);
            }
        }
        _output.WriteLine("MAVP fixed-period Streaming validated successfully against EMA");
    }

    [Fact]
    public void Validate_FixedPeriod_SpanMatchesStreaming()
    {
        // MAVP Span uses compensated EMA; EMA Span uses CalculateCleanCore (seeded,
        // no compensation) for large NaN-free datasets. Comparing MAVP Span against
        // its own streaming output validates cross-mode consistency instead.
        int[] periods = { 10, 14, 20 };

        foreach (var period in periods)
        {
            // MAVP streaming reference
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var streamResults = new double[_testData.RawData.Length];
            for (int i = 0; i < _testData.RawData.Length; i++)
            {
                streamResults[i] = mavp.Update(
                    new TValue(DateTime.UtcNow, _testData.RawData.Span[i])).Value;
            }

            // MAVP span with fixed period
            double[] mavpOutput = new double[_testData.RawData.Length];
            Mavp.Batch(_testData.RawData.Span, mavpOutput.AsSpan(), period, 2, 50);

            for (int i = 0; i < streamResults.Length; i++)
            {
                Assert.Equal(streamResults[i], mavpOutput[i], 1e-9);
            }
        }
        _output.WriteLine("MAVP fixed-period Span validated against Streaming (cross-mode consistency)");
    }

    [Fact]
    public void Validate_Skender_Ema_Batch()
    {
        int[] periods = { 10, 14, 20 };

        foreach (var period in periods)
        {
            // QuanTAlib MAVP with fixed period
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var qResult = mavp.Update(_testData.Data);

            // Skender EMA (same as MAVP with fixed period)
            var sResult = Skender.Stock.Indicators.Indicator
                .GetEma(_testData.SkenderQuotes, period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Ema);
        }
        _output.WriteLine("MAVP Batch validated successfully against Skender EMA");
    }

    [Fact]
    public void Validate_Skender_Ema_Streaming()
    {
        int[] periods = { 10, 14, 20 };

        foreach (var period in periods)
        {
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(mavp.Update(item).Value);
            }

            var sResult = Skender.Stock.Indicators.Indicator
                .GetEma(_testData.SkenderQuotes, period).ToList();

            ValidationHelper.VerifyData(qResults, sResult, x => x.Ema);
        }
        _output.WriteLine("MAVP Streaming validated successfully against Skender EMA");
    }

    [Fact]
    public void Validate_Talib_Ema_Batch()
    {
        int[] periods = { 10, 14, 20 };

        double[] cData = _testData.Data.Select(x => x.Value).ToArray();
        double[] output = new double[cData.Length];

        foreach (var period in periods)
        {
            // QuanTAlib MAVP with fixed period
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var qResult = mavp.Update(_testData.Data);

            // TA-Lib EMA
            var retCode = TALib.Functions.Ema(cData, 0..^0, output, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.EmaLookback(period);

            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("MAVP Batch validated successfully against TA-Lib EMA");
    }

    [Fact]
    public void Validate_Talib_Ema_Streaming()
    {
        int[] periods = { 10, 14, 20 };

        double[] cData = _testData.Data.Select(x => x.Value).ToArray();
        double[] output = new double[cData.Length];

        foreach (var period in periods)
        {
            var mavp = new Mavp(2, 50);
            mavp.Period = period;
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(mavp.Update(item).Value);
            }

            var retCode = TALib.Functions.Ema(cData, 0..^0, output, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.EmaLookback(period);

            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("MAVP Streaming validated successfully against TA-Lib EMA");
    }
}
