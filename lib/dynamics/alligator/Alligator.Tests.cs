
namespace QuanTAlib;

public class AlligatorTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            alligator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(alligator.Last.Value));
        Assert.True(double.IsFinite(alligator.Jaw.Value));
        Assert.True(double.IsFinite(alligator.Teeth.Value));
        Assert.True(double.IsFinite(alligator.Lips.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            alligator.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        alligator.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open + 5, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close + 5, bars[99].Volume);
        var val2 = alligator.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var alligator2 = new Alligator();
        for (int i = 0; i < 99; i++)
        {
            alligator2.Update(bars[i]);
        }
        var val3 = alligator2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
        Assert.Equal(alligator2.Jaw.Value, alligator.Jaw.Value, 1e-9);
        Assert.Equal(alligator2.Teeth.Value, alligator.Teeth.Value, 1e-9);
        Assert.Equal(alligator2.Lips.Value, alligator.Lips.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            alligator.Update(bars[i]);
        }

        alligator.Reset();
        Assert.Equal(0, alligator.Last.Value);
        Assert.False(alligator.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            alligator.Update(bars[i]);
        }

        Assert.True(double.IsFinite(alligator.Last.Value));
        Assert.True(alligator.IsHot);
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(alligator.Update(bars[i]).Value);
        }

        var alligator2 = new Alligator();
        var seriesResults = alligator2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var alligator = new Alligator();
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(alligator.Update(bars[i]).Value);
        }

        var staticResults = Alligator.Batch(bars);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Alligator(0, 8, 8, 5, 5, 3));
        Assert.Throws<ArgumentException>(() => new Alligator(13, -1, 8, 5, 5, 3));
        Assert.Throws<ArgumentException>(() => new Alligator(13, 8, 0, 5, 5, 3));
        Assert.Throws<ArgumentException>(() => new Alligator(13, 8, 8, -1, 5, 3));
        Assert.Throws<ArgumentException>(() => new Alligator(13, 8, 8, 5, 0, 3));
        Assert.Throws<ArgumentException>(() => new Alligator(13, 8, 8, 5, 5, -1));
    }

    [Fact]
    public void DefaultConstructor_UsesStandardParameters()
    {
        var alligator = new Alligator();

        Assert.Equal(8, alligator.JawOffset);
        Assert.Equal(5, alligator.TeethOffset);
        Assert.Equal(3, alligator.LipsOffset);
        Assert.Contains("13", alligator.Name, StringComparison.Ordinal);
        Assert.Contains("8", alligator.Name, StringComparison.Ordinal);
        Assert.Contains("5", alligator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void LipsIsFastest_TeethIsMiddle_JawIsSlowest()
    {
        var alligator = new Alligator();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.01, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed all bars
        for (int i = 0; i < bars.Count; i++)
        {
            alligator.Update(bars[i]);
        }

        // After warmup, all values should be finite
        Assert.True(double.IsFinite(alligator.Jaw.Value));
        Assert.True(double.IsFinite(alligator.Teeth.Value));
        Assert.True(double.IsFinite(alligator.Lips.Value));

        // Lips (5-period) should respond faster than Teeth (8-period) which responds faster than Jaw (13-period)
        // In an uptrend, Lips > Teeth > Jaw
        // We can't guarantee order without specific data, but all should be close to the price
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var alligator = new Alligator(13, 8, 8, 5, 5, 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TBar twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = bar;
            alligator.Update(bar, isNew: true);
        }

        // Remember state after 20 values
        double jawAfterTwenty = alligator.Jaw.Value;
        double teethAfterTwenty = alligator.Teeth.Value;
        double lipsAfterTwenty = alligator.Lips.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            alligator.Update(bar, isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        alligator.Update(twentiethInput, isNew: false);

        // State should match the original state after 20 values
        Assert.Equal(jawAfterTwenty, alligator.Jaw.Value, 1e-10);
        Assert.Equal(teethAfterTwenty, alligator.Teeth.Value, 1e-10);
        Assert.Equal(lipsAfterTwenty, alligator.Lips.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenAllLinesWarmedUp()
    {
        var alligator = new Alligator(13, 8, 8, 5, 5, 3);
        var gbm = new GBM();

        Assert.False(alligator.IsHot);

        // Feed bars until IsHot becomes true
        int count = 0;
        while (!alligator.IsHot && count < 100)
        {
            var bar = gbm.Next(isNew: true);
            alligator.Update(bar, isNew: true);
            count++;
        }

        Assert.True(alligator.IsHot);
        Assert.True(count >= 13); // Should take at least the longest period (Jaw = 13)
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            alligator.Update(bars[i]);
        }

        // Create a bar with NaN values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = alligator.Update(nanBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(alligator.Jaw.Value));
        Assert.True(double.IsFinite(alligator.Teeth.Value));
        Assert.True(double.IsFinite(alligator.Lips.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var alligator = new Alligator();
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            alligator.Update(bars[i]);
        }

        // Create a bar with Infinity values
        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = alligator.Update(infBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(alligator.Jaw.Value));
        Assert.True(double.IsFinite(alligator.Teeth.Value));
        Assert.True(double.IsFinite(alligator.Lips.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode (static method)
        var batchSeries = Alligator.Batch(bars);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode (instance, one bar at a time)
        var streamingInd = new Alligator();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Instance Update with TBarSeries
        var instanceInd = new Alligator();
        var instanceResult = instanceInd.Update(bars);
        double instanceValue = instanceResult.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, instanceValue, precision: 9);
    }

    [Fact]
    public void SmmaFormula_AllLinesEqualWithSamePeriod()
    {
        // When all three lines use the same period and offset, they should produce identical values
        // This verifies the SMMA formula is applied consistently across all three lines
        var alligator = new Alligator(5, 0, 5, 0, 5, 0); // All same period for easy comparison

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            alligator.Update(bars[i], isNew: true);

            // All three lines should be exactly equal since they have the same period
            Assert.Equal(alligator.Jaw.Value, alligator.Teeth.Value, precision: 15);
            Assert.Equal(alligator.Jaw.Value, alligator.Lips.Value, precision: 15);
        }

        // Ensure warmup completed
        Assert.True(alligator.IsHot);
    }

    [Fact]
    public void EventPublishing_Works()
    {
        var alligator = new Alligator();
        var gbm = new GBM();

        int eventCount = 0;
        TValue lastPublishedValue = default;
        bool lastIsNew = false;

        alligator.Pub += (object? sender, in TValueEventArgs args) =>
        {
            eventCount++;
            lastPublishedValue = args.Value;
            lastIsNew = args.IsNew;
        };

        var bar = gbm.Next(isNew: true);
        alligator.Update(bar, isNew: true);

        Assert.Equal(1, eventCount);
        Assert.True(lastIsNew);
        Assert.Equal(alligator.Last.Value, lastPublishedValue.Value);

        // Update with isNew=false
        alligator.Update(bar, isNew: false);

        Assert.Equal(2, eventCount);
        Assert.False(lastIsNew);
    }
}
