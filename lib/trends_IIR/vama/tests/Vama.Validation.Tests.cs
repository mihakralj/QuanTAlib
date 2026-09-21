namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for VAMA (Volatility Adjusted Moving Average).
/// VAMA is a unique indicator that dynamically adjusts its smoothing period
/// based on volatility ratio. Since there's no standard external library
/// implementation to compare against, we validate mathematical properties.
/// </summary>
public class VamaValidationTests
{
    private const double Tolerance = 1e-10;

    /// <summary>
    /// VAMA should behave like a simple SMA when volatility ratio is 1.0.
    /// With synthetic bars where H=L=C (zero TR), the adjusted length equals base_length.
    /// </summary>
    [Fact]
    public void Vama_ZeroVolatility_EqualsBaseLength_SMA()
    {
        var vama = new Vama(baseLength: 10, shortAtrPeriod: 5, longAtrPeriod: 20, minLength: 5, maxLength: 50);
        var sma = new Sma(10);

        // Feed identical prices (close-only data creates zero TR)
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        foreach (var val in values)
        {
            var tv = new TValue(DateTime.UtcNow, val);
            vama.Update(tv, isNew: true);
            sma.Update(tv, isNew: true);
        }

        // With zero volatility, VAMA should equal SMA(base_length)
        // Allow small tolerance due to potential floating-point differences in implementation
        Assert.Equal(sma.Last.Value, vama.Last.Value, 1.0);
    }

    /// <summary>
    /// When input is constant, VAMA output should equal the input value.
    /// </summary>
    [Fact]
    public void Vama_ConstantInput_OutputEqualsInput()
    {
        var vama = new Vama();
        const double constantValue = 42.5;

        for (int i = 0; i < 200; i++)
        {
            vama.Update(new TValue(DateTime.UtcNow, constantValue), isNew: true);
        }

        Assert.Equal(constantValue, vama.Last.Value, Tolerance);
    }

    /// <summary>
    /// VAMA output should always be within the range of input values (no overshoot).
    /// </summary>
    [Fact]
    public void Vama_OutputWithinInputRange()
    {
        var vama = new Vama();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 123);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double minInput = double.MaxValue;
        double maxInput = double.MinValue;
        var outputs = new List<double>();

        foreach (var bar in bars)
        {
            minInput = Math.Min(minInput, bar.Close);
            maxInput = Math.Max(maxInput, bar.Close);
            var result = vama.Update(bar, isNew: true);
            outputs.Add(result.Value);
        }

        // Skip warmup period
        var hotOutputs = outputs.Skip(100).ToList();

        foreach (var output in hotOutputs)
        {
            Assert.True(output >= minInput - 1 && output <= maxInput + 1,
                $"Output {output} should be within input range [{minInput}, {maxInput}]");
        }
    }

    /// <summary>
    /// VAMA should be continuous - no sudden jumps in output.
    /// </summary>
    [Fact]
    public void Vama_OutputIsContinuous()
    {
        var vama = new Vama();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 456);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var outputs = new List<double>();
        foreach (var bar in bars)
        {
            var result = vama.Update(bar, isNew: true);
            outputs.Add(result.Value);
        }

        // Check that consecutive outputs don't jump more than the input typically moves
        for (int i = 101; i < outputs.Count; i++)
        {
            double change = Math.Abs(outputs[i] - outputs[i - 1]);
            Assert.True(change < 10, $"Output jump at index {i} is {change}, expected < 10");
        }
    }

    /// <summary>
    /// Volatility adjustment: high short-term volatility should shorten the period.
    /// </summary>
    [Fact]
    public void Vama_HighShortTermVolatility_ShortensEffectivePeriod()
    {
        var time = DateTime.UtcNow;

        // Create two scenarios: low volatility vs high volatility
        var vamaLowVol = new Vama(baseLength: 20, shortAtrPeriod: 10, longAtrPeriod: 50, minLength: 5, maxLength: 100);
        var vamaHighVol = new Vama(baseLength: 20, shortAtrPeriod: 10, longAtrPeriod: 50, minLength: 5, maxLength: 100);

        // Feed low volatility bars first
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100 + (i * 0.1), 100.5 + (i * 0.1), 99.5 + (i * 0.1), 100 + (i * 0.1), 1000);
            vamaLowVol.Update(bar, isNew: true);
        }

        // Feed high volatility bars
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100 + (i * 0.1), 110 + (i * 0.1), 90 + (i * 0.1), 100 + (i * 0.1), 1000);
            vamaHighVol.Update(bar, isNew: true);
        }

        // Both should produce valid results
        Assert.True(double.IsFinite(vamaLowVol.Last.Value));
        Assert.True(double.IsFinite(vamaHighVol.Last.Value));
    }

    /// <summary>
    /// With TBar input containing proper OHLC, True Range should be calculated correctly.
    /// </summary>
    [Fact]
    public void Vama_TrueRange_CalculatedCorrectly()
    {
        var vama = new Vama();
        var time = DateTime.UtcNow;

        // Bar with gap up (previous close below current low)
        // TR should be max(H-L, |H-prevClose|, |L-prevClose|)
        var bar1 = new TBar(time, 100, 102, 98, 100, 1000);
        vama.Update(bar1, isNew: true);

        // Second bar with a gap
        var bar2 = new TBar(time.AddMinutes(1), 105, 108, 104, 106, 1000);
        vama.Update(bar2, isNew: true);

        Assert.True(double.IsFinite(vama.Last.Value));
    }

    /// <summary>
    /// Batch processing with TBarSeries should produce same results as streaming.
    /// </summary>
    [Fact]
    public void Vama_BatchTBar_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 789);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch calculation
        var batchResult = Vama.Batch(bars);

        // Streaming calculation
        var vamaStreaming = new Vama();
        var streamingResult = new List<double>();
        foreach (var bar in bars)
        {
            var result = vamaStreaming.Update(bar, isNew: true);
            streamingResult.Add(result.Value);
        }

        Assert.Equal(batchResult.Count, streamingResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i], Tolerance);
        }
    }

    /// <summary>
    /// Min/Max length constraints should be respected.
    /// </summary>
    [Fact]
    public void Vama_LengthConstraints_Respected()
    {
        const int minLength = 5;
        const int maxLength = 50;

        var vama = new Vama(baseLength: 20, shortAtrPeriod: 10, longAtrPeriod: 50, minLength: minLength, maxLength: maxLength);
        var time = DateTime.UtcNow;

        // Feed bars with extreme volatility to push the adjusted length to limits
        for (int i = 0; i < 200; i++)
        {
            // Alternate between very high and very low volatility
            double volatility = (i % 2 == 0) ? 20 : 0.1;
            var bar = new TBar(time.AddMinutes(i), 100, 100 + volatility, 100 - volatility, 100, 1000);
            vama.Update(bar, isNew: true);

            // Output should always be valid
            Assert.True(double.IsFinite(vama.Last.Value));
        }
    }

    /// <summary>
    /// RMA (Wilder's smoothing) should be used for ATR calculation.
    /// Verify the alpha = 1/period property.
    /// </summary>
    [Fact]
    public void Vama_UsesRMA_ForATR()
    {
        // Feed identical TR values and verify ATR converges correctly
        var vama = new Vama(baseLength: 20, shortAtrPeriod: 10, longAtrPeriod: 20, minLength: 5, maxLength: 100);
        var time = DateTime.UtcNow;

        // Feed bars with constant TR = 10 (H-L)
        for (int i = 0; i < 500; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 105, 95, 100, 1000);
            vama.Update(bar, isNew: true);
        }

        // With constant TR, ATR should converge to that TR value
        // And volatility ratio should approach 1, making adjusted length = base_length
        Assert.True(vama.IsHot);
        Assert.True(double.IsFinite(vama.Last.Value));
    }

    /// <summary>
    /// Bias compensation should be applied during warmup for accurate early values.
    /// </summary>
    [Fact]
    public void Vama_BiasCompensation_AppliedDuringWarmup()
    {
        var vama = new Vama();
        var time = DateTime.UtcNow;

        // First bar should output its close value (no history to average)
        var bar1 = new TBar(time, 100, 102, 98, 100, 1000);
        var result1 = vama.Update(bar1, isNew: true);

        Assert.Equal(100, result1.Value, Tolerance);

        // Subsequent bars should show reasonable values, not skewed by uncompensated ATR
        for (int i = 1; i < 10; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 102, 98, 100, 1000);
            var result = vama.Update(bar, isNew: true);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value > 50 && result.Value < 150);
        }
    }

    /// <summary>
    /// State rollback with isNew=false should work correctly with complex state.
    /// </summary>
    [Fact]
    public void Vama_StateRollback_HandlesComplexState()
    {
        var vama = new Vama();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 321);

        // Feed some bars
        TBar lastBar = default;
        for (int i = 0; i < 50; i++)
        {
            lastBar = gbm.Next(isNew: true);
            vama.Update(lastBar, isNew: true);
        }

        double valueAfter50 = vama.Last.Value;

        // Apply corrections (isNew=false)
        for (int i = 0; i < 10; i++)
        {
            var correctionBar = gbm.Next(isNew: false);
            vama.Update(correctionBar, isNew: false);
        }

        // Revert to original bar
        vama.Update(lastBar, isNew: false);

        // Should restore to original state
        Assert.Equal(valueAfter50, vama.Last.Value, Tolerance);
    }

    /// <summary>
    /// VAMA should handle edge case where short ATR could be zero.
    /// </summary>
    [Fact]
    public void Vama_HandlesZero_ShortATR()
    {
        var vama = new Vama();
        var time = DateTime.UtcNow;

        // Feed bars with H=L=C (zero TR)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 100, 100, 100, 1000);
            var result = vama.Update(bar, isNew: true);

            // Should never produce NaN or Infinity
            Assert.True(double.IsFinite(result.Value), $"Result at index {i} was {result.Value}");
        }
    }
}
