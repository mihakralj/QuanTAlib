using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for UI (Ulcer Index).
/// UI = √(avg(percentDrawdown²)) where percentDrawdown = ((close - highestClose) / highestClose) × 100
/// </summary>
public class UiValidationTests
{
    private const int DefaultPeriod = 14;

    private static TSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(bars[i].Time, bars[i].Close));
        }
        return ts;
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the UI formula: √(avg(percentDrawdown²))
    /// </summary>
    [Fact]
    public void Ui_Formula_IsCorrect()
    {
        // Manual calculation for period=5 with known prices
        double[] prices = [100, 102, 101, 103, 100];
        double[] highests = [100, 102, 102, 103, 103];
        double[] percentDrawdowns = new double[5];
        double[] squaredDrawdowns = new double[5];

        for (int i = 0; i < 5; i++)
        {
            percentDrawdowns[i] = ((prices[i] - highests[i]) / highests[i]) * 100;
            squaredDrawdowns[i] = percentDrawdowns[i] * percentDrawdowns[i];
        }

        double avgSquared = squaredDrawdowns.Average();
        double expected = Math.Sqrt(avgSquared);

        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        Assert.Equal(expected, result.Value, 10);
    }

    /// <summary>
    /// Validates UI is zero when price continuously rises (no drawdowns).
    /// </summary>
    [Fact]
    public void Ui_RisingPrices_ReturnsZero()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Continuously rising prices
        double[] prices = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // When price is always at new highs, there's no drawdown
        Assert.Equal(0.0, result.Value, 10);
    }

    /// <summary>
    /// Validates UI increases with deeper drawdowns.
    /// </summary>
    [Fact]
    public void Ui_DeeperDrawdown_HigherValue()
    {
        var time = DateTime.UtcNow;

        // Shallow drawdown (5% from peak)
        var ui1 = new Ui(period: 5);
        double[] prices1 = [100, 105, 110, 110, 104.5]; // 5% drawdown from 110
        for (int i = 0; i < prices1.Length; i++)
        {
            ui1.Update(new TValue(time.AddSeconds(i), prices1[i]));
        }
        double shallow = ui1.Last.Value;

        // Deep drawdown (20% from peak)
        var ui2 = new Ui(period: 5);
        double[] prices2 = [100, 105, 110, 110, 88]; // 20% drawdown from 110
        for (int i = 0; i < prices2.Length; i++)
        {
            ui2.Update(new TValue(time.AddSeconds(i), prices2[i]));
        }
        double deep = ui2.Last.Value;

        Assert.True(deep > shallow,
            $"Deeper drawdown should have higher UI: deep={deep:F4}, shallow={shallow:F4}");
    }

    /// <summary>
    /// Validates UI captures sustained drawdowns over multiple periods.
    /// </summary>
    [Fact]
    public void Ui_SustainedDrawdown_CapturesCorrectly()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Price rises to 110, then stays at lower levels
        double[] prices = [100, 105, 110, 105, 100, 100, 100];
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // UI should be positive (sustained drawdown from 110)
        Assert.True(result.Value > 0, $"UI should be positive for sustained drawdown, got {result.Value}");
    }

    // === Streaming Validation ===

    /// <summary>
    /// Validates streaming calculation matches manual calculation.
    /// </summary>
    [Fact]
    public void Ui_StreamingMatchesManual()
    {
        int period = 5;
        var ui = new Ui(period);
        var time = DateTime.UtcNow;

        double[] prices = [100, 102, 98, 105, 100, 103, 97, 110, 105, 100];

        // Track for manual calculation
        var closeBuffer = new List<double>();
        var sqDrawdownBuffer = new List<double>();

        for (int i = 0; i < prices.Length; i++)
        {
            var result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));

            // Manual calculation
            closeBuffer.Add(prices[i]);
            if (closeBuffer.Count > period)
            {
                closeBuffer.RemoveAt(0);
            }

            double highest = closeBuffer.Max();
            double percentDrawdown = highest > 0 ? ((prices[i] - highest) / highest) * 100 : 0;
            double squaredDrawdown = percentDrawdown * percentDrawdown;

            sqDrawdownBuffer.Add(squaredDrawdown);
            if (sqDrawdownBuffer.Count > period)
            {
                sqDrawdownBuffer.RemoveAt(0);
            }

            double avgSq = sqDrawdownBuffer.Average();
            double expected = Math.Sqrt(avgSq);

            Assert.Equal(expected, result.Value, 10);
        }
    }

    /// <summary>
    /// Validates batch calculation matches streaming.
    /// </summary>
    [Fact]
    public void Ui_BatchMatchesStreaming()
    {
        var data = GenerateTestData(100);

        // Streaming
        var streamingUi = new Ui(DefaultPeriod);
        var streamingResults = new double[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            streamingResults[i] = streamingUi.Update(data[i]).Value;
        }

        // Batch
        var batchOutput = new double[data.Count];
        Ui.Batch(data.Values, batchOutput, DefaultPeriod);

        // Compare all values
        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchOutput[i], 10);
        }
    }

    /// <summary>
    /// Validates TSeries batch matches streaming.
    /// </summary>
    [Fact]
    public void Ui_TSeriesBatchMatchesStreaming()
    {
        var data = GenerateTestData(100);

        // Streaming
        var streamingUi = new Ui(DefaultPeriod);
        for (int i = 0; i < data.Count; i++)
        {
            streamingUi.Update(data[i]);
        }

        // Batch via TSeries
        var batchResult = Ui.Batch(data, DefaultPeriod);

        Assert.Equal(streamingUi.Last.Value, batchResult.Last.Value, 10);
    }

    // === Property Validation ===

    /// <summary>
    /// Validates UI is always non-negative.
    /// </summary>
    [Fact]
    public void Ui_Output_IsNonNegative()
    {
        var data = GenerateTestData(100);
        var ui = new Ui(DefaultPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = ui.Update(data[i]);
            Assert.True(result.Value >= 0, $"UI should be non-negative at index {i}");
        }
    }

    /// <summary>
    /// Validates UI output is always finite.
    /// </summary>
    [Fact]
    public void Ui_Output_IsFinite()
    {
        var data = GenerateTestData(100);
        var ui = new Ui(DefaultPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = ui.Update(data[i]);
            Assert.True(double.IsFinite(result.Value), $"UI should be finite at index {i}");
        }
    }

    /// <summary>
    /// Validates UI is bounded (typically single digits for reasonable price movements).
    /// </summary>
    [Fact]
    public void Ui_Output_IsReasonablyBounded()
    {
        var data = GenerateTestData(100);
        var ui = new Ui(DefaultPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = ui.Update(data[i]);
            // UI is percentage-based; for normal markets, rarely exceeds 20
            Assert.True(result.Value < 50, $"UI seems too high at index {i}: {result.Value}");
        }
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of flat prices (no volatility).
    /// </summary>
    [Fact]
    public void Ui_FlatPrices_ReturnsZero()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            var result = ui.Update(new TValue(time.AddSeconds(i), 100.0));
            // Flat prices = no drawdown = UI is zero
            Assert.Equal(0.0, result.Value, 10);
        }
    }

    /// <summary>
    /// Validates handling of very small price movements.
    /// </summary>
    [Fact]
    public void Ui_SmallMovements_HandledCorrectly()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + Math.Sin(i * 0.1) * 0.001; // Tiny movements
            var result = ui.Update(new TValue(time.AddSeconds(i), price));

            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    /// <summary>
    /// Validates handling of very large price movements.
    /// </summary>
    [Fact]
    public void Ui_LargeMovements_HandledCorrectly()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Large price swings
        double[] prices = [100, 200, 50, 150, 75, 250, 100];

        for (int i = 0; i < prices.Length; i++)
        {
            var result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));

            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Ui_BarCorrection_WorksCorrectly()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Feed initial data
        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // Add new bar
        ui.Update(new TValue(time.AddSeconds(5), 95), isNew: true);
        double afterNew = ui.Last.Value;

        // Correct with different value (much larger drawdown)
        ui.Update(new TValue(time.AddSeconds(5), 80), isNew: false);
        double afterCorrection = ui.Last.Value;

        // Restore original
        ui.Update(new TValue(time.AddSeconds(5), 95), isNew: false);
        double afterRestore = ui.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge.
    /// </summary>
    [Fact]
    public void Ui_IterativeCorrections_Converge()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Feed data
        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            ui.Update(new TValue(time.AddSeconds(4), 100 + j * 2), isNew: false);
        }

        // Final correction back to original
        ui.Update(new TValue(time.AddSeconds(4), 104), isNew: false);
        double afterCorrections = ui.Last.Value;

        // Fresh calculation
        var uiFresh = new Ui(period: 5);
        for (int i = 0; i < 5; i++)
        {
            uiFresh.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }
        double freshValue = uiFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    /// <summary>
    /// Validates Reset clears state completely.
    /// </summary>
    [Fact]
    public void Ui_Reset_ClearsState()
    {
        var ui = new Ui(DefaultPeriod);
        var data = GenerateTestData(30);

        // Feed data
        for (int i = 0; i < 20; i++)
        {
            ui.Update(data[i]);
        }

        // Reset
        ui.Reset();

        // State should be cleared
        Assert.False(ui.IsHot);
        Assert.Equal(default, ui.Last);

        // Feed data again
        for (int i = 0; i < 15; i++)
        {
            ui.Update(data[i]);
        }

        // Fresh indicator
        var uiFresh = new Ui(DefaultPeriod);
        for (int i = 0; i < 15; i++)
        {
            uiFresh.Update(data[i]);
        }

        Assert.Equal(uiFresh.Last.Value, ui.Last.Value, 10);
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Ui_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var data = GenerateTestData(100);
            var ui = new Ui(DefaultPeriod);

            for (int i = 0; i < data.Count; i++)
            {
                ui.Update(data[i]);
            }
            results.Add(ui.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates UI responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Ui_RespondsToVolatilityChange()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;
        var lowVolResults = new List<double>();
        var highVolResults = new List<double>();

        // Low volatility regime (small drawdowns)
        double price;
        for (int i = 0; i < 10; i++)
        {
            price = 100 + (i * 0.1); // Gentle uptrend with tiny corrections
            lowVolResults.Add(ui.Update(new TValue(time.AddSeconds(i), price)).Value);
        }

        // High volatility regime (large drawdowns)
        for (int i = 10; i < 20; i++)
        {
            // Sawtooth pattern with big drops
            price = i % 2 == 0 ? 110 : 90;
            highVolResults.Add(ui.Update(new TValue(time.AddSeconds(i), price)).Value);
        }

        double avgHighVol = highVolResults.Skip(2).Average(); // Skip transition period

        // High vol UI should be significantly higher due to larger drawdowns
        Assert.True(avgHighVol > 5, $"High vol UI ({avgHighVol:F4}) should show significant stress");
    }

    // === WarmupPeriod Validation ===

    /// <summary>
    /// Validates WarmupPeriod equals period.
    /// </summary>
    [Fact]
    public void Ui_WarmupPeriod_EqualsPeriod()
    {
        var ui = new Ui(period: 20);
        Assert.Equal(20, ui.WarmupPeriod);
    }

    /// <summary>
    /// Validates IsHot is true after period bars.
    /// </summary>
    [Fact]
    public void Ui_IsHot_AfterPeriod()
    {
        int period = 10;
        var ui = new Ui(period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < period - 1; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
            Assert.False(ui.IsHot);
        }

        ui.Update(new TValue(time.AddSeconds(period - 1), 100 + period - 1));
        Assert.True(ui.IsHot);
    }

    // === NaN/Infinity Handling ===

    /// <summary>
    /// Validates NaN input uses last valid value.
    /// </summary>
    [Fact]
    public void Ui_NaNInput_UsesLastValid()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var result = ui.Update(new TValue(time.AddSeconds(5), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates Infinity input uses last valid value.
    /// </summary>
    [Fact]
    public void Ui_InfinityInput_UsesLastValid()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var result = ui.Update(new TValue(time.AddSeconds(5), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates batch handles NaN values.
    /// </summary>
    [Fact]
    public void Ui_BatchNaN_HandledCorrectly()
    {
        var source = new double[] { 100, 102, double.NaN, 98, 101 };
        var output = new double[5];

        Ui.Batch(source, output, period: 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
            Assert.True(output[i] >= 0, $"Output at index {i} should be non-negative");
        }
    }

    // === Period Sensitivity ===

    /// <summary>
    /// Validates longer period produces smoother results.
    /// </summary>
    [Fact]
    public void Ui_LongerPeriod_SmootherResults()
    {
        var data = GenerateTestData(100);

        var uiShort = new Ui(period: 5);
        var uiLong = new Ui(period: 20);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < data.Count; i++)
        {
            shortResults.Add(uiShort.Update(data[i]).Value);
            longResults.Add(uiLong.Update(data[i]).Value);
        }

        // Calculate variance of changes (smoothness measure)
        double shortVariance = CalculateChangeVariance(shortResults.Skip(20).ToList());
        double longVariance = CalculateChangeVariance(longResults.Skip(20).ToList());

        // Longer period should be smoother (lower variance of changes)
        Assert.True(longVariance < shortVariance,
            $"Longer period should be smoother: short variance={shortVariance:F6}, long variance={longVariance:F6}");
    }

    private static double CalculateChangeVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var changes = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            changes.Add(values[i] - values[i - 1]);
        }

        double mean = changes.Average();
        double variance = changes.Select(c => (c - mean) * (c - mean)).Average();
        return variance;
    }

    // === Known Value Test ===

    /// <summary>
    /// Validates UI against manually calculated known values.
    /// </summary>
    [Fact]
    public void Ui_KnownValues_MatchExpected()
    {
        var ui = new Ui(period: 3);
        var time = DateTime.UtcNow;

        // Period 3, prices: 100, 105, 100
        // Highest: 100, 105, 105
        // %Drawdown: 0, 0, (100-105)/105*100 = -4.762
        // SqDrawdown: 0, 0, 22.677
        // AvgSq = 22.677/3 = 7.559
        // UI = sqrt(7.559) = 2.749

        ui.Update(new TValue(time.AddSeconds(0), 100));
        ui.Update(new TValue(time.AddSeconds(1), 105));
        var result = ui.Update(new TValue(time.AddSeconds(2), 100));

        double expected = Math.Sqrt(22.6757369614512 / 3.0);
        Assert.Equal(expected, result.Value, 5);
    }

    // === External Library Validation ===
    // NOTE: Skender.Stock.Indicators uses a different Ulcer Index algorithm variant:
    //   Skender: For each bar j in the period window, highestClose = max(closes from window_start to j)
    //            Each bar gets its own "growing" highest reference within the evaluation window.
    //   QuanTAlib: highestClose = max(closes over the entire rolling period window)
    //   Both are valid implementations of the Ulcer Index concept, but produce different values.
    //   No external validation test is added for UI due to this algorithmic difference.

    [Fact]
    public void Ui_MatchesOoples_Structural()
    {
        // CalculateUlcerIndex — structural test (different highest-close window variant)
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();

        var result = new StockData(ooplesData).CalculateUlcerIndex();
        var values = result.CustomValuesList;

        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples UI values, got {finiteCount}");
    }
}
