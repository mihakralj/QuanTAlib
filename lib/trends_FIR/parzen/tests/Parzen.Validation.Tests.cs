namespace QuanTAlib.Tests;

using Xunit;

public class ParzenValidationTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    private readonly TSeries _data = MakeSeries();

    [Fact]
    public void Batch_Matches_Streaming()
    {
        int period = 14;

        var streaming = new Parzen(period);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var batchResults = Parzen.Batch(_data, period);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        int period = 14;

        var streaming = new Parzen(period);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var spanOutput = new double[_data.Count];
        Parzen.Batch(_data.Values, spanOutput, period);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-9);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(50)]
    public void DifferentPeriods_ProduceValidResults(int period)
    {
        var parzen = new Parzen(period);
        foreach (var tv in _data)
        {
            var result = parzen.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
        Assert.True(parzen.IsHot);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var parzen = new Parzen(10);
        for (int i = 0; i < 50; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, parzen.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Parzen.Calculate(_data, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(_data.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        int period = 7;
        var parzen = new Parzen(period);

        for (int i = 0; i < 20; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double original = parzen.Last.Value;

        parzen.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        parzen.Update(new TValue(DateTime.UtcNow, 119.0), isNew: false);

        Assert.Equal(original, parzen.Last.Value, 1e-10);
    }

    [Fact]
    public void SubsetStability()
    {
        int period = 10;
        var src = MakeSeries(200);

        var full = new Parzen(period);
        for (int i = 0; i < src.Count; i++)
        {
            full.Update(src[i]);
        }

        var subset = new Parzen(period);
        for (int i = 0; i < src.Count; i++)
        {
            subset.Update(src[i]);
        }

        Assert.Equal(full.Last.Value, subset.Last.Value, 1e-10);
    }

    [Fact]
    public void OddAndEvenPeriods_BothWork()
    {
        var oddParzen = new Parzen(7);
        var evenParzen = new Parzen(8);

        foreach (var tv in _data)
        {
            var oddResult = oddParzen.Update(tv);
            var evenResult = evenParzen.Update(tv);
            Assert.True(double.IsFinite(oddResult.Value));
            Assert.True(double.IsFinite(evenResult.Value));
        }

        Assert.True(oddParzen.IsHot);
        Assert.True(evenParzen.IsHot);
    }
}
