using Xunit;
using TALib;

namespace QuanTAlib.Tests;

public sealed class HtSineValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private bool _disposed;

    public HtSineValidationTests()
    {
        _data = new ValidationTestData(10000);
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
            _data?.Dispose();
        }
    }

    [Fact]
    public void Validate_TaLib()
    {
        // Calculate TA-Lib HtSine
        var input = _data.RawData.Span;
        var outSine = new double[input.Length];
        var outLeadSine = new double[input.Length];
        var retCode = TALib.Functions.HtSine(input, 0..^0, outSine, outLeadSine, out var outRange);

        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // Calculate QuanTAlib HtSine
        var htSine = new HtSine();
        var quantalibResults = htSine.Update(_data.Data);
        var quantLeadSine = new List<double>();

        // Get LeadSine values by re-running
        var htSine2 = new HtSine();
        foreach (var tv in _data.Data)
        {
            htSine2.Update(tv);
            quantLeadSine.Add(htSine2.LeadSine);
        }

        // Compare results - TA-Lib HT_SINE has a lookback of 63
        int outLength = outRange.End.Value - outRange.Start.Value;
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            int talibIdx = i - outRange.Start.Value;
            if (talibIdx >= 0 && talibIdx < outLength)
            {
                double talibSineValue = outSine[talibIdx];
                double talibLeadSineValue = outLeadSine[talibIdx];
                double quantalibSineValue = quantalibResults.Values[i];
                double quantalibLeadSineValue = quantLeadSine[i];
                Assert.Equal(talibSineValue, quantalibSineValue, ValidationHelper.TalibTolerance);
                Assert.Equal(talibLeadSineValue, quantalibLeadSineValue, ValidationHelper.TalibTolerance);
            }
        }
    }

    [Fact]
    public void Validate_TaLib_Streaming()
    {
        // Calculate TA-Lib HtSine
        var input = _data.RawData.Span;
        var outSine = new double[input.Length];
        var outLeadSine = new double[input.Length];
        var retCode = TALib.Functions.HtSine(input, 0..^0, outSine, outLeadSine, out var outRange);

        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // Calculate QuanTAlib HtSine Streaming
        var htSine = new HtSine();
        var streamingSine = new List<double>();
        var streamingLeadSine = new List<double>();

        foreach (var item in _data.Data)
        {
            htSine.Update(item);
            streamingSine.Add(htSine.Last.Value);
            streamingLeadSine.Add(htSine.LeadSine);
        }

        // Compare results
        int outLength = outRange.End.Value - outRange.Start.Value;
        for (int i = streamingSine.Count - 100; i < streamingSine.Count; i++)
        {
            int talibIdx = i - outRange.Start.Value;
            if (talibIdx >= 0 && talibIdx < outLength)
            {
                double talibSineValue = outSine[talibIdx];
                double talibLeadSineValue = outLeadSine[talibIdx];
                double quantalibSineValue = streamingSine[i];
                double quantalibLeadSineValue = streamingLeadSine[i];
                Assert.Equal(talibSineValue, quantalibSineValue, ValidationHelper.TalibTolerance);
                Assert.Equal(talibLeadSineValue, quantalibLeadSineValue, ValidationHelper.TalibTolerance);
            }
        }
    }

    [Fact]
    public void HtSine_Lookback_MatchesTalib()
    {
        int talibLookback = TALib.Functions.HtSineLookback();
        var htSine = new HtSine();

        Assert.Equal(talibLookback, htSine.WarmupPeriod);
    }
}
