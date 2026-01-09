using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class LinRegValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public LinRegValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [SkipLocalsInit]
    [Fact]
    public void Validate_Against_Skender_Slope()
    {
        var period = 14;
        var skender = _data.SkenderQuotes.GetSlope(period).ToList();

        var linreg = new LinReg(period);
        var slopeSeries = new TSeries();
        foreach (var item in _data.Data)
        {
            linreg.Update(item);
            slopeSeries.Add(new TValue(item.Time, linreg.Slope));
        }

        ValidationHelper.VerifyData(slopeSeries, skender, x => x.Slope, tolerance: ValidationHelper.DefaultTolerance);
    }

    [SkipLocalsInit]
    [Fact]
    public void Validate_Against_Skender_RSquared()
    {
        var period = 14;
        var skender = _data.SkenderQuotes.GetSlope(period).ToList();

        var linreg = new LinReg(period);
        var r2Series = new TSeries();
        foreach (var item in _data.Data)
        {
            linreg.Update(item);
            r2Series.Add(new TValue(item.Time, linreg.RSquared));
        }

        ValidationHelper.VerifyData(r2Series, skender, x => x.RSquared, tolerance: ValidationHelper.DefaultTolerance);
    }
}
