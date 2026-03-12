// Vo: Mathematical property validation tests
// Volume Oscillator compares short and long SMAs of volume.
// No standard external library equivalents with matching implementation.
// Validation uses mathematical property testing.

using Tulip;

namespace QuanTAlib.Tests;

using Xunit;

public class VoValidationTests
{
    private const int DefaultShortPeriod = 5;
    private const int DefaultLongPeriod = 10;
    private const int DefaultSignalPeriod = 10;
    private const int TestDataLength = 500;

    [Fact]
    public void Vo_Output_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vo.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Vo output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Vo_ConstantVolume_ZeroOscillator()
    {
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        // Feed bars with identical volume
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, 1000); // constant volume
            vo.Update(bar, isNew: true);
        }

        // When volume is constant, short MA == long MA, VO = 0
        Assert.Equal(0.0, vo.Last.Value, precision: 8);
    }

    [Fact]
    public void Vo_IncreasingVolume_PositiveOscillator()
    {
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        // Feed bars with steadily increasing volume
        for (int i = 0; i < 50; i++)
        {
            double volume = 1000 + i * 100; // increasing
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, volume);
            vo.Update(bar, isNew: true);
        }

        // Short MA should be higher than long MA when volume is increasing
        Assert.True(vo.Last.Value > 0,
            $"VO should be positive with increasing volume, got {vo.Last.Value}");
    }

    [Fact]
    public void Vo_DecreasingVolume_NegativeOscillator()
    {
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        // Feed bars with steadily decreasing volume
        for (int i = 0; i < 50; i++)
        {
            double volume = 10000 - i * 100; // decreasing
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, volume);
            vo.Update(bar, isNew: true);
        }

        // Short MA should be lower than long MA when volume is decreasing
        Assert.True(vo.Last.Value < 0,
            $"VO should be negative with decreasing volume, got {vo.Last.Value}");
    }

    [Fact]
    public void Vo_Signal_IsFinite()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            vo.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(vo.Signal),
                $"Signal must be finite at bar {i}, got {vo.Signal}");
        }
    }

    [Fact]
    public void Vo_ConstantVolume_SignalAlsoZero()
    {
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, 1000);
            vo.Update(bar, isNew: true);
        }

        // Signal is SMA of VO values, all of which are zero
        Assert.Equal(0.0, vo.Signal, precision: 8);
    }

    [Fact]
    public void Vo_BatchAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Vo.Batch(bars, DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        // Streaming
        var streamVo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVo.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 8);
        }
    }

    [Fact]
    public void Vo_DifferentPeriods_ProduceDifferentResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vo1 = new Vo(3, 7, 5);
        var vo2 = new Vo(10, 30, 15);

        for (int i = 0; i < bars.Count; i++)
        {
            vo1.Update(bars[i], isNew: true);
            vo2.Update(bars[i], isNew: true);
        }

        Assert.NotEqual(vo1.Last.Value, vo2.Last.Value);
    }

    [Fact]
    public void Vo_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        for (int i = 0; i < 30; i++)
        {
            vo.Update(bars[i], isNew: true);
        }

        vo.Update(bars[30], isNew: true);
        double afterNew = vo.Last.Value;

        vo.Update(bars[30], isNew: false);
        double afterCorrection = vo.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }

    [Fact]
    public void Vo_IsHot_AfterLongPeriod()
    {
        var vo = new Vo(DefaultShortPeriod, DefaultLongPeriod, DefaultSignalPeriod);

        for (int i = 0; i < DefaultLongPeriod - 1; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 101, 99, 100, 1000);
            vo.Update(bar, isNew: true);
            Assert.False(vo.IsHot, $"Should not be hot at bar {i}");
        }

        // Bar at index longPeriod-1 should make it hot (Index becomes longPeriod)
        var finalBar = new TBar(DateTime.UtcNow.AddMinutes(DefaultLongPeriod), 100, 101, 99, 100, 1000);
        vo.Update(finalBar, isNew: true);
        Assert.True(vo.IsHot, "Should be hot after longPeriod bars");
    }

    // === Tulip Cross-Validation ===

    /// <summary>
    /// Structural validation against Tulip <c>vosc</c> (volume oscillator).
    /// Algorithm variant: Tulip <c>vosc</c> takes one input (volume only) with two options
    /// (short_period, long_period) and computes <c>(sma_short - sma_long) / sma_long × 100</c>.
    /// QuanTAlib Vo also adds an optional signal EMA. With <c>signalPeriod=1</c> the signal
    /// equals Vo itself, so raw Vo output is directly comparable to Tulip vosc.
    /// </summary>
    [Fact]
    public void Vo_Matches_Tulip_Vosc_Batch()
    {
        const int shortPeriod = 5;
        const int longPeriod = 10;
        var bars = new GBM(sigma: 0.3, seed: 42).Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] volumeData = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++) { volumeData[i] = bars[i].Volume; }

        // QuanTAlib Vo batch
        var qResult = Vo.Batch(bars, shortPeriod, longPeriod, signalPeriod: 1);

        // Tulip vosc — volume only, no signal period
        var tulipIndicator = Tulip.Indicators.vosc;
        double[][] inputs = { volumeData };
        double[] options = { shortPeriod, longPeriod };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[volumeData.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tResult, lookback);
    }

    [Fact]
    public void Vo_Matches_Tulip_Vosc_Streaming()
    {
        const int shortPeriod = 5;
        const int longPeriod = 10;
        var bars = new GBM(sigma: 0.3, seed: 42).Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] volumeData = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++) { volumeData[i] = bars[i].Volume; }

        // QuanTAlib Vo streaming (signalPeriod=1 → signal equals Vo)
        var vo = new Vo(shortPeriod, longPeriod, signalPeriod: 1);
        var qResults = new List<double>();
        foreach (var bar in bars) { qResults.Add(vo.Update(bar).Value); }

        // Tulip vosc
        var tulipIndicator = Tulip.Indicators.vosc;
        double[][] inputs = { volumeData };
        double[] options = { shortPeriod, longPeriod };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[volumeData.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tResult, lookback);
    }
}
