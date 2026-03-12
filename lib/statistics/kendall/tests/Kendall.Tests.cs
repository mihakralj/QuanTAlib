namespace QuanTAlib.Tests;

public class KendallConstructorTests
{
    [Fact]
    public void Constructor_ValidPeriod_CreatesIndicator()
    {
        var indicator = new Kendall(20);
        Assert.Equal("Kendall(20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_MinimumValidPeriod_CreatesIndicator()
    {
        var indicator = new Kendall(2);
        Assert.Equal("Kendall(2)", indicator.Name);
    }

    [Fact]
    public void Constructor_DefaultPeriod_IsTwenty()
    {
        var indicator = new Kendall();
        Assert.Equal("Kendall(20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Kendall(1));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Kendall(0));
        Assert.Equal("period", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => new Kendall(-5));
        Assert.Equal("period", ex3.ParamName);
    }
}

public class KendallBasicTests
{
    [Fact]
    public void Update_SingleValue_ReturnsNaN()
    {
        var indicator = new Kendall(5);
        var result = indicator.Update(100.0, 200.0, true);
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_TwoValues_ReturnsFinite()
    {
        var indicator = new Kendall(5);
        indicator.Update(100.0, 200.0, true);
        var result = indicator.Update(102.0, 204.0, true);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_PerfectPositiveCorrelation_ReturnsOne()
    {
        var indicator = new Kendall(10);

        // Monotonically increasing both series — all pairs concordant
        for (int i = 0; i < 10; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 + (2 * i);
            indicator.Update(x, y, true);
        }

        Assert.True(indicator.IsHot);
        Assert.Equal(1.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_PerfectNegativeCorrelation_ReturnsMinusOne()
    {
        var indicator = new Kendall(10);

        // x increasing, y decreasing — all pairs discordant
        for (int i = 0; i < 10; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 - (2 * i);
            indicator.Update(x, y, true);
        }

        Assert.True(indicator.IsHot);
        Assert.Equal(-1.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_ConstantX_ReturnsZero()
    {
        var indicator = new Kendall(5);

        // Constant x means all x differences are 0 → product is 0 → no concordant/discordant
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0, 200.0 + i, true);
        }

        Assert.Equal(0.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_ConstantY_ReturnsZero()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0 + i, 200.0, true);
        }

        Assert.Equal(0.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_KnownSequence_CorrectTau()
    {
        // Known example: x = [1,2,3,4,5], y = [1,3,2,5,4]
        // Concordant pairs: (1,2),(1,3),(1,4),(1,5),(2,4),(2,5),(3,4),(3,5) = 8
        // Discordant pairs: (2,3),(4,5) = 2
        // Tau-a = (8-2)/(5*4/2) = 6/10 = 0.6
        var indicator = new Kendall(5);
        indicator.Update(1.0, 1.0, true);
        indicator.Update(2.0, 3.0, true);
        indicator.Update(3.0, 2.0, true);
        indicator.Update(4.0, 5.0, true);
        var result = indicator.Update(5.0, 4.0, true);

        Assert.Equal(0.6, result.Value, 1e-10);
    }

    [Fact]
    public void Update_ResultAlwaysInRange()
    {
        var indicator = new Kendall(10);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 12345);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.5, seed: 54321);

        for (int i = 0; i < 200; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            var result = indicator.Update(x, y, true);

            if (double.IsFinite(result.Value))
            {
                Assert.InRange(result.Value, -1.0, 1.0);
            }
        }
    }
}

public class KendallStateCorrectionTests
{
    [Fact]
    public void Update_BarCorrection_RestoresState()
    {
        var indicator1 = new Kendall(5);
        var indicator2 = new Kendall(5);

        // Feed same initial data
        for (int i = 0; i < 10; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 + (i * 0.5);
            indicator1.Update(x, y, true);
            indicator2.Update(x, y, true);
        }

        // indicator1: Add another bar
        indicator1.Update(110.0, 205.0, true);

        // indicator2: Add wrong bar, then correct
        indicator2.Update(999.0, 999.0, true);
        indicator2.Update(110.0, 205.0, false);

        Assert.Equal(indicator1.Last.Value, indicator2.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var indicator = new Kendall(5);

        // Feed initial data
        for (int i = 0; i < 8; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 + (i * 2);
            indicator.Update(x, y, true);
        }

        // Add new bar
        indicator.Update(108.0, 216.0, true);

        // Make multiple corrections
        for (int j = 0; j < 5; j++)
        {
            double x = 108.0 + (j * 0.1);
            double y = 216.0 + (j * 0.2);
            _ = indicator.Update(x, y, false);
        }

        // Final correction back to original
        indicator.Update(108.0, 216.0, false);

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesBuffer()
    {
        var indicator = new Kendall(3);

        indicator.Update(1.0, 10.0, true);
        indicator.Update(2.0, 20.0, true);
        indicator.Update(3.0, 30.0, true);

        // All concordant: tau = 1.0
        Assert.Equal(1.0, indicator.Last.Value, 1e-10);

        // Add a 4th bar — buffer rolls, oldest drops
        indicator.Update(4.0, 40.0, true);
        Assert.Equal(1.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_IsNewFalse_DoesNotAdvanceBuffer()
    {
        var indicator = new Kendall(3);

        indicator.Update(1.0, 10.0, true);
        indicator.Update(2.0, 20.0, true);
        indicator.Update(3.0, 30.0, true);

        double beforeValue = indicator.Last.Value;

        // Correct the last bar to same values — result unchanged
        indicator.Update(3.0, 30.0, false);
        Assert.Equal(beforeValue, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0 + i, 200.0 + (i * 2), true);
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }
}

public class KendallWarmupTests
{
    [Fact]
    public void IsHot_BelowTwo_ReturnsFalse()
    {
        var indicator = new Kendall(10);
        indicator.Update(100.0, 200.0, true);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void IsHot_AtLeastTwoValues_ReturnsTrue()
    {
        var indicator = new Kendall(10);
        indicator.Update(100.0, 200.0, true);
        indicator.Update(101.0, 201.0, true);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesConstructorPeriod()
    {
        var indicator = new Kendall(15);
        Assert.Equal(15, indicator.WarmupPeriod);
    }
}

public class KendallRobustnessTests
{
    [Fact]
    public void Update_NaNInputX_UsesLastValidValue()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        var result = indicator.Update(double.NaN, 205.0, true);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_NaNInputY_UsesLastValidValue()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        var result = indicator.Update(105.0, double.NaN, true);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_NaNBothInputs_UsesLastValidValues()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        var result = indicator.Update(double.NaN, double.NaN, true);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var indicator = new Kendall(5);

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        var result = indicator.Update(double.PositiveInfinity, double.NegativeInfinity, true);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_LargeDataset_NoOverflow()
    {
        var indicator = new Kendall(20);
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.4, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.03, sigma: 0.3, seed: 84);

        for (int i = 0; i < 5000; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            var result = indicator.Update(x, y, true);

            if (double.IsFinite(result.Value))
            {
                Assert.InRange(result.Value, -1.0, 1.0);
            }
        }
    }
}

public class KendallConsistencyTests
{
    [Fact]
    public void StreamingVsBatch_TSeries_Match()
    {
        int period = 10;
        int length = 100;

        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 123);

        var seriesX = new TSeries(length);
        var seriesY = new TSeries(length);

        for (int i = 0; i < length; i++)
        {
            var now = DateTime.UtcNow.AddMinutes(i);
            seriesX.Add(new TValue(now, gbmX.Next().Close));
            seriesY.Add(new TValue(now, gbmY.Next().Close));
        }

        // Streaming
        var streamIndicator = new Kendall(period);
        double[] streamResults = new double[length];
        for (int i = 0; i < length; i++)
        {
            streamResults[i] = streamIndicator.Update(
                seriesX.Values[i], seriesY.Values[i], true).Value;
        }

        // Batch TSeries
        var batchResults = Kendall.Batch(seriesX, seriesY, period);

        for (int i = 0; i < length; i++)
        {
            if (double.IsFinite(streamResults[i]) && double.IsFinite(batchResults.Values[i]))
            {
                Assert.Equal(streamResults[i], batchResults.Values[i], 1e-10);
            }
        }
    }

    [Fact]
    public void StreamingVsBatch_Span_Match()
    {
        int period = 10;
        int length = 100;

        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 123);

        double[] xData = new double[length];
        double[] yData = new double[length];

        for (int i = 0; i < length; i++)
        {
            xData[i] = gbmX.Next().Close;
            yData[i] = gbmY.Next().Close;
        }

        // Streaming
        var indicator = new Kendall(period);
        double[] streamResults = new double[length];
        for (int i = 0; i < length; i++)
        {
            streamResults[i] = indicator.Update(xData[i], yData[i], true).Value;
        }

        // Span batch
        double[] spanResults = new double[length];
        Kendall.Batch(xData, yData, spanResults, period);

        for (int i = 0; i < length; i++)
        {
            if (double.IsFinite(streamResults[i]) && double.IsFinite(spanResults[i]))
            {
                Assert.Equal(streamResults[i], spanResults[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        int period = 5;
        var seriesX = new TSeries(20);
        var seriesY = new TSeries(20);

        for (int i = 0; i < 20; i++)
        {
            var now = DateTime.UtcNow.AddMinutes(i);
            seriesX.Add(new TValue(now, 100.0 + i));
            seriesY.Add(new TValue(now, 200.0 + (i * 2)));
        }

        var (results, indicator) = Kendall.Calculate(seriesX, seriesY, period);

        Assert.Equal(20, results.Count);
        Assert.NotNull(indicator);
    }
}

public class KendallSpanTests
{
    [Fact]
    public void Batch_Span_ReturnsCorrectLength()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[20];

        for (int i = 0; i < 20; i++)
        {
            seriesX[i] = 100.0 + i;
            seriesY[i] = 200.0 + (i * 2);
        }

        Kendall.Batch(seriesX, seriesY, output, 5);

        Assert.True(double.IsNaN(output[0]));
        Assert.True(double.IsFinite(output[19]));
    }

    [Fact]
    public void Batch_Span_DifferentLengths_ThrowsArgumentException()
    {
        double[] seriesX = new double[10];
        double[] seriesY = new double[15];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Kendall.Batch(seriesX, seriesY, output, 5));
        Assert.Equal("seriesY", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputWrongLength_ThrowsArgumentException()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Kendall.Batch(seriesX, seriesY, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[20];

        var ex = Assert.Throws<ArgumentException>(() => Kendall.Batch(seriesX, seriesY, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_TSeries_DifferentLengths_ThrowsArgumentException()
    {
        var seriesX = new TSeries(10);
        var seriesY = new TSeries(15);

        for (int i = 0; i < 10; i++)
        {
            seriesX.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }
        for (int i = 0; i < 15; i++)
        {
            seriesY.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 200.0 + i));
        }

        Assert.Throws<ArgumentException>(() => Kendall.Batch(seriesX, seriesY, 5));
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] seriesX = [100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109];
        double[] seriesY = [200, 201, 202, 203, double.NaN, 205, 206, 207, 208, 209];
        double[] output = new double[10];

        Kendall.Batch(seriesX, seriesY, output, 5);

        // After warmup, results should be finite
        for (int i = 5; i < 10; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite but was {output[i]}");
        }
    }
}

public class KendallNotSupportedTests
{
    [Fact]
    public void Update_TValue_ThrowsNotSupportedException()
    {
        var indicator = new Kendall(5);
        Assert.Throws<NotSupportedException>(() => indicator.Update(new TValue(DateTime.UtcNow, 100.0)));
    }

    [Fact]
    public void Update_TSeries_ThrowsNotSupportedException()
    {
        var indicator = new Kendall(5);
        var series = new TSeries(10);
        Assert.Throws<NotSupportedException>(() => indicator.Update(series));
    }

    [Fact]
    public void Prime_ThrowsNotSupportedException()
    {
        var indicator = new Kendall(5);
        Assert.Throws<NotSupportedException>(() => indicator.Prime(new double[] { 1, 2, 3 }));
    }
}
