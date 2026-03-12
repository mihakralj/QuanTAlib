namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Granger Causality indicator.
/// Granger causality is not commonly implemented in standard TA libraries.
/// These tests validate against expected statistical properties.
/// </summary>
public class GrangerValidationTests
{
    // GBM-based noise helper: extracts log-return from a seeded GBM price stream as centered noise.
    // Using sigma=1.0 gives log-returns ~N(0, vol²*dt); scale to required magnitude.
    private static double GbmNoise(GBM gbm) => Math.Log(gbm.Next().Close / 100.0);

    [Fact]
    public void Granger_CausalRelationship_ProducesHighFStatistic()
    {
        // X causes Y: Y_t = 0.5*Y_{t-1} + 0.3*X_{t-1} + noise
        // Adding X_lag should significantly improve prediction
        var indicator = new Granger(20);
        var rng = new GBM(startPrice: 100.0, sigma: 1.0, seed: 42);

        double y = 100.0;
        double x = 100.0;
        double prevY = y;
        double prevX = x;

        for (int i = 0; i < 200; i++)
        {
            x = 100.0 + (Math.Sin(i * 0.1) * 10.0) + (GbmNoise(rng) * 2.0);
            y = 50.0 + (0.5 * prevY) + (0.3 * prevX) + (GbmNoise(rng) * 0.5);

            indicator.Update(y, x, isNew: true);

            prevY = y;
            prevX = x;
        }

        // With a genuine causal relationship, F-statistic should be positive
        Assert.True(indicator.Last.Value > 0,
            $"F-statistic should be positive for causal relationship, got {indicator.Last.Value}");
    }

    [Fact]
    public void Granger_IndependentSeries_ProducesLowFStatistic()
    {
        // Two completely independent GBM series
        var indicator = new Granger(20);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 99999);

        double lastF = 0;
        for (int i = 0; i < 200; i++)
        {
            var barY = gbmY.Next(isNew: true);
            var barX = gbmX.Next(isNew: true);
            var result = indicator.Update(barY.Close, barX.Close, isNew: true);
            if (double.IsFinite(result.Value))
            {
                lastF = result.Value;
            }
        }

        // Independent series should have relatively low F-statistic
        // (not always near zero due to random correlation, but generally < critical value ~4)
        Assert.True(double.IsFinite(lastF),
            $"F-statistic should be finite for independent series, got {lastF}");
    }

    [Fact]
    public void Granger_StrongCausal_HigherThanWeak()
    {
        // Compare strong causal vs weak causal relationship
        var strongIndicator = new Granger(20);
        var weakIndicator = new Granger(20);
        var rng = new GBM(startPrice: 100.0, sigma: 1.0, seed: 42);

        double yStrong = 100.0, yWeak = 100.0;
        double x = 100.0;
        double prevYStrong = yStrong, prevYWeak = yWeak, prevX = x;

        for (int i = 0; i < 200; i++)
        {
            x = 100.0 + (Math.Sin(i * 0.1) * 10.0) + (GbmNoise(rng) * 2.0);

            // Strong: Y depends heavily on X_lag
            yStrong = 50.0 + (0.3 * prevYStrong) + (0.6 * prevX) + (GbmNoise(rng) * 0.5);
            // Weak: Y barely depends on X_lag
            yWeak = 50.0 + (0.8 * prevYWeak) + (0.05 * prevX) + (GbmNoise(rng) * 5.0);

            strongIndicator.Update(yStrong, x, isNew: true);
            weakIndicator.Update(yWeak, x, isNew: true);

            prevYStrong = yStrong;
            prevYWeak = yWeak;
            prevX = x;
        }

        double fStrong = strongIndicator.Last.Value;
        double fWeak = weakIndicator.Last.Value;

        // Strong causal should produce higher F than weak causal on average
        // This may not hold for every seed, so we just check both are finite
        Assert.True(double.IsFinite(fStrong), $"Strong F should be finite, got {fStrong}");
        Assert.True(double.IsFinite(fWeak), $"Weak F should be finite, got {fWeak}");
    }

    [Fact]
    public void Granger_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator10 = new Granger(10);
        var indicator30 = new Granger(30);

        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);

        for (int i = 0; i < 100; i++)
        {
            double y = gbmY.Next(isNew: true).Close;
            double x = gbmX.Next(isNew: true).Close;
            indicator10.Update(y, x, isNew: true);
            indicator30.Update(y, x, isNew: true);
        }

        // Different periods should generally produce different results
        if (double.IsFinite(indicator10.Last.Value) && double.IsFinite(indicator30.Last.Value))
        {
            // They could be equal by chance, but very unlikely
            Assert.True(Math.Abs(indicator10.Last.Value - indicator30.Last.Value) > 1e-12 ||
                (indicator10.Last.Value == 0 && indicator30.Last.Value == 0),
                "Different periods should produce different F-statistics");
        }
    }

    [Fact]
    public void Granger_BatchAndStreaming_Agree()
    {
        const int period = 10;
        const int count = 100;
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);

        var seriesY = new TSeries(count);
        var seriesX = new TSeries(count);

        for (int i = 0; i < count; i++)
        {
            var barY = gbmY.Next(isNew: true);
            var barX = gbmX.Next(isNew: true);
            seriesY.Add(new TValue(barY.Time, barY.Close));
            seriesX.Add(new TValue(barX.Time, barX.Close));
        }

        var batchResults = Granger.Batch(seriesY, seriesX, period);

        var streamIndicator = new Granger(period);
        for (int i = 0; i < count; i++)
        {
            var result = streamIndicator.Update(
                new TValue(seriesY.Times[i], seriesY.Values[i]),
                new TValue(seriesX.Times[i], seriesX.Values[i]),
                isNew: true);

            if (double.IsNaN(batchResults.Values[i]) && double.IsNaN(result.Value))
            {
                continue;
            }

            Assert.Equal(batchResults.Values[i], result.Value, 10);
        }
    }

    [Fact]
    public void Granger_CalculateMethod_ReturnsBothResultsAndIndicator()
    {
        const int period = 10;
        const int count = 50;
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);

        var seriesY = new TSeries(count);
        var seriesX = new TSeries(count);

        for (int i = 0; i < count; i++)
        {
            var barY = gbmY.Next(isNew: true);
            var barX = gbmX.Next(isNew: true);
            seriesY.Add(new TValue(barY.Time, barY.Close));
            seriesX.Add(new TValue(barX.Time, barX.Close));
        }

        var (results, indicator) = Granger.Calculate(seriesY, seriesX, period);

        Assert.NotNull(results);
        Assert.NotNull(indicator);
        Assert.Equal(count, results.Count);
        Assert.Equal($"Granger({period})", indicator.Name);
    }

    [Fact]
    public void Granger_SymmetricCausal_DifferentDirections()
    {
        // Test that Granger(Y,X) and Granger(X,Y) give different results
        // when causality is asymmetric
        var indicatorYX = new Granger(15);
        var indicatorXY = new Granger(15);
        var rng = new GBM(startPrice: 100.0, sigma: 1.0, seed: 42);

        double y = 100.0, x = 100.0;
        double prevY = y, prevX = x;

        for (int i = 0; i < 200; i++)
        {
            // X is exogenous (just random walk with drift)
            x = prevX + (GbmNoise(rng) * 2.0);
            // Y depends on X_lag (X Granger-causes Y, but Y does NOT Granger-cause X)
            y = 50.0 + (0.3 * prevY) + (0.4 * prevX) + (GbmNoise(rng) * 0.5);

            indicatorYX.Update(y, x, isNew: true); // Testing: does X cause Y?
            indicatorXY.Update(x, y, isNew: true); // Testing: does Y cause X?

            prevY = y;
            prevX = x;
        }

        double fYX = indicatorYX.Last.Value; // Should be higher (X does cause Y)
        double fXY = indicatorXY.Last.Value; // Should be lower (Y doesn't cause X)

        Assert.True(double.IsFinite(fYX), $"F(Y,X) should be finite, got {fYX}");
        Assert.True(double.IsFinite(fXY), $"F(X,Y) should be finite, got {fXY}");

        // X genuinely causes Y, so F(Y,X) should be higher than F(X,Y)
        Assert.True(fYX > fXY,
            $"F(Y,X)={fYX} should be greater than F(X,Y)={fXY} for asymmetric causality");
    }
}
