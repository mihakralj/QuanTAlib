using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class SmaToleranceTests : IDisposable
{
    private readonly ValidationTestData _testData;

    public SmaToleranceTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        _testData.Dispose();
    }

    [Fact]
    public void Check_Skender_Tolerance()
    {
        int period = 20;
        var sma = new Sma(period);
        var qResult = sma.Update(_testData.Data);
        var sResult = _testData.SkenderQuotes.GetSma(period).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Sma);

        // Add explicit assertion to satisfy SonarQube
        Assert.True(qResult.Count > 0);
    }
}
