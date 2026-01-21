using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Decaychannel - internal consistency tests only.
/// No external library validation available (N/A in TA-Lib, Skender, Tulip, Ooples).
/// </summary>
public sealed class DecaychannelValidationTests : IDisposable
{
    private readonly GBM _gbm;
    private readonly TBarSeries _series;
    private const int Period = 14;
    private const int DataLength = 500;

    public DecaychannelValidationTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        _series = new TBarSeries();
        for (int i = 0; i < DataLength; i++)
        {
            _series.Add(_gbm.Next(isNew: true));
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Streaming_Batch_Span_Consistency()
    {
        // Streaming mode
        var dStream = new Decaychannel(Period);
        for (int i = 0; i < _series.Count; i++)
        {
            dStream.Update(_series[i], isNew: true);
        }

        // Batch mode (TBarSeries overload)
        var (midBatch, upBatch, loBatch) = Decaychannel.Batch(_series, Period);

        // Span mode
        int len = _series.Count;
        double[] midSpan = new double[len];
        double[] upSpan = new double[len];
        double[] loSpan = new double[len];
        Decaychannel.Batch(_series.HighValues, _series.LowValues, midSpan.AsSpan(), upSpan.AsSpan(), loSpan.AsSpan(), Period);

        // Compare last 100 values across all modes
        int checkStart = len - 100;
        for (int i = checkStart; i < len; i++)
        {
            Assert.Equal(midBatch[i].Value, midSpan[i], 1e-10);
            Assert.Equal(upBatch[i].Value, upSpan[i], 1e-10);
            Assert.Equal(loBatch[i].Value, loSpan[i], 1e-10);
        }

        // Streaming vs batch final values
        Assert.Equal(dStream.Last.Value, midBatch.Last.Value, 1e-10);
        Assert.Equal(dStream.Upper.Value, upBatch.Last.Value, 1e-10);
        Assert.Equal(dStream.Lower.Value, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void AllModes_ProduceFiniteResults()
    {
        // Streaming
        var dStream = new Decaychannel(Period);
        for (int i = 0; i < _series.Count; i++)
        {
            var result = dStream.Update(_series[i], isNew: true);
            if (i >= Period - 1)
            {
                Assert.True(double.IsFinite(result.Value), $"Streaming: bar {i} should be finite");
                Assert.True(double.IsFinite(dStream.Upper.Value), $"Streaming upper: bar {i} should be finite");
                Assert.True(double.IsFinite(dStream.Lower.Value), $"Streaming lower: bar {i} should be finite");
            }
        }

        // Batch
        var (mid, up, lo) = Decaychannel.Batch(_series, Period);
        for (int i = Period - 1; i < mid.Count; i++)
        {
            Assert.True(double.IsFinite(mid[i].Value), $"Batch middle: index {i} should be finite");
            Assert.True(double.IsFinite(up[i].Value), $"Batch upper: index {i} should be finite");
            Assert.True(double.IsFinite(lo[i].Value), $"Batch lower: index {i} should be finite");
        }
    }

    [Fact]
    public void BandOrdering_AlwaysValid()
    {
        var d = new Decaychannel(Period);
        for (int i = 0; i < _series.Count; i++)
        {
            d.Update(_series[i], isNew: true);

            Assert.True(d.Upper.Value >= d.Lower.Value, $"Bar {i}: Upper >= Lower");
            Assert.True(d.Last.Value >= d.Lower.Value - 1e-10, $"Bar {i}: Middle >= Lower");
            Assert.True(d.Last.Value <= d.Upper.Value + 1e-10, $"Bar {i}: Middle <= Upper");
        }
    }

    [Fact]
    public void Calculate_Static_ReturnsValidIndicator()
    {
        var ((mid, up, lo), indicator) = Decaychannel.Calculate(_series, Period);

        Assert.True(indicator.IsHot);
        Assert.Equal(mid.Count, _series.Count);
        Assert.Equal(up.Count, _series.Count);
        Assert.Equal(lo.Count, _series.Count);

        // Can continue streaming
        var newBar = _gbm.Next(isNew: true);
        indicator.Update(newBar, isNew: true);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void DecayBehavior_ChannelContractsWithoutNewExtremes()
    {
        var d = new Decaychannel(Period);

        // Prime with data
        for (int i = 0; i < Period * 2; i++)
        {
            d.Update(_series[i], isNew: true);
        }

        double widthBefore = d.Upper.Value - d.Lower.Value;

        // Feed flat bars at midpoint
        double mid = (d.Upper.Value + d.Lower.Value) * 0.5;
        for (int i = 0; i < Period; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, mid, mid, mid, mid, 1000), isNew: true);
        }

        double widthAfter = d.Upper.Value - d.Lower.Value;

        Assert.True(widthAfter < widthBefore, "Channel should contract when no new extremes");
    }

    [Fact]
    public void BarCorrection_RestoresState()
    {
        var d = new Decaychannel(Period);

        // Build up state
        for (int i = 0; i < 50; i++)
        {
            d.Update(_series[i], isNew: true);
        }

        double midBefore = d.Last.Value;
        double upBefore = d.Upper.Value;
        double loBefore = d.Lower.Value;

        // Apply correction
        var correctionBar = new TBar(DateTime.UtcNow, 200, 250, 150, 200, 1000);
        d.Update(correctionBar, isNew: false);

        // Values should change
        Assert.NotEqual(midBefore, d.Last.Value);

        // Apply another correction back to original-ish
        d.Update(_series[49], isNew: false);

        // Should restore
        Assert.Equal(midBefore, d.Last.Value, 1e-10);
        Assert.Equal(upBefore, d.Upper.Value, 1e-10);
        Assert.Equal(loBefore, d.Lower.Value, 1e-10);
    }

    [Fact]
    public void Event_FiresOnUpdate()
    {
        var src = new TBarSeries();
        var d = new Decaychannel(src, Period);

        int eventCount = 0;
        d.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            src.Add(_series[i]);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void MultiPeriod_Consistency()
    {
        int[] periods = [5, 10, 20, 50];

        foreach (int period in periods)
        {
            var d = new Decaychannel(period);
            for (int i = 0; i < _series.Count; i++)
            {
                d.Update(_series[i], isNew: true);
            }

            Assert.True(d.IsHot, $"Period {period} should be hot");
            Assert.True(double.IsFinite(d.Last.Value), $"Period {period} middle should be finite");
            Assert.True(d.Upper.Value >= d.Lower.Value, $"Period {period} upper >= lower");
        }
    }
}
