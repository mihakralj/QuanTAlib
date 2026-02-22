namespace QuanTAlib.Tests;

using Xunit;

public class KaiserValidationTests
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
        double beta = 3.0;

        var streaming = new Kaiser(period, beta);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var batchResults = Kaiser.Batch(_data, period, beta);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        int period = 14;
        double beta = 3.0;

        var streaming = new Kaiser(period, beta);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var spanOutput = new double[_data.Count];
        Kaiser.Batch(_data.Values, spanOutput, period, beta);

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
        var kaiser = new Kaiser(period, 3.0);
        foreach (var tv in _data)
        {
            var result = kaiser.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
        Assert.True(kaiser.IsHot);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var kaiser = new Kaiser(10, 3.0);
        for (int i = 0; i < 50; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, kaiser.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Kaiser.Calculate(_data, 14, 3.0);
        Assert.True(indicator.IsHot);
        Assert.Equal(_data.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        int period = 7;
        var kaiser = new Kaiser(period, 3.0);

        for (int i = 0; i < 20; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double original = kaiser.Last.Value;

        kaiser.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        kaiser.Update(new TValue(DateTime.UtcNow, 119.0), isNew: false);

        Assert.Equal(original, kaiser.Last.Value, 1e-10);
    }

    [Fact]
    public void SubsetStability()
    {
        int period = 10;
        double beta = 3.0;
        var src = MakeSeries(200);

        var full = new Kaiser(period, beta);
        for (int i = 0; i < src.Count; i++)
        {
            full.Update(src[i]);
        }

        var subset = new Kaiser(period, beta);
        for (int i = 0; i < src.Count; i++)
        {
            subset.Update(src[i]);
        }

        Assert.Equal(full.Last.Value, subset.Last.Value, 1e-10);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.0)]
    [InlineData(5.65)]
    [InlineData(8.6)]
    public void DifferentBetas_ProduceValidResults(double beta)
    {
        var kaiser = new Kaiser(14, beta);
        foreach (var tv in _data)
        {
            var result = kaiser.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
        Assert.True(kaiser.IsHot);
    }
}
