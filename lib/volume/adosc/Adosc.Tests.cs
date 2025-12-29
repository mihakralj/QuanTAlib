using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class AdoscTests
{
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;

    public AdoscTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        _bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Adosc(fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Adosc(slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Adosc(fastPeriod: 10, slowPeriod: 5));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var adosc = new Adosc(3, 10);
        var result = adosc.Update(_bars[0]);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var adosc = new Adosc(3, 10);
        Assert.Equal("Adosc(3,10)", adosc.Name);
        Assert.False(adosc.IsHot);
        Assert.Equal(10, adosc.WarmupPeriod);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0], isNew: true);
        adosc.Update(_bars[1], isNew: true);
        Assert.NotEqual(adosc.Last.Time, _bars[0].Time);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0], isNew: true);
        var firstResult = adosc.Last.Value;

        var modifiedBar = new TBar(_bars[0].Time, _bars[0].Open, _bars[0].High, _bars[0].Low, _bars[0].Close * 1.1, _bars[0].Volume);
        adosc.Update(modifiedBar, isNew: false);

        Assert.NotEqual(firstResult, adosc.Last.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0]);
        adosc.Reset();
        Assert.False(adosc.IsHot);
        Assert.Equal(0, adosc.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var adosc = new Adosc(3, 10);
        for (int i = 0; i < 20; i++)
        {
            adosc.Update(_bars[i]);
        }
        Assert.True(adosc.IsHot);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var adosc = new Adosc(3, 10);
        var batchResult = Adosc.Batch(_bars, 3, 10);

        var streamResult = new List<double>();
        foreach (var bar in _bars)
        {
            streamResult.Add(adosc.Update(bar).Value);
        }

        var spanOutput = new double[_bars.Count];
        Adosc.Calculate(_bars.High.Values, _bars.Low.Values, _bars.Close.Values, _bars.Volume.Values, spanOutput, 3, 10);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-9);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-6);
        }
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var adosc = new Adosc(3, 10);

        // Feed some valid data
        for (int i = 0; i < 15; i++)
        {
            adosc.Update(_bars[i]);
        }

        // Create a bar with NaN close
        var nanBar = new TBar(_bars[15].Time, _bars[15].Open, _bars[15].High, _bars[15].Low, double.NaN, _bars[15].Volume);
        var result = adosc.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var adosc = new Adosc(3, 10);

        // Feed some valid data
        for (int i = 0; i < 15; i++)
        {
            adosc.Update(_bars[i]);
        }

        // Create a bar with Infinity close
        var infBar = new TBar(_bars[15].Time, _bars[15].Open, _bars[15].High, _bars[15].Low, double.PositiveInfinity, _bars[15].Volume);
        var result = adosc.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var adosc = new Adosc(3, 10);

        // Feed 20 bars
        TBar bar20 = default;
        for (int i = 0; i < 20; i++)
        {
            bar20 = _bars[i];
            adosc.Update(bar20, isNew: true);
        }

        // Remember state after 20 bars
        double stateAfter20 = adosc.Last.Value;

        // Apply 5 corrections with different values
        for (int i = 0; i < 5; i++)
        {
            var correctedBar = new TBar(bar20.Time, bar20.Open * (1 + i * 0.01), bar20.High * (1 + i * 0.01),
                bar20.Low * (1 + i * 0.01), bar20.Close * (1 + i * 0.01), bar20.Volume);
            adosc.Update(correctedBar, isNew: false);
        }

        // Restore original bar
        adosc.Update(bar20, isNew: false);

        Assert.Equal(stateAfter20, adosc.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_CalculatesValidOutput()
    {
        double[] high = [100, 101, 102, 103, 104];
        double[] low = [98, 99, 100, 101, 102];
        double[] close = [99, 100, 101, 102, 103];
        double[] volume = [1000, 1100, 1200, 1300, 1400];
        double[] output = new double[5];

        Adosc.Calculate(high, low, close, volume, output, 3, 5);

        // Verify output is finite
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
        }
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var batchResult = Adosc.Batch(_bars, 3, 10);

        var spanOutput = new double[_bars.Count];
        Adosc.Calculate(_bars.High.Values, _bars.Low.Values, _bars.Close.Values, _bars.Volume.Values, spanOutput, 3, 10);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-6);
        }
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var iterativeAdosc = new Adosc(3, 10);
        var iterativeResults = new List<double>();

        foreach (var bar in _bars)
        {
            iterativeResults.Add(iterativeAdosc.Update(bar).Value);
        }

        var batchResult = Adosc.Batch(_bars, 3, 10);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResult[i].Value, 1e-10);
        }
    }
}