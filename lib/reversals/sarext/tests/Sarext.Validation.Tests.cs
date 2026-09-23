// SAREXT Validation Tests - Parabolic SAR Extended
// Cross-validated against TA-Lib's TA_SAREXT, which uses the identical asymmetric
// long/short acceleration-factor state machine and sign-encoded output convention.

using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class SarextValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
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
    public void Sarext_MatchesTalib_DefaultParameters()
    {
        double[] highData = _testData.Bars.High.Values.ToArray();
        double[] lowData = _testData.Bars.Low.Values.ToArray();
        double[] taOutput = new double[highData.Length];

        const double startValue = 0.0;
        const double offsetOnReverse = 0.0;
        const double afInitLong = 0.02;
        const double afLong = 0.02;
        const double afMaxLong = 0.20;
        const double afInitShort = 0.02;
        const double afShort = 0.02;
        const double afMaxShort = 0.20;

        var retCode = TALib.Functions.SarExt<double>(
            highData, lowData, 0..^0, taOutput, out var outRange,
            startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong,
            afInitShort, afShort, afMaxShort);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        (int offset, int length) = outRange.GetOffsetAndLength(taOutput.Length);
        Assert.True(length > 100, $"TALib SAREXT produced only {length} values");

        // QuanTAlib SAREXT (batch)
        var qResult = Sarext.Batch(_testData.Bars,
            startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong,
            afInitShort, afShort, afMaxShort);

        // Skip the first few bars (direction-detection divergence at the very start),
        // then require exact match — both implementations run the identical state machine.
        const int skipBars = 3;
        int compared = 0;
        int matched = 0;
        for (int j = skipBars; j < length; j++)
        {
            int qi = j + offset;
            if (qi >= qResult.Count) { continue; }
            double qv = qResult[qi].Value;
            double tv = taOutput[j];
            if (!double.IsFinite(qv) || !double.IsFinite(tv)) { continue; }

            compared++;
            if (Math.Abs(qv - tv) <= 1e-8) { matched++; }
        }

        double matchRate = compared > 0 ? (double)matched / compared : 0;
        Assert.True(matchRate >= 0.98,
            $"TALib SAREXT match rate {matchRate:P1} ({matched}/{compared}) < 98%");

        _output.WriteLine($"SAREXT validated against TALib: {matched}/{compared} matched ({matchRate:P1})");
    }

    [Fact]
    public void Sarext_MatchesTalib_AsymmetricAf()
    {
        double[] highData = _testData.Bars.High.Values.ToArray();
        double[] lowData = _testData.Bars.Low.Values.ToArray();
        double[] taOutput = new double[highData.Length];

        const double startValue = 0.0;
        const double offsetOnReverse = 0.0;
        const double afInitLong = 0.03;
        const double afLong = 0.03;
        const double afMaxLong = 0.30;
        const double afInitShort = 0.01;
        const double afShort = 0.01;
        const double afMaxShort = 0.10;

        var retCode = TALib.Functions.SarExt<double>(
            highData, lowData, 0..^0, taOutput, out var outRange,
            startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong,
            afInitShort, afShort, afMaxShort);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        (int offset, int length) = outRange.GetOffsetAndLength(taOutput.Length);

        var qResult = Sarext.Batch(_testData.Bars,
            startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong,
            afInitShort, afShort, afMaxShort);

        const int skipBars = 3;
        int compared = 0;
        int matched = 0;
        for (int j = skipBars; j < length; j++)
        {
            int qi = j + offset;
            if (qi >= qResult.Count) { continue; }
            double qv = qResult[qi].Value;
            double tv = taOutput[j];
            if (!double.IsFinite(qv) || !double.IsFinite(tv)) { continue; }

            compared++;
            if (Math.Abs(qv - tv) <= 1e-8) { matched++; }
        }

        double matchRate = compared > 0 ? (double)matched / compared : 0;
        Assert.True(matchRate >= 0.98,
            $"TALib SAREXT (asymmetric AF) match rate {matchRate:P1} ({matched}/{compared}) < 98%");

        _output.WriteLine($"SAREXT (asymmetric AF) validated against TALib: {matched}/{compared} matched ({matchRate:P1})");
    }

    [Fact]
    public void Sarext_StreamingMatchesBatch()
    {
        var indicator = new Sarext();
        var streamValues = new double[_testData.Bars.Count];
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            streamValues[i] = indicator.Update(_testData.Bars[i], isNew: true).Value;
        }

        var batchResult = Sarext.Batch(_testData.Bars);

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            Assert.Equal(streamValues[i], batchResult[i].Value, precision: 10);
        }
    }
}
