using System;
using System.Collections.Generic;
using QuanTAlib;
using TALib;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class HtDcphaseValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private bool _disposed;

    public HtDcphaseValidationTests()
    {
        _data = new ValidationTestData(5000);
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
    public void Validate_TaLib_Static()
    {
        var input = _data.RawData.Span;
        var outPhase = new double[input.Length];
        var rc = TALib.Functions.HtDcPhase(input, 0..^0, outPhase, out var outRange);

        Assert.Equal(Core.RetCode.Success, rc);

        var q = new HtDcphase();
        var qSeries = q.Update(_data.Data);

        int outLength = outRange.End.Value - outRange.Start.Value;
        for (int i = qSeries.Count - 200; i < qSeries.Count; i++)
        {
            int talibIdx = i - outRange.Start.Value;
            if (talibIdx >= 0 && talibIdx < outLength)
            {
                Assert.Equal(outPhase[talibIdx], qSeries.Values[i], ValidationHelper.TalibTolerance);
            }
        }
    }

    [Fact]
    public void Validate_TaLib_Streaming()
    {
        var input = _data.RawData.Span;
        var outPhase = new double[input.Length];
        var rc = TALib.Functions.HtDcPhase(input, 0..^0, outPhase, out var outRange);

        Assert.Equal(Core.RetCode.Success, rc);

        var streaming = new List<double>(_data.Data.Count);
        var q = new HtDcphase();
        foreach (var tv in _data.Data)
        {
            streaming.Add(q.Update(tv).Value);
        }

        int outLength = outRange.End.Value - outRange.Start.Value;
        for (int i = streaming.Count - 200; i < streaming.Count; i++)
        {
            int talibIdx = i - outRange.Start.Value;
            if (talibIdx >= 0 && talibIdx < outLength)
            {
                Assert.Equal(outPhase[talibIdx], streaming[i], ValidationHelper.TalibTolerance);
            }
        }
    }

    [Fact]
    public void Lookback_MatchesTaLib()
    {
        int talibLookback = TALib.Functions.HtDcPhaseLookback();
        var q = new HtDcphase();
        Assert.Equal(talibLookback, q.WarmupPeriod);
    }
}
