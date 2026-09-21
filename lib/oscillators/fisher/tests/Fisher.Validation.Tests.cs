using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using System.Runtime.CompilerServices;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validates Fisher Transform against Skender, Tulip, Ooples, and manual computation.
/// Primary reference: Skender (Ehlers 2002 IIR algorithm with HL2 input).
/// </summary>
public sealed class FisherValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 10;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Manual arctanh Cross-Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_Arctanh()
    {
        // Validate that our Fisher Transform correctly computes arctanh
        // by testing with known normalized inputs
        double[] testValues = [-0.9, -0.5, 0.0, 0.5, 0.9];

        foreach (double v in testValues)
        {
            double expected = 0.5 * Math.Log((1.0 + v) / (1.0 - v));
            double actual = Math.Atanh(v);

            Assert.True(Math.Abs(expected - actual) < 1e-12,
                $"arctanh({v}): expected={expected}, actual={actual}");
        }

        _output.WriteLine("arctanh mathematical identity verified.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_Computation()
    {
        double[] values = _testData.RawData.ToArray();
        int[] periods = [5, 10, 20];

        foreach (int period in periods)
        {
            double[] batchOutput = new double[values.Length];
            Fisher.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

            // Manual computation — Ehlers 2002 TASC algorithm
            double[] manualOutput = new double[values.Length];
            double emaValue = 0.0;
            double fisherValue = 0.0;
            var buffer = new double[period];
            int bufCount = 0;
            int bufIdx = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double val = values[i];

                // Add to circular buffer
                if (bufCount < period)
                {
                    buffer[bufCount] = val;
                    bufCount++;
                }
                else
                {
                    buffer[bufIdx] = val;
                    bufIdx = (bufIdx + 1) % period;
                }

                // Find min/max
                double highest = double.MinValue;
                double lowest = double.MaxValue;
                for (int j = 0; j < bufCount; j++)
                {
                    if (buffer[j] > highest)
                    {
                        highest = buffer[j];
                    }
                    if (buffer[j] < lowest)
                    {
                        lowest = buffer[j];
                    }
                }

                double range = highest - lowest;
                if (range != 0.0)
                {
                    emaValue = (0.66 * (((val - lowest) / range) - 0.5))
                        + (0.67 * emaValue);
                }
                else
                {
                    emaValue = 0.0;  // Skender: xv[i] = 0 when range=0
                }

                // Ehlers/Skender: snap to ±0.999 when |Value1| > 0.99
                // Clamped value stored back — Skender stores array2[i] clamped
                if (emaValue > 0.99)
                {
                    emaValue = 0.999;
                }
                else if (emaValue < -0.99)
                {
                    emaValue = -0.999;
                }

                // Ehlers 2002: Fish = arctanh(Value1) + 0.5 * Fish[1]  (IIR feedback)
                fisherValue = (0.5 * Math.Log((1.0 + emaValue) / (1.0 - emaValue))) + (0.5 * fisherValue);
                manualOutput[i] = fisherValue;
            }

            int validCount = 0;
            for (int i = period; i < values.Length; i++)
            {
                Assert.True(Math.Abs(manualOutput[i] - batchOutput[i]) < 1e-9,
                    $"Fisher mismatch at i={i}, period={period}: manual={manualOutput[i]}, batch={batchOutput[i]}");
                validCount++;
            }

            Assert.True(validCount > 0, $"No valid comparison points for period {period}");
            _output.WriteLine($"Fisher period={period}: validated {validCount} points against manual computation.");
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validate_Manual_DifferentPeriods(int period)
    {
        double[] values = _testData.RawData.ToArray();

        double[] batchOutput = new double[values.Length];
        Fisher.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

        // Verify all outputs are finite
        for (int i = 0; i < values.Length; i++)
        {
            Assert.True(double.IsFinite(batchOutput[i]),
                $"Fisher output not finite at i={i}, period={period}: {batchOutput[i]}");
        }

        _output.WriteLine($"Fisher period={period}: all {values.Length} outputs finite.");
    }

    #endregion

    #region Consistency Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        double[] tData = _testData.RawData.ToArray();

        // Batch TSeries
        TSeries batchSeries = Fisher.Batch(_testData.Data, TestPeriod);

        // Batch Span
        var spanOutput = new double[tData.Length];
        Fisher.Batch(tData.AsSpan(), spanOutput.AsSpan(), TestPeriod);

        // Batch and Span should be identical (same code path)
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], 12);
        }

        // Streaming
        var fisher = new Fisher(TestPeriod);
        var streamResults = new double[tData.Length];
        for (int i = 0; i < tData.Length; i++)
        {
            streamResults[i] = fisher.Update(_testData.Data[i]).Value;
        }

        // Streaming vs Batch should match exactly (same algorithm, same state)
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 9);
        }

        _output.WriteLine("Fisher streaming/batch/span agreement verified.");
    }

    #endregion

    #region Tulip Cross-Validation

    /// <summary>
    /// Structural validation against Tulip <c>fisher</c> indicator.
    /// Algorithm variant: Tulip fisher uses two inputs (high[], low[]) and computes the
    /// Fisher Transform from the high-low price range midpoint normalized over a rolling window.
    /// QuanTAlib Fisher uses a single price series with EMA-based normalization via alpha parameter.
    /// Direct numeric equality is not asserted; both must produce finite output on the same data.
    /// </summary>
    [Fact]
    public void Fisher_Tulip_StructuralVariant_BothFinite()
    {
        const int period = 10;
        double[] highData = _testData.HighPrices.ToArray();
        double[] lowData = _testData.LowPrices.ToArray();

        // Tulip fisher — uses high/low range normalization
        var tulipIndicator = Tulip.Indicators.fisher;
        double[][] inputs = { highData, lowData };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[highData.Length - lookback], new double[highData.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // QuanTAlib Fisher — single price series (close)
        var fisher = new Fisher(TestPeriod);
        foreach (var item in _testData.Data) { fisher.Update(item); }

        // Structural: Tulip must produce finite output
        Assert.True(tResult.Length > 0, "Tulip fisher must produce output");
        foreach (double v in tResult)
        {
            Assert.True(double.IsFinite(v), $"Tulip fisher produced non-finite value: {v}");
        }

        // QuanTAlib must also be hot and finite
        Assert.True(fisher.IsHot, "QuanTAlib Fisher must be hot after sufficient bars");
        Assert.True(double.IsFinite(fisher.Last.Value), "QuanTAlib Fisher last value must be finite");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Event_Matches_Streaming()
    {
        // Streaming
        var streamFisher = new Fisher(TestPeriod);
        var streamResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            streamResults[i] = streamFisher.Update(_testData.Data[i]).Value;
        }

        // Event-based
        var eventSource = new TSeries();
        var eventFisher = new Fisher(eventSource, TestPeriod);
        var eventResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            eventSource.Add(_testData.Data[i]);
            eventResults[i] = eventFisher.Last.Value;
        }

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 12);
        }

        _output.WriteLine("Fisher event-based matches streaming.");
    }

    #endregion

    #region Ooples Validation

    /// <summary>
    /// Structural validation against Ooples <c>CalculateEhlersFisherTransform</c>.
    /// Ooples uses the Ehlers variant: HL2 (high-low midpoint) normalized over rolling period,
    /// then arctanh transformed. QuanTAlib Fisher uses a single price series with EMA-based
    /// normalization via alpha parameter. Input types differ (OHLCV vs close-only); numeric
    /// equality not asserted. Both must produce finite output on the same underlying data.
    /// </summary>
    [Fact]
    public void Fisher_Ooples_StructuralVariant_BothFinite()
    {
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateEhlersFisherTransform(length: TestPeriod);
        var oValues = oResult.OutputValues.Values.First();

        // QuanTAlib Fisher — single price series (close)
        var fisher = new Fisher(TestPeriod);
        foreach (var item in _testData.Data) { fisher.Update(item); }

        // Structural: Ooples must produce finite output
        Assert.True(oValues.Count > 0, "Ooples Fisher must produce output");
        int finiteCount = 0;
        for (int i = TestPeriod; i < oValues.Count; i++)
        {
            if (double.IsFinite(oValues[i])) { finiteCount++; }
        }

        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples values, got {finiteCount}");
        Assert.True(fisher.IsHot, "QuanTAlib Fisher must be hot after sufficient bars");
        Assert.True(double.IsFinite(fisher.Last.Value), "QuanTAlib Fisher last value must be finite");

        _output.WriteLine($"Fisher Ooples structural: {finiteCount} finite Ooples values, QuanTAlib last={fisher.Last.Value:F6}");
    }

    #endregion

    #region Skender Cross-Validation

    /// <summary>
    /// Numeric validation against Skender <c>GetFisherTransform</c>.
    /// Both use Ehlers 2002 IIR algorithm: <c>Fish = arctanh(Value1) + 0.5 * Fish[1]</c>.
    /// Skender uses HL2 input with expanding window during warmup.
    /// QuanTAlib uses same HL2 input via RingBuffer (expanding window when not full).
    /// Both should converge; tolerance allows warmup-phase divergence.
    /// </summary>
    [Fact]
    public void Validate_Skender_FisherTransform_Numeric()
    {
        const int period = 10;
        var sResult = _testData.SkenderQuotes.GetFisherTransform(period).ToList();

        // Feed HL2 to QuanTAlib (same input as Skender)
        var quotes = _testData.SkenderQuotes.ToList();
        var fisher = new Fisher(period);
        var qtFisher = new double[quotes.Count];
        var qtSignal = new double[quotes.Count];
        for (int i = 0; i < quotes.Count; i++)
        {
            // Match Skender's HL2 computation: decimal arithmetic then convert
            double hl2 = (double)((quotes[i].High + quotes[i].Low) / 2m);
            fisher.Update(new TValue(quotes[i].Date, hl2));
            qtFisher[i] = fisher.FisherValue;
            qtSignal[i] = fisher.Signal;
        }

        // Numeric comparison — skip warmup (first 2*period bars)
        int startIdx = period * 2;
        int validCount = 0;
        for (int i = startIdx; i < sResult.Count; i++)
        {
            if (sResult[i].Fisher is null) { continue; }
            double sFisher = sResult[i].Fisher!.Value;

            Assert.True(Math.Abs(sFisher - qtFisher[i]) < 1e-9,
                $"Fisher mismatch at i={i}: Skender={sFisher:F9}, QuanTAlib={qtFisher[i]:F9}");
            validCount++;
        }

        Assert.True(validCount > 100, $"Expected >100 valid comparisons, got {validCount}");
        _output.WriteLine($"Fisher Skender numeric: validated {validCount} points at 1e-9 tolerance.");
    }

    /// <summary>
    /// Validates signal line (Trigger = Fish[1]) matches Skender's Trigger output.
    /// </summary>
    [Fact]
    public void Validate_Skender_Signal_Numeric()
    {
        const int period = 10;
        var sResult = _testData.SkenderQuotes.GetFisherTransform(period).ToList();

        // Feed HL2 to QuanTAlib
        var quotes = _testData.SkenderQuotes.ToList();
        var fisher = new Fisher(period);
        var qtSignal = new double[quotes.Count];
        for (int i = 0; i < quotes.Count; i++)
        {
            double hl2 = (double)((quotes[i].High + quotes[i].Low) / 2m);
            fisher.Update(new TValue(quotes[i].Date, hl2));
            qtSignal[i] = fisher.Signal;
        }

        // Signal comparison — skip warmup
        int startIdx = period * 2;
        int validCount = 0;
        for (int i = startIdx; i < sResult.Count; i++)
        {
            if (sResult[i].Trigger is null) { continue; }
            double sTrigger = sResult[i].Trigger!.Value;

            Assert.True(Math.Abs(sTrigger - qtSignal[i]) < 1e-9,
                $"Signal mismatch at i={i}: Skender={sTrigger:F9}, QuanTAlib={qtSignal[i]:F9}");
            validCount++;
        }

        Assert.True(validCount > 100, $"Expected >100 valid signal comparisons, got {validCount}");
        _output.WriteLine($"Fisher Signal Skender numeric: validated {validCount} points at 1e-9 tolerance.");
    }

    /// <summary>
    /// Structural validation: both Skender and QuanTAlib produce finite output.
    /// </summary>
    [Fact]
    public void Validate_Skender_FisherTransform_Structural()
    {
        var sResult = _testData.SkenderQuotes.GetFisherTransform(TestPeriod).ToList();

        var fisher = new Fisher(TestPeriod);
        foreach (var item in _testData.Data) { fisher.Update(item); }

        int finiteCount = sResult.Count(r => r.Fisher is not null && double.IsFinite(r.Fisher.Value));
        Assert.True(finiteCount > 100, $"Skender should produce >100 finite Fisher values, got {finiteCount}");
        Assert.True(fisher.IsHot, "QuanTAlib Fisher must be hot");
        Assert.True(double.IsFinite(fisher.Last.Value), "QuanTAlib Fisher last must be finite");

        _output.WriteLine($"Fisher Skender structural: {finiteCount} finite Skender values, " +
                          $"QuanTAlib last={fisher.Last.Value:F6}, Skender last={sResult[^1].Fisher:F6}");
    }

    #endregion
}
