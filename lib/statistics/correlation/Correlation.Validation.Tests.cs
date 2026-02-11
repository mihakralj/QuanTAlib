using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Correlation (Pearson Correlation Coefficient) indicator.
/// Validates against Skender.Stock.Indicators.GetCorrelation and mathematical properties.
/// </summary>
public sealed class CorrelationValidationTests : IDisposable
{
    private const double Tolerance = 1e-10;
    private readonly ValidationTestData _data;
    private readonly ITestOutputHelper _output;

    public CorrelationValidationTests(ITestOutputHelper output)
    {
        _data = new ValidationTestData();
        _output = output;
    }

    public void Dispose()
    {
        _data.Dispose();
        GC.SuppressFinalize(this);
    }

    #region External Library Validation — Skender

    [Fact]
    public void Validate_Skender_Correlation()
    {
        // === DESCRIPTION ===
        // Compares QuanTAlib Correlation against Skender.Stock.Indicators.GetCorrelation
        // using Close prices (series A) vs Open prices (series B) from the same dataset.

        const int period = 20;

        // --- Skender: uses IQuote-based API ---
        // GetCorrelation compares two quote series by their Close prices
        // We use the same quotes for both but shift perspective: A=Close, B=Open
        // To use GetCorrelation, we need two separate IEnumerable<Quote> that share the same dates
        // Skender correlates the Close of quotesA with the Close of quotesB.
        // So we create quotesB where Close = Open of the original data.
        var quotesA = _data.SkenderQuotes; // Close = actual close prices
        var quotesB = new Quote[_data.Count];
        var closePrices = _data.ClosePrices.Span;
        var openPrices = _data.OpenPrices.Span;
        var timestamps = _data.Timestamps.Span;

        for (int i = 0; i < _data.Count; i++)
        {
            quotesB[i] = new Quote
            {
                Date = new DateTime(timestamps[i], DateTimeKind.Utc),
                Open = (decimal)openPrices[i],
                High = (decimal)openPrices[i],
                Low = (decimal)openPrices[i],
                Close = (decimal)openPrices[i], // Use Open prices as the "Close" for series B
                Volume = 0
            };
        }

        var sResult = quotesA.GetCorrelation(quotesB, period).ToList();

        // --- QuanTAlib: streaming API ---
        var corr = new Correlation(period);
        var qValues = new List<double>();

        for (int i = 0; i < _data.Count; i++)
        {
            var result = corr.Update(closePrices[i], openPrices[i]);
            qValues.Add(result.Value);
        }

        // --- Compare ---
        int matched = 0;
        int compared = 0;

        for (int i = period; i < _data.Count; i++)
        {
            double? sCorr = sResult[i].Correlation;
            double qCorr = qValues[i];

            if (!sCorr.HasValue || !double.IsFinite(qCorr))
            {
                continue;
            }

            compared++;
            double diff = Math.Abs(qCorr - sCorr.Value);

            Assert.True(diff <= ValidationHelper.SkenderTolerance,
                $"Correlation mismatch at [{i}]: QuanTAlib={qCorr:G17}, Skender={sCorr.Value:G17}, diff={diff:E3}");
            matched++;
        }

        Assert.True(matched > 100, $"Only matched {matched} Correlation values (expected > 100)");
        _output.WriteLine($"Correlation validated against Skender ({matched} values matched within tolerance {ValidationHelper.SkenderTolerance:E1})");
    }

    [Fact]
    public void Validate_Skender_Correlation_MultiplePeriods()
    {
        // === DESCRIPTION ===
        // Cross-validates QuanTAlib vs Skender across multiple lookback periods.

        int[] periods = [10, 20, 50];
        var closePrices = _data.ClosePrices.Span;
        var openPrices = _data.OpenPrices.Span;
        var timestamps = _data.Timestamps.Span;

        // Build quotesB (Open prices as Close for series B)
        var quotesB = new Quote[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            quotesB[i] = new Quote
            {
                Date = new DateTime(timestamps[i], DateTimeKind.Utc),
                Close = (decimal)openPrices[i],
            };
        }

        foreach (int period in periods)
        {
            var sResult = _data.SkenderQuotes.GetCorrelation(quotesB, period).ToList();

            var corr = new Correlation(period);
            int matched = 0;

            for (int i = 0; i < _data.Count; i++)
            {
                var result = corr.Update(closePrices[i], openPrices[i]);

                if (i >= period)
                {
                    double? sCorr = sResult[i].Correlation;
                    if (sCorr.HasValue && double.IsFinite(result.Value))
                    {
                        double diff = Math.Abs(result.Value - sCorr.Value);
                        Assert.True(diff <= ValidationHelper.SkenderTolerance,
                            $"Period={period}, [{i}]: Q={result.Value:G17}, S={sCorr.Value:G17}, diff={diff:E3}");
                        matched++;
                    }
                }
            }

            Assert.True(matched > 50, $"Period={period}: only matched {matched} values");
            _output.WriteLine($"  Period {period}: {matched} values matched");
        }
    }

    [Fact]
    public void Validate_Skender_Correlation_HighLow()
    {
        // === DESCRIPTION ===
        // Validates correlation between High and Low price series against Skender.

        const int period = 20;
        var highPrices = _data.HighPrices.Span;
        var lowPrices = _data.LowPrices.Span;
        var timestamps = _data.Timestamps.Span;

        // quotesA: Close = High prices
        var quotesA = new Quote[_data.Count];
        var quotesB = new Quote[_data.Count];

        for (int i = 0; i < _data.Count; i++)
        {
            var date = new DateTime(timestamps[i], DateTimeKind.Utc);
            quotesA[i] = new Quote { Date = date, Close = (decimal)highPrices[i] };
            quotesB[i] = new Quote { Date = date, Close = (decimal)lowPrices[i] };
        }

        var sResult = quotesA.GetCorrelation(quotesB, period).ToList();

        var corr = new Correlation(period);
        int matched = 0;

        for (int i = 0; i < _data.Count; i++)
        {
            var result = corr.Update(highPrices[i], lowPrices[i]);

            if (i >= period)
            {
                double? sCorr = sResult[i].Correlation;
                if (sCorr.HasValue && double.IsFinite(result.Value))
                {
                    double diff = Math.Abs(result.Value - sCorr.Value);
                    Assert.True(diff <= ValidationHelper.SkenderTolerance,
                        $"HighLow [{i}]: Q={result.Value:G17}, S={sCorr.Value:G17}, diff={diff:E3}");
                    matched++;
                }
            }
        }

        Assert.True(matched > 100, $"Only matched {matched} HighLow correlation values");
        _output.WriteLine($"Correlation (High vs Low) validated against Skender ({matched} values matched)");
    }

    #endregion

    #region Mathematical Property Validation

    [Fact]
    public void Correlation_PerfectLinearPositive_ReturnsOne()
    {
        // y = a + b*x with b > 0 should give r = 1
        var indicator = new Correlation(20);

        for (int i = 0; i < 50; i++)
        {
            double x = 10.0 + i * 2.5;
            double y = 5.0 + 3.0 * x; // y = 5 + 3x
            indicator.Update(x, y);
        }

        Assert.Equal(1.0, indicator.Last.Value, 1e-9);
    }

    [Fact]
    public void Correlation_PerfectLinearNegative_ReturnsMinusOne()
    {
        // y = a + b*x with b < 0 should give r = -1
        var indicator = new Correlation(20);

        for (int i = 0; i < 50; i++)
        {
            double x = 10.0 + i * 2.5;
            double y = 100.0 - 2.0 * x; // y = 100 - 2x
            indicator.Update(x, y);
        }

        Assert.Equal(-1.0, indicator.Last.Value, 1e-9);
    }

    [Fact]
    public void Correlation_SymmetryProperty_XY_Equals_YX()
    {
        // Correlation(X, Y) should equal Correlation(Y, X)
        var indicatorXY = new Correlation(10);
        var indicatorYX = new Correlation(10);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        for (int i = 0; i < 100; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            indicatorXY.Update(x, y);
            indicatorYX.Update(y, x);
        }

        Assert.Equal(indicatorXY.Last.Value, indicatorYX.Last.Value, 1e-10);
    }

    [Fact]
    public void Correlation_ScaleInvariance_AffineTransform()
    {
        // Correlation is invariant under positive linear transformations
        // corr(X, Y) = corr(aX + b, cY + d) when a, c > 0
        var indicator1 = new Correlation(10);
        var indicator2 = new Correlation(10);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        double a = 2.5, b = 100.0, c = 0.5, d = -50.0;

        for (int i = 0; i < 100; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            indicator1.Update(x, y);
            indicator2.Update(a * x + b, c * y + d);
        }

        // Relax tolerance due to floating point precision with large transformations
        Assert.Equal(indicator1.Last.Value, indicator2.Last.Value, 1e-6);
    }

    [Fact]
    public void Correlation_BoundedProperty_AlwaysBetweenMinusOneAndOne()
    {
        // Correlation coefficient is always in [-1, 1]
        var indicator = new Correlation(10);
        var gbmX = new GBM(startPrice: 100, mu: 0.1, sigma: 0.5, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: -0.05, sigma: 0.3, seed: 54321);

        for (int i = 0; i < 1000; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            var result = indicator.Update(x, y);

            if (double.IsFinite(result.Value))
            {
                Assert.InRange(result.Value, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void Correlation_ZeroVariance_ReturnsNaN()
    {
        // When one or both series have zero variance, correlation is undefined
        var indicator = new Correlation(10);

        for (int i = 0; i < 20; i++)
        {
            indicator.Update(100.0, 50.0 + i); // X constant, Y varying
        }

        // Correlation with constant series is undefined (0/0)
        Assert.True(double.IsNaN(indicator.Last.Value));
    }

    #endregion

    #region Known Value Tests

    [Fact]
    public void Correlation_KnownValues_SimpleSet()
    {
        // Test with known values that can be hand-calculated
        // X = [1, 2, 3, 4, 5], Y = [2, 4, 5, 4, 5]
        // Mean(X) = 3, Mean(Y) = 4
        // Cov(X,Y) = ((1-3)(2-4) + (2-3)(4-4) + (3-3)(5-4) + (4-3)(4-4) + (5-3)(5-4)) / 5
        //          = (4 + 0 + 0 + 0 + 2) / 5 = 1.2
        // Var(X) = ((1-3)² + (2-3)² + (3-3)² + (4-3)² + (5-3)²) / 5 = (4+1+0+1+4)/5 = 2
        // Var(Y) = ((2-4)² + (4-4)² + (5-4)² + (4-4)² + (5-4)²) / 5 = (4+0+1+0+1)/5 = 1.2
        // r = Cov(X,Y) / sqrt(Var(X) * Var(Y)) = 1.2 / sqrt(2 * 1.2) = 1.2 / sqrt(2.4)
        //   = 1.2 / 1.5492 ≈ 0.7746

        var indicator = new Correlation(5);
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [2, 4, 5, 4, 5];

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(x[i], y[i]);
        }

        double expected = 1.2 / Math.Sqrt(2.0 * 1.2); // ≈ 0.7746
        Assert.Equal(expected, indicator.Last.Value, 1e-4);
    }

    [Fact]
    public void Correlation_KnownValues_NoCorrelation()
    {
        // X = [1, 2, 3, 4, 5], Y = [3, 3, 3, 3, 3] (constant)
        // Should be NaN (or 0 with special handling)
        var indicator = new Correlation(5);
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [3, 3, 3, 3, 3];

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(x[i], y[i]);
        }

        // Zero variance in Y means correlation is undefined
        Assert.True(double.IsNaN(indicator.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void Correlation_BatchMatchesStreaming()
    {
        var seriesX = new TSeries();
        var seriesY = new TSeries();
        var baseTime = DateTime.UtcNow;
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        for (int i = 0; i < 100; i++)
        {
            seriesX.Add(baseTime.AddMinutes(i), gbmX.Next().Close);
            seriesY.Add(baseTime.AddMinutes(i), gbmY.Next().Close);
        }

        // Batch calculation
        var batchResult = Correlation.Batch(seriesX, seriesY, 20);

        // Streaming calculation
        var streamingIndicator = new Correlation(20);
        for (int i = 0; i < seriesX.Count; i++)
        {
            streamingIndicator.Update(seriesX[i].Value, seriesY[i].Value);
        }

        // Last values should match
        if (double.IsNaN(batchResult.Last.Value) && double.IsNaN(streamingIndicator.Last.Value))
        {
            Assert.True(true);
        }
        else
        {
            Assert.Equal(batchResult.Last.Value, streamingIndicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Correlation_SpanMatchesStreaming()
    {
        const int length = 100;
        var seriesX = new double[length];
        var seriesY = new double[length];
        var output = new double[length];
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        for (int i = 0; i < length; i++)
        {
            seriesX[i] = gbmX.Next().Close;
            seriesY[i] = gbmY.Next().Close;
        }

        // Span calculation
        Correlation.Batch(seriesX, seriesY, output, 20);

        // Streaming calculation
        var streamingIndicator = new Correlation(20);
        for (int i = 0; i < length; i++)
        {
            streamingIndicator.Update(seriesX[i], seriesY[i]);
        }

        // Last values should match
        if (double.IsNaN(output[length - 1]) && double.IsNaN(streamingIndicator.Last.Value))
        {
            Assert.True(true);
        }
        else
        {
            Assert.Equal(output[length - 1], streamingIndicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Correlation_ResetProducesSameResults()
    {
        var indicator = new Correlation(20);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        // First run
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(gbmX.Next().Close, gbmY.Next().Close);
        }
        var firstResult = indicator.Last.Value;

        indicator.Reset();

        // Second run with same seeds
        gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(gbmX.Next().Close, gbmY.Next().Close);
        }
        var secondResult = indicator.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Rolling Window Tests

    [Fact]
    public void Correlation_SlidingWindow_MovesCorrectly()
    {
        var indicator = new Correlation(5);

        // Build up with known values for period 5
        // After 5 values, window should be full
        double[] x = [10, 20, 30, 40, 50, 60, 70];
        double[] y = [15, 25, 35, 45, 55, 65, 75];

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(x[i], y[i]);
        }

        // Perfect correlation with same-slope linear data
        Assert.Equal(1.0, indicator.Last.Value, 1e-9);

        // Add more - window should slide
        indicator.Update(x[5], y[5]);
        Assert.Equal(1.0, indicator.Last.Value, 1e-9); // Still perfect linear

        indicator.Update(x[6], y[6]);
        Assert.Equal(1.0, indicator.Last.Value, 1e-9); // Still perfect linear
    }

    [Fact]
    public void Correlation_SlidingWindow_DropsOldValues()
    {
        var indicator = new Correlation(3);

        // First window: perfectly correlated
        indicator.Update(1, 2);
        indicator.Update(2, 4);
        indicator.Update(3, 6);
        Assert.Equal(1.0, indicator.Last.Value, 1e-9);

        // Add value that breaks perfect correlation in new window
        indicator.Update(4, 7); // Window is now [2,4,7] for Y, [2,3,4] for X
        // Not perfect linear anymore
        Assert.NotEqual(1.0, indicator.Last.Value);
    }

    #endregion

    #region Numerical Stability

    [Fact]
    public void Correlation_LargeValues_MaintainsStability()
    {
        var indicator = new Correlation(20);

        for (int i = 0; i < 50; i++)
        {
            double x = 1e8 + i * 1e5;
            double y = 2e8 + 2.0 * (i * 1e5); // Linear relationship
            indicator.Update(x, y);
        }

        // Should still detect linear relationship
        Assert.InRange(indicator.Last.Value, 0.99, 1.01);
    }

    [Fact]
    public void Correlation_SmallValues_MaintainsStability()
    {
        var indicator = new Correlation(20);

        // Use values that are small but not so small they cause numerical issues
        for (int i = 0; i < 50; i++)
        {
            double x = 0.001 + i * 0.0001;
            double y = 0.002 + 1.5 * (i * 0.0001); // Linear relationship
            indicator.Update(x, y);
        }

        // Should still detect linear relationship
        Assert.InRange(indicator.Last.Value, 0.99, 1.01);
    }

    [Fact]
    public void Correlation_MixedMagnitudes_HandlesCorrectly()
    {
        var indicator = new Correlation(20);

        for (int i = 0; i < 50; i++)
        {
            double x = 1000.0 + i;
            double y = 0.001 * (1000.0 + i); // Same pattern, different scale
            indicator.Update(x, y);
        }

        // Should detect perfect correlation despite scale difference
        Assert.Equal(1.0, indicator.Last.Value, 1e-9);
    }

    #endregion

    #region Statistical Scenarios

    [Fact]
    public void Correlation_HighPositiveCorrelation_DetectedCorrectly()
    {
        // Create two series with high positive correlation (r ≈ 0.95+)
        var indicator = new Correlation(20);

        // Use deterministic data that creates high correlation
        for (int i = 0; i < 100; i++)
        {
            double x = 100.0 + i + (i % 3) * 0.1; // Small variation
            double y = 0.9 * x + (i % 5) * 0.2; // High correlation with small noise
            indicator.Update(x, y);
        }

        Assert.True(indicator.Last.Value > 0.9);
    }

    [Fact]
    public void Correlation_NegativeCorrelation_DetectedCorrectly()
    {
        // Create two series with negative correlation
        var indicator = new Correlation(20);
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double x = 100.0 + i + (random.NextDouble() - 0.5) * 2;
            double y = 200.0 - 0.8 * i + (random.NextDouble() - 0.5) * 2; // Negative relationship
            indicator.Update(x, y);
        }

        Assert.True(indicator.Last.Value < -0.8);
    }

    [Fact]
    public void Correlation_WeakCorrelation_DetectedCorrectly()
    {
        // Create two series with weak correlation (lots of noise)
        var indicator = new Correlation(20);
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double x = 100.0 + i + (random.NextDouble() - 0.5) * 50;
            double y = 100.0 + 0.1 * i + (random.NextDouble() - 0.5) * 50; // Weak relationship
            indicator.Update(x, y);
        }

        // Should be close to zero but may be positive or negative
        Assert.InRange(Math.Abs(indicator.Last.Value), 0, 0.5);
    }

    #endregion

    #region Different Period Tests

    [Fact]
    public void Correlation_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator5 = new Correlation(5);
        var indicator20 = new Correlation(20);
        var indicator50 = new Correlation(50);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        for (int i = 0; i < 100; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            indicator5.Update(x, y);
            indicator20.Update(x, y);
            indicator50.Update(x, y);
        }

        // Different periods should yield different values
        Assert.NotEqual(indicator5.Last.Value, indicator20.Last.Value);
        Assert.NotEqual(indicator20.Last.Value, indicator50.Last.Value);
    }

    [Fact]
    public void Correlation_SmallPeriod_MoreVolatile()
    {
        var indicator3 = new Correlation(3);
        var indicator30 = new Correlation(30);
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 12345);
        var gbmY = new GBM(startPrice: 50, mu: 0.01, sigma: 0.15, seed: 54321);

        var values3 = new List<double>();
        var values30 = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            double x = gbmX.Next().Close;
            double y = gbmY.Next().Close;
            indicator3.Update(x, y);
            indicator30.Update(x, y);

            if (double.IsFinite(indicator3.Last.Value))
            {
                values3.Add(indicator3.Last.Value);
            }

            if (double.IsFinite(indicator30.Last.Value))
            {
                values30.Add(indicator30.Last.Value);
            }
        }

        // Calculate variance of correlation values
        double variance3 = CalculateVariance(values3);
        double variance30 = CalculateVariance(values30);

        // Shorter period should have higher variance (more volatile)
        Assert.True(variance3 > variance30, $"Expected small period variance ({variance3}) > large period variance ({variance30})");
    }

    private static double CalculateVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }

    #endregion
}
