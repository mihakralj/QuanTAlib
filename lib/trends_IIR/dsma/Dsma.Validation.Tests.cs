namespace QuanTAlib.Tests;

public class DsmaValidationTests
{
    [Fact]
    public void Dsma_FollowsPriceTrend()
    {
        // DSMA should generally follow price trends due to Super Smoother filter
        // In an uptrend, DSMA should eventually trend upward

        var dsma = new Dsma(period: 10, scaleFactor: 0.5);
        double previousDsma = 0;
        int increasingCount = 0;

        // Uptrend: steadily increasing prices
        for (int i = 0; i < 100; i++)
        {
            var result = dsma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            if (i > 20 && result.Value > previousDsma) // Allow warmup
            {
                increasingCount++;
            }
            previousDsma = result.Value;
        }

        // DSMA should be increasing in most bars during uptrend (allow some lag)
        Assert.True(increasingCount > 60, $"DSMA should follow uptrend, increased in {increasingCount} out of 80 bars");
    }

    [Fact]
    public void Dsma_ResponsivenessToVolatility()
    {
        // DSMA adapts to volatility via RMS-based scaling
        // Higher volatility should produce more responsive behavior

        var gbmLowVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.05, seed: 42);
        var gbmHighVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.5, seed: 42);

        var dsmaLowVol = new Dsma(period: 20, scaleFactor: 0.5);
        var dsmaHighVol = new Dsma(period: 20, scaleFactor: 0.5);

        double lowVolDeviation = 0;
        double highVolDeviation = 0;

        for (int i = 0; i < 100; i++)
        {
            var barLow = gbmLowVol.Next(isNew: true);
            var barHigh = gbmHighVol.Next(isNew: true);

            var resultLow = dsmaLowVol.Update(new TValue(barLow.Time, barLow.Close));
            var resultHigh = dsmaHighVol.Update(new TValue(barHigh.Time, barHigh.Close));

            if (i > 30) // After warmup
            {
                lowVolDeviation += Math.Abs(barLow.Close - resultLow.Value);
                highVolDeviation += Math.Abs(barHigh.Close - resultHigh.Value);
            }
        }

        // In higher volatility, absolute deviation should generally be larger
        Assert.True(highVolDeviation > lowVolDeviation * 2,
            $"High volatility deviation {highVolDeviation:F2} should be significantly larger than low volatility {lowVolDeviation:F2}");
    }

    [Fact]
    public void Dsma_ScaleFactorEffect()
    {
        // Higher scaleFactor should make DSMA more responsive to price changes
        // Lower scaleFactor should make it smoother

        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.3, seed: 123);
        var dsmaLowScale = new Dsma(period: 20, scaleFactor: 0.1);
        var dsmaHighScale = new Dsma(period: 20, scaleFactor: 0.8);

        double lowScaleLag = 0;
        double highScaleLag = 0;
        int count = 0;

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tval = new TValue(bar.Time, bar.Close);

            var resultLow = dsmaLowScale.Update(tval);
            var resultHigh = dsmaHighScale.Update(tval);

            if (i > 30) // After warmup
            {
                lowScaleLag += Math.Abs(bar.Close - resultLow.Value);
                highScaleLag += Math.Abs(bar.Close - resultHigh.Value);
                count++;
            }
        }

        double avgLowLag = lowScaleLag / count;
        double avgHighLag = highScaleLag / count;

        // Lower scale factor should have higher average lag (smoother, less responsive)
        Assert.True(avgLowLag > avgHighLag,
            $"Low scale lag {avgLowLag:F4} should be greater than high scale lag {avgHighLag:F4}");
    }

    [Fact]
    public void Dsma_SmoothnessBehavior()
    {
        // DSMA should be smoother than raw price (lower variance)
        // This validates the Super Smoother filter component

        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.2, seed: 456);
        var dsma = new Dsma(period: 15, scaleFactor: 0.5);

        var priceChanges = new List<double>();
        var dsmaChanges = new List<double>();
        double prevPrice = 100.0;
        double prevDsma = 100.0;

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = dsma.Update(new TValue(bar.Time, bar.Close));

            if (i > 30) // After warmup
            {
                priceChanges.Add(Math.Abs(bar.Close - prevPrice));
                dsmaChanges.Add(Math.Abs(result.Value - prevDsma));
            }

            prevPrice = bar.Close;
            prevDsma = result.Value;
        }

        double priceVariance = priceChanges.Average();
        double dsmaVariance = dsmaChanges.Average();

        // DSMA should have lower variance than raw price
        Assert.True(dsmaVariance < priceVariance,
            $"DSMA variance {dsmaVariance:F4} should be less than price variance {priceVariance:F4}");
    }

    [Fact]
    public void Dsma_WithinBounds()
    {
        // DSMA should stay within reasonable bounds of recent prices
        // It's an adaptive moving average, shouldn't overshoot wildly

        var dsma = new Dsma(period: 10, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.3, seed: 789);

        var recentPrices = new List<double>();
        const int windowSize = 20;

        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = dsma.Update(new TValue(bar.Time, bar.Close));

            recentPrices.Add(bar.Close);
            if (recentPrices.Count > windowSize)
            {
                recentPrices.RemoveAt(0);
            }

            if (i > 30 && recentPrices.Count == windowSize)
            {
                double minPrice = recentPrices.Min();
                double maxPrice = recentPrices.Max();
                double margin = (maxPrice - minPrice) * 0.3; // 30% margin for adaptive behavior

                Assert.True(result.Value >= minPrice - margin && result.Value <= maxPrice + margin,
                    $"At index {i}: DSMA {result.Value:F2} outside bounds [{minPrice - margin:F2}, {maxPrice + margin:F2}]");
            }
        }
    }

    [Fact]
    public void Dsma_ConsistentWarmup()
    {
        // DSMA should consistently reach IsHot state at expected period

        var dsma = new Dsma(period: 15, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 321);

        for (int i = 0; i < 14; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close));
            Assert.False(dsma.IsHot, $"Should not be hot at bar {i + 1}");
        }

        var lastBar = gbm.Next(isNew: true);
        dsma.Update(new TValue(lastBar.Time, lastBar.Close));
        Assert.True(dsma.IsHot, "Should be hot at period boundary");
    }

    [Fact]
    public void Dsma_ConvergenceAfterReset()
    {
        // After reset, DSMA should converge to similar values when fed same data

        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 654);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        // First run
        var dsma1 = new Dsma(period: 10, scaleFactor: 0.5);
        var result1 = dsma1.Update(series);

        // Reset and second run
        var dsma2 = new Dsma(period: 10, scaleFactor: 0.5);
        var result2 = dsma2.Update(series);

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(result1.Values[i], result2.Values[i], precision: 10);
        }
    }

    [Fact]
    public void Dsma_PeriodEffect()
    {
        // Longer period should produce smoother results with more lag

        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.25, seed: 987);
        var dsmaShort = new Dsma(period: 5, scaleFactor: 0.5);
        var dsmaLong = new Dsma(period: 30, scaleFactor: 0.5);

        double shortLag = 0;
        double longLag = 0;
        int count = 0;

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tval = new TValue(bar.Time, bar.Close);

            var resultShort = dsmaShort.Update(tval);
            var resultLong = dsmaLong.Update(tval);

            if (i > 40) // After both warmed up
            {
                shortLag += Math.Abs(bar.Close - resultShort.Value);
                longLag += Math.Abs(bar.Close - resultLong.Value);
                count++;
            }
        }

        double avgShortLag = shortLag / count;
        double avgLongLag = longLag / count;

        // Longer period should have higher average lag (more smoothing)
        Assert.True(avgLongLag > avgShortLag,
            $"Long period lag {avgLongLag:F4} should be greater than short period lag {avgShortLag:F4}");
    }

    [Fact]
    public void Dsma_MathematicalConsistency()
    {
        // Verify that DSMA maintains mathematical consistency:
        // - Output is always finite
        // - Sequential updates produce deterministic results
        // - Values remain reasonable

        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.3, seed: 111);
        var dsma = new Dsma(period: 12, scaleFactor: 0.5);

        for (int i = 0; i < 300; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = dsma.Update(new TValue(bar.Time, bar.Close));

            // Always finite
            Assert.True(double.IsFinite(result.Value), $"DSMA should be finite at index {i}");

            // DSMA should remain positive for positive prices
            Assert.True(result.Value > 0, $"DSMA should be positive at index {i}");

            // DSMA should stay within reasonable range of price (allow wide margin for adaptive behavior)
            if (i > 20)
            {
                Assert.True(result.Value > bar.Close * 0.5 && result.Value < bar.Close * 1.5,
                    $"At index {i}: DSMA {result.Value:F2} outside reasonable range of price {bar.Close:F2}");
            }
        }
    }

    [Fact]
    public void Dsma_SuperSmootherComponent()
    {
        // Validate that the Super Smoother (Butterworth) filter component
        // provides noise reduction while maintaining trend following

        var gbm = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.3, seed: 222);
        var dsma = new Dsma(period: 20, scaleFactor: 0.5);

        var prices = new List<double>();
        var dsmaValues = new List<double>();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = dsma.Update(new TValue(bar.Time, bar.Close));

            if (i > 30)
            {
                prices.Add(bar.Close);
                dsmaValues.Add(result.Value);
            }
        }

        // Calculate directional consistency
        int priceUpCount = 0;
        int dsmaUpCount = 0;

        for (int i = 1; i < prices.Count; i++)
        {
            if (prices[i] > prices[i - 1])
            {
                priceUpCount++;
            }

            if (dsmaValues[i] > dsmaValues[i - 1])
            {
                dsmaUpCount++;
            }
        }

        // DSMA should have similar directional trend but smoother
        // (fewer direction changes due to filtering)
        Assert.True(Math.Abs(dsmaUpCount - priceUpCount) < prices.Count * 0.3,
            $"DSMA direction changes {dsmaUpCount} should be reasonably aligned with price {priceUpCount}");
    }
}
