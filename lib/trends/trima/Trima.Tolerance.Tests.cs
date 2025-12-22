using System;
using System.Collections.Generic;
using System.Linq;
using TALib;
using Xunit;

namespace QuanTAlib.Tests;

public class TrimaToleranceTests : IDisposable
{
    private readonly ValidationTestData _testData;

    public TrimaToleranceTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    [Fact]
    public void Check_Talib_Tolerance()
    {
        int period = 20;
        var trima = new Trima(period);
        var qResult = trima.Update(_testData.Data);

        double[] output = new double[_testData.RawData.Length];
        var retCode = TALib.Functions.Trima<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.TrimaLookback(period);

        ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: ValidationHelper.OoplesTolerance);

        // Add explicit assertion to satisfy SonarQube
        Assert.True(qResult.Count > 0);
    }
}
