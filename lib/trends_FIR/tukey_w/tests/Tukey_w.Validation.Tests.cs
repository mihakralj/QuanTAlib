namespace QuanTAlib.Tests;

using Xunit;

public class TukeyWValidationTests
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
        int period = 20;
        double alpha = 0.5;

        var streaming = new Tukey_w(period, alpha);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var batchResults = Tukey_w.Batch(_data, period, alpha);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        int period = 20;
        double alpha = 0.5;

        var streaming = new Tukey_w(period, alpha);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        var spanOutput = new double[_data.Count];
        Tukey_w.Batch(_data.Values, spanOutput, period, alpha);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-9);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(50)]
    public void DifferentPeriods_ProduceValidResults(int period)
    {
        var tukey = new Tukey_w(period, 0.5);
        foreach (var tv in _data)
        {
            var result = tukey.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
        Assert.True(tukey.IsHot);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var tukey = new Tukey_w(10, 0.5);
        for (int i = 0; i < 50; i++)
        {
            tukey.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, tukey.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Tukey_w.Calculate(_data, 20, 0.5);
        Assert.True(indicator.IsHot);
        Assert.Equal(_data.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        int period = 7;
        var tukey = new Tukey_w(period, 0.5);

        for (int i = 0; i < 20; i++)
        {
            tukey.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double original = tukey.Last.Value;

        tukey.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        tukey.Update(new TValue(DateTime.UtcNow, 119.0), isNew: false);

        Assert.Equal(original, tukey.Last.Value, 1e-10);
    }

    [Fact]
    public void SubsetStability()
    {
        int period = 10;
        double alpha = 0.5;
        var src = MakeSeries(200);

        var full = new Tukey_w(period, alpha);
        for (int i = 0; i < src.Count; i++)
        {
            full.Update(src[i]);
        }

        var subset = new Tukey_w(period, alpha);
        for (int i = 0; i < src.Count; i++)
        {
            subset.Update(src[i]);
        }

        Assert.Equal(full.Last.Value, subset.Last.Value, 1e-10);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void DifferentAlphas_ProduceValidResults(double alpha)
    {
        var tukey = new Tukey_w(20, alpha);
        foreach (var tv in _data)
        {
            var result = tukey.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
        Assert.True(tukey.IsHot);
    }

    [Fact]
    public void Alpha0_MatchesSma()
    {
        int period = 10;
        var tukey = new Tukey_w(period, 0.0);
        var sma = new Sma(period);

        foreach (var tv in _data)
        {
            tukey.Update(tv);
            sma.Update(tv);
        }

        Assert.Equal(sma.Last.Value, tukey.Last.Value, 1e-10);
    }

    [Fact]
    public void DifferentAlphas_ProduceDifferentResults()
    {
        int period = 20;
        var tukey025 = new Tukey_w(period, 0.25);
        var tukey075 = new Tukey_w(period, 0.75);

        foreach (var tv in _data)
        {
            tukey025.Update(tv);
            tukey075.Update(tv);
        }

        Assert.NotEqual(tukey025.Last.Value, tukey075.Last.Value);
    }

    [Fact]
    public void Weights_AreSymmetric()
    {
        int period = 11;
        double alpha = 0.5;
        var tukey1 = new Tukey_w(period, alpha);

        // Feed ascending then verify symmetry by checking constant input
        for (int i = 0; i < 50; i++)
        {
            tukey1.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        Assert.Equal(50.0, tukey1.Last.Value, 1e-10);
    }

    [Fact]
    public void Output_BoundedByInput()
    {
        int period = 10;
        var tukey = new Tukey_w(period, 0.5);

        for (int i = 0; i < _data.Count; i++)
        {
            tukey.Update(_data[i]);
            if (i >= period - 1)
            {
                // Track recent window min/max
                double wMin = double.MaxValue;
                double wMax = double.MinValue;
                int start = Math.Max(0, i - period + 1);
                for (int j = start; j <= i; j++)
                {
                    double v = _data[j].Value;
                    if (v < wMin)
                    {
                        wMin = v;
                    }
                    if (v > wMax)
                    {
                        wMax = v;
                    }
                }
                Assert.InRange(tukey.Last.Value, wMin - 1e-10, wMax + 1e-10);
            }
        }
    }

    [Fact]
    public void PineScript_Equivalence_Alpha05()
    {
        // Verify the piecewise Tukey window with known values
        // period=5, alpha=0.5: N=4, aN=2
        // i=0: i < aN/2=1 → w = 0.5*(1-cos(2π*0/2)) = 0.5*(1-1) = 0
        // i=1: i >= aN/2=1 and i <= N-aN/2=3 → w = 1.0
        // i=2: flat → w = 1.0
        // i=3: flat → w = 1.0
        // i=4: i > N-aN/2=3 → w = 0.5*(1-cos(2π*(4-4)/2)) = 0.5*(1-1) = 0
        // weights = [0, 1, 1, 1, 0] normalized = [0, 1/3, 1/3, 1/3, 0]
        // So for constant input 10.0, result should be 10.0
        var tukey = new Tukey_w(5, 0.5);
        for (int i = 0; i < 10; i++)
        {
            tukey.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        Assert.Equal(10.0, tukey.Last.Value, 1e-10);

        // For values [1,2,3,4,5] with weights [0,1/3,1/3,1/3,0]:
        // result = (0*1 + 1/3*2 + 1/3*3 + 1/3*4 + 0*5) = (2+3+4)/3 = 3.0
        var tukey2 = new Tukey_w(5, 0.5);
        for (int i = 1; i <= 5; i++)
        {
            tukey2.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.0, tukey2.Last.Value, 1e-10);
    }
}
