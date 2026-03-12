namespace QuanTAlib.Tests;

public class CorrelationTests
{
    [Fact]
    public void Constructor_ValidPeriod_CreatesIndicator()
    {
        var indicator = new Correlation(20);
        Assert.Equal("Correlation(20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_MinimumValidPeriod_CreatesIndicator()
    {
        var indicator = new Correlation(2);
        Assert.Equal("Correlation(2)", indicator.Name);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Correlation(1));
        Assert.Throws<ArgumentException>(() => new Correlation(0));
        Assert.Throws<ArgumentException>(() => new Correlation(-5));
    }

    [Fact]
    public void Update_SingleValue_ReturnsNaN()
    {
        var indicator = new Correlation(5);
        var result = indicator.Update(100.0, 200.0, true);
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_TwoValues_ReturnsValidCorrelation()
    {
        var indicator = new Correlation(5);
        indicator.Update(100.0, 200.0, true);
        var result = indicator.Update(102.0, 204.0, true);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_PerfectPositiveCorrelation_ReturnsOne()
    {
        var indicator = new Correlation(5);

        // Same values scaled by constant should give correlation = 1
        for (int i = 0; i < 10; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 + (2 * i); // y = 200 + 2x (perfectly correlated)
            indicator.Update(x, y, true);
        }

        Assert.True(indicator.IsHot);
        Assert.InRange(indicator.Last.Value, 0.999, 1.001);
    }

    [Fact]
    public void Update_PerfectNegativeCorrelation_ReturnsMinusOne()
    {
        var indicator = new Correlation(5);

        // Opposite movements should give correlation = -1
        for (int i = 0; i < 10; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 - (2 * i); // y = 200 - 2x (perfectly negatively correlated)
            indicator.Update(x, y, true);
        }

        Assert.True(indicator.IsHot);
        Assert.InRange(indicator.Last.Value, -1.001, -0.999);
    }

    [Fact]
    public void Update_ConstantValues_ReturnsNaN()
    {
        var indicator = new Correlation(5);

        // Constant values have zero variance, so correlation is undefined
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0, 200.0, true);
        }

        Assert.True(double.IsNaN(indicator.Last.Value));
    }

    [Fact]
    public void Update_BarCorrection_RestoresState()
    {
        var indicator1 = new Correlation(5);
        var indicator2 = new Correlation(5);

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

        // indicator2: Add bar, then correct it
        indicator2.Update(999.0, 999.0, true); // Wrong values
        indicator2.Update(110.0, 205.0, false); // Correct them

        // Values should match
        Assert.Equal(indicator1.Last.Value, indicator2.Last.Value, 1e-9);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var corrected = new Correlation(5);
        var direct = new Correlation(5);

        // Feed identical initial state
        for (int i = 0; i < 8; i++)
        {
            double x = 100.0 + i;
            double y = 200.0 + (i * 2);
            corrected.Update(x, y, true);
            direct.Update(x, y, true);
        }

        // Target final value for the current bar
        const double finalX = 108.0;
        const double finalY = 216.0;

        // Correction path: new bar, several rewrites, final rewrite back to target
        corrected.Update(finalX, finalY, true);
        corrected.Update(finalX + 1.0, finalY + 2.0, false);
        corrected.Update(finalX - 0.5, finalY - 1.0, false);
        corrected.Update(finalX + 0.25, finalY + 0.5, false);
        corrected.Update(finalX, finalY, false);

        // Direct path: same initial state + one new bar with final value
        direct.Update(finalX, finalY, true);

        Assert.Equal(direct.Last.Value, corrected.Last.Value, 1e-12);
    }

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var indicator = new Correlation(5);

        // Add valid data
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        _ = indicator.Last.Value;

        // Add NaN - should use last valid value, result must be finite
        var result = indicator.Update(double.NaN, double.NaN, true);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var indicator = new Correlation(5);

        // Add valid data
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }

        // Add Infinity - should use last valid value, result must be finite
        var result = indicator.Update(double.PositiveInfinity, double.NegativeInfinity, true);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void IsHot_BelowPeriod_ReturnsFalse()
    {
        var indicator = new Correlation(10);
        indicator.Update(100.0, 200.0, true);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void IsHot_AtPeriod_ReturnsTrue()
    {
        var indicator = new Correlation(10);
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0 + i, 200.0 + i, true);
        }
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Correlation(5);

        // Add data
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(100.0 + i, 200.0 + (i * 2), true);
        }

        Assert.True(indicator.IsHot);

        // Reset
        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Update_TValue_ThrowsNotSupportedException()
    {
        var indicator = new Correlation(5);
        Assert.Throws<NotSupportedException>(() => indicator.Update(new TValue(DateTime.UtcNow, 100.0)));
    }

    [Fact]
    public void Update_TSeries_ThrowsNotSupportedException()
    {
        var indicator = new Correlation(5);
        var series = new TSeries(10);
        Assert.Throws<NotSupportedException>(() => indicator.Update(series));
    }

    [Fact]
    public void Prime_ThrowsNotSupportedException()
    {
        var indicator = new Correlation(5);
        Assert.Throws<NotSupportedException>(() => indicator.Prime(new double[] { 1, 2, 3 }));
    }

    [Fact]
    public void Calculate_TSeries_ReturnsCorrectLength()
    {
        var seriesX = new TSeries(20);
        var seriesY = new TSeries(20);

        for (int i = 0; i < 20; i++)
        {
            seriesX.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
            seriesY.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 200.0 + (i * 2)));
        }

        var result = Correlation.Batch(seriesX, seriesY, 5);

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Calculate_TSeries_DifferentLengths_ThrowsArgumentException()
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

        Assert.Throws<ArgumentException>(() => Correlation.Batch(seriesX, seriesY, 5));
    }

    [Fact]
    public void Calculate_Span_ReturnsCorrectValues()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[20];

        for (int i = 0; i < 20; i++)
        {
            seriesX[i] = 100.0 + i;
            seriesY[i] = 200.0 + (i * 2);
        }

        Correlation.Batch(seriesX, seriesY, output, 5);

        // First value should be NaN (not enough data)
        Assert.True(double.IsNaN(output[0]));

        // After warmup, should have valid correlation
        Assert.True(double.IsFinite(output[19]));
    }

    [Fact]
    public void Calculate_Span_DifferentLengths_ThrowsArgumentException()
    {
        double[] seriesX = new double[10];
        double[] seriesY = new double[15];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Correlation.Batch(seriesX, seriesY, output, 5));
    }

    [Fact]
    public void Calculate_Span_OutputWrongLength_ThrowsArgumentException()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Correlation.Batch(seriesX, seriesY, output, 5));
    }

    [Fact]
    public void Calculate_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] seriesX = new double[20];
        double[] seriesY = new double[20];
        double[] output = new double[20];

        Assert.Throws<ArgumentException>(() => Correlation.Batch(seriesX, seriesY, output, 1));
    }

    [Fact]
    public void CorrelationRange_AlwaysBetweenMinusOneAndOne()
    {
        var indicator = new Correlation(10);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 12345);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.5, seed: 54321);

        for (int i = 0; i < 1000; i++)
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

    [Fact]
    public void StreamingVsBatch_Consistency()
    {
        int period = 10;
        int length = 100;

        // Generate data
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 123);
        double[] seriesX = new double[length];
        double[] seriesY = new double[length];

        for (int i = 0; i < length; i++)
        {
            seriesX[i] = gbmX.Next().Close;
            seriesY[i] = gbmY.Next().Close;
        }

        // Streaming calculation
        var indicator = new Correlation(period);
        double[] streamingResults = new double[length];
        for (int i = 0; i < length; i++)
        {
            streamingResults[i] = indicator.Update(seriesX[i], seriesY[i], true).Value;
        }

        // Batch calculation
        double[] batchResults = new double[length];
        Correlation.Batch(seriesX, seriesY, batchResults, period);

        // Compare last 50 values (after warmup)
        for (int i = length - 50; i < length; i++)
        {
            if (double.IsFinite(streamingResults[i]) && double.IsFinite(batchResults[i]))
            {
                Assert.Equal(streamingResults[i], batchResults[i], 1e-9);
            }
        }
    }
}
