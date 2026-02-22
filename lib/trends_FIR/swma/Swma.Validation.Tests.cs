namespace QuanTAlib.Tests;

public class SwmaValidationTests
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

    // === Self-consistency: Batch vs Streaming vs Span ===

    [Fact]
    public void Batch_Matches_Streaming()
    {
        var src = MakeSeries(500);
        int period = 6;

        var batchResult = Swma.Batch(src, period);

        var streaming = new Swma(period);
        var streamResults = new TSeries();
        for (int i = 0; i < src.Count; i++)
        {
            streamResults.Add(streaming.Update(src[i]));
        }

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        var src = MakeSeries(500);
        int period = 8;

        var streaming = new Swma(period);
        var streamResults = new List<double>();
        for (int i = 0; i < src.Count; i++)
        {
            streamResults.Add(streaming.Update(src[i]).Value);
        }

        var spanOutput = new double[src.Count];
        Swma.Batch(src.Values, spanOutput, period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Calculate_Matches_Batch()
    {
        var src = MakeSeries(300);
        int period = 5;

        var batchResult = Swma.Batch(src, period);
        var (calcResult, _) = Swma.Calculate(src, period);

        Assert.Equal(batchResult.Count, calcResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, calcResult[i].Value, 1e-10);
        }
    }

    // === Mathematical properties ===

    [Fact]
    public void ConstantInput_ReturnsConstant_AllPeriods()
    {
        double constant = 42.0;
        int[] periods = { 2, 3, 4, 5, 10, 20 };

        foreach (int period in periods)
        {
            var swma = new Swma(period);
            for (int i = 0; i < period + 5; i++)
            {
                swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constant));
            }
            Assert.Equal(constant, swma.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void OutputBounded_ByInputRange()
    {
        var src = MakeSeries(500);
        int period = 10;

        var result = Swma.Batch(src, period);

        // After warmup, output should be bounded by local window min/max
        for (int i = period - 1; i < src.Count; i++)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int j = i - period + 1; j <= i; j++)
            {
                double v = src[j].Value;
                if (v < min) { min = v; }
                if (v > max) { max = v; }
            }
            Assert.InRange(result[i].Value, min - 1e-10, max + 1e-10);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(11)]
    public void SymmetricWeights_SymmetricInput_ProducesCenter(int period)
    {
        // For symmetric weights and linearly increasing input fully filling the window,
        // the weighted average equals the center value
        var swma = new Swma(period);

        for (int i = 0; i < period; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)(i + 1)));
        }

        // Linear input [1..period]: center = (period+1)/2.0
        double expectedCenter = (period + 1) / 2.0;
        Assert.Equal(expectedCenter, swma.Last.Value, 1e-10);
    }

    [Fact]
    public void Period4_PineScript_Equivalence()
    {
        // PineScript ta.swma: weights [1, 2, 2, 1] / 6
        var swma = new Swma(period: 4);
        double[] values = { 100, 102, 98, 104, 106, 103, 101, 105 };
        var results = new List<double>();

        for (int i = 0; i < values.Length; i++)
        {
            results.Add(swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i])).Value);
        }

        // Manual Pine calculation for bar 3 (index 3): (1*100 + 2*102 + 2*98 + 1*104)/6
        double expected3 = (100.0 + 204.0 + 196.0 + 104.0) / 6.0;
        Assert.Equal(expected3, results[3], 1e-10);

        // bar 4: (1*102 + 2*98 + 2*104 + 1*106)/6
        double expected4 = (102.0 + 196.0 + 208.0 + 106.0) / 6.0;
        Assert.Equal(expected4, results[4], 1e-10);
    }

    // === Stress and edge cases ===

    [Fact]
    public void LargePeriod_Handles()
    {
        int period = 200;
        var src = MakeSeries(500);
        var result = Swma.Batch(src, period);

        Assert.Equal(500, result.Count);
        Assert.True(double.IsFinite(result[^1].Value));
    }

    [Fact]
    public void AllNaN_Input_ReturnsNaN()
    {
        double[] source = new double[10];
        Array.Fill(source, double.NaN);
        double[] output = new double[10];

        Swma.Batch(source, output, period: 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsNaN(output[i]));
        }
    }

    [Fact]
    public void MixedNaN_Recovers()
    {
        var swma = new Swma(period: 3);
        swma.Update(new TValue(DateTime.UtcNow, 10.0));
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 30.0));

        // Now NaN
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(3), double.NaN));
        Assert.True(double.IsFinite(swma.Last.Value));

        // Recover with valid value
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 40.0));
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var src = MakeSeries(100);

        var r4 = Swma.Batch(src, 4);
        var r8 = Swma.Batch(src, 8);

        // After both are hot, results should differ
        bool anyDifferent = false;
        for (int i = 20; i < src.Count; i++)
        {
            if (Math.Abs(r4[i].Value - r8[i].Value) > 1e-6)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void BarCorrection_ProducesSameAsNewSequence()
    {
        var src = MakeSeries(50);
        int period = 5;

        // Path 1: All new bars
        var swma1 = new Swma(period);
        for (int i = 0; i < src.Count; i++)
        {
            swma1.Update(src[i], isNew: true);
        }

        // Path 2: Bar correction on last bar
        var swma2 = new Swma(period);
        for (int i = 0; i < src.Count - 1; i++)
        {
            swma2.Update(src[i], isNew: true);
        }
        // Simulate tick corrections then final new bar
        swma2.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true);
        swma2.Update(src[^1], isNew: false); // Correct last

        // The correction path rewrites the last value
        Assert.Equal(swma1.Last.Value, swma2.Last.Value, 1e-10);
    }
}
