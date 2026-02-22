namespace QuanTAlib.Tests;

using Xunit;

public class IlrsValidationTests
{
    private const int DataCount = 5000;
    private readonly TSeries _data;

    public IlrsValidationTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 123);
        _data = gbm.Fetch(DataCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        const int period = 14;
        var batchResult = Ilrs.Batch(_data, period);

        var ilrs = new Ilrs(period);
        for (int i = 0; i < _data.Count; i++)
        {
            ilrs.Update(_data[i]);
            Assert.Equal(batchResult.Values[i], ilrs.Last.Value, 1e-6);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        const int period = 14;
        var spanOutput = new double[_data.Count];
        Ilrs.Batch(_data.Values, spanOutput, period);

        var ilrs = new Ilrs(period);
        for (int i = 0; i < _data.Count; i++)
        {
            double expected = ilrs.Update(_data[i]).Value;
            Assert.Equal(expected, spanOutput[i], 1e-6);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(50)]
    public void DifferentPeriods_ProduceValidResults(int period)
    {
        var ilrs = new Ilrs(period);
        for (int i = 0; i < _data.Count; i++)
        {
            var result = ilrs.Update(_data[i]);
            Assert.True(double.IsFinite(result.Value), $"Non-finite at bar {i}, period {period}");
        }
        Assert.True(ilrs.IsHot);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        const int period = 14;
        const double price = 50.0;
        var ilrs = new Ilrs(period);

        for (int i = 0; i < 200; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, price));
        }

        Assert.Equal(price, ilrs.Last.Value, 1e-6);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Ilrs.Calculate(_data, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(_data.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        const int period = 7;
        var ilrs = new Ilrs(period);

        for (int i = 0; i < 20; i++)
        {
            ilrs.Update(_data[i]);
        }

        var baseline = ilrs.Last.Value;

        // Apply correction then revert
        ilrs.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        Assert.NotEqual(baseline, ilrs.Last.Value);

        ilrs.Update(_data[19], isNew: false);
        Assert.Equal(baseline, ilrs.Last.Value, 1e-6);
    }

    [Fact]
    public void SubsetStability()
    {
        const int period = 14;

        // Run on first 100 bars
        var ilrs1 = new Ilrs(period);
        for (int i = 0; i < 100; i++)
        {
            ilrs1.Update(_data[i]);
        }
        double val100 = ilrs1.Last.Value;

        // Run on first 200 bars, check the output at bar 99 matches
        var ilrs2 = new Ilrs(period);
        double val100_from200 = 0;
        for (int i = 0; i < 200; i++)
        {
            ilrs2.Update(_data[i]);
            if (i == 99)
            {
                val100_from200 = ilrs2.Last.Value;
            }
        }

        Assert.Equal(val100, val100_from200, 1e-9);
    }
}
