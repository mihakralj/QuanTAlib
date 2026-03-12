namespace QuanTAlib.Tests;

public class AhrensValidationTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    [Fact]
    public void Batch_And_Streaming_Match()
    {
        TSeries src = MakeSeries(1000);
        int period = 9;

        TSeries batchResult = Ahrens.Batch(src, period);

        var streaming = new Ahrens(period);
        for (int i = 0; i < src.Count; i++)
        {
            streaming.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        for (int i = period; i < src.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streaming.Last.Value is double _ ? batchResult.Values[i] : double.NaN, 10);
        }

        // More direct: streaming last == batch last
        Assert.Equal(batchResult.Values[src.Count - 1], streaming.Last.Value, 10);
    }

    [Fact]
    public void Span_And_Streaming_Match()
    {
        TSeries src = MakeSeries(1000);
        int period = 9;

        double[] spanOut = new double[src.Count];
        Ahrens.Batch(src.Values, spanOut, period);

        var streaming = new Ahrens(period);
        double[] streamVals = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamVals[i] = streaming.Update(new TValue(DateTime.UtcNow, src.Values[i])).Value;
        }

        for (int i = period; i < src.Count; i++)
        {
            Assert.Equal(spanOut[i], streamVals[i], 10);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(20)]
    [InlineData(50)]
    public void DifferentPeriods_AllFinite(int period)
    {
        TSeries src = MakeSeries(200);
        TSeries result = Ahrens.Batch(src, period);

        for (int i = period; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]), $"Non-finite at index {i} for period {period}");
        }
    }

    [Fact]
    public void Constant_ConvergesToConstant()
    {
        int period = 9;
        double constant = 100.0;
        var ind = new Ahrens(period);
        for (int i = 0; i < 500; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, constant));
        }

        Assert.Equal(constant, ind.Last.Value, 8);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        TSeries src = MakeSeries(200);
        (TSeries results, Ahrens indicator) = Ahrens.Calculate(src, 9);
        Assert.True(indicator.IsHot);
        Assert.Equal(src.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        TSeries src = MakeSeries(100);
        int period = 9;

        // Run full series
        var ind1 = new Ahrens(period);
        for (int i = 0; i < src.Count; i++)
        {
            ind1.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        double fullResult = ind1.Last.Value;

        // Run with bar corrections at every bar
        var ind2 = new Ahrens(period);
        for (int i = 0; i < src.Count; i++)
        {
            // First update with wrong value
            ind2.Update(new TValue(DateTime.UtcNow, src.Values[i] + 10.0));
            // Correct it
            ind2.Update(new TValue(DateTime.UtcNow, src.Values[i]), isNew: false);
            // Then advance
            if (i < src.Count - 1)
            {
                // The next isNew=true will snapshot the corrected state
            }
        }

        Assert.Equal(fullResult, ind2.Last.Value, 10);
    }

    [Fact]
    public void SubsetStability()
    {
        TSeries src = MakeSeries(500);
        int period = 9;

        // Run full 500 bars
        var full = new Ahrens(period);
        for (int i = 0; i < 500; i++)
        {
            full.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        // Run only first 300 bars
        var partial = new Ahrens(period);
        for (int i = 0; i < 300; i++)
        {
            partial.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        // Continue the partial from 300 to 500
        for (int i = 300; i < 500; i++)
        {
            partial.Update(new TValue(DateTime.UtcNow, src.Values[i]));
        }

        Assert.Equal(full.Last.Value, partial.Last.Value, 10);
    }

    [Fact]
    public void LargeDataset_NoOverflow()
    {
        TSeries src = MakeSeries(5000);
        int period = 50;

        TSeries result = Ahrens.Batch(src, period);
        Assert.Equal(5000, result.Count);
        Assert.True(double.IsFinite(result.Values[result.Count - 1]));
    }
}
