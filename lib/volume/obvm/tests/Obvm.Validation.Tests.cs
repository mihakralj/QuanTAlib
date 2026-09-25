using Xunit;

namespace QuanTAlib.Tests;

public sealed class ObvmValidationTests : IDisposable
{
    private readonly Obvm _obvm;
    private readonly TBarSeries _bars;

    public ObvmValidationTests()
    {
        _obvm = new Obvm();
        GBM gbm = new();
        _bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        _obvm.Reset();
    }

    [Fact]
    public void Streaming_Equals_Batch()
    {
        // Streaming
        double[] streamObvm = new double[_bars.Count];
        double[] streamSignal = new double[_bars.Count];
        for (int i = 0; i < _bars.Count; i++)
        {
            _obvm.Update(_bars[i]);
            streamObvm[i] = _obvm.ObvmValue.Value;
            streamSignal[i] = _obvm.Signal.Value;
        }

        // Batch
        var (batchObvm, batchSignal) = Obvm.Batch(_bars);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(streamObvm[i], batchObvm.Values[i], 6);
            Assert.Equal(streamSignal[i], batchSignal.Values[i], 6);
        }
    }

    [Fact]
    public void Span_Equals_Streaming()
    {
        // Streaming
        _obvm.Reset();
        double[] streamObvm = new double[_bars.Count];
        double[] streamSignal = new double[_bars.Count];
        for (int i = 0; i < _bars.Count; i++)
        {
            _obvm.Update(_bars[i]);
            streamObvm[i] = _obvm.ObvmValue.Value;
            streamSignal[i] = _obvm.Signal.Value;
        }

        // Span batch
        double[] obvmOut = new double[_bars.Count];
        double[] signalOut = new double[_bars.Count];
        Obvm.Batch(_bars.CloseValues, _bars.VolumeValues, obvmOut, signalOut);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(streamObvm[i], obvmOut[i], 6);
            Assert.Equal(streamSignal[i], signalOut[i], 6);
        }
    }

    [Fact]
    public void ConstantPriceInput_ProducesZero()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 50, 50, 50, 50, 1000));
        }

        var obvm = new Obvm();
        for (int i = 0; i < bars.Count; i++)
        {
            obvm.Update(bars[i]);
        }

        Assert.Equal(0, obvm.ObvmValue.Value);
        Assert.Equal(0, obvm.Signal.Value);
    }

    [Fact]
    public void KnownValues_ManualOBVCalculation()
    {
        var time = DateTime.UtcNow;
        // alpha = 2/(3+1) = 0.5
        var obvm = new Obvm(obvmLength: 3, signalLength: 3);

        // Bar 0: close=100, vol=1000 → OBV=0
        // OBVM seeded = OBV = 0 (NaN branch), Signal seeded = OBVM = 0
        obvm.Update(new TBar(time, 100, 101, 99, 100, 1000));
        Assert.Equal(0, obvm.ObvmValue.Value);
        Assert.Equal(0, obvm.Signal.Value);

        // Bar 1: close=105 > 100 → OBV = 0 + 1000 = 1000
        // OBVM = FMA(0.5, 1000 - 0, 0) = 500
        // Signal = FMA(0.5, 500 - 0, 0) = 250
        obvm.Update(new TBar(time.AddMinutes(1), 104, 106, 99, 105, 1000));
        Assert.Equal(500, obvm.ObvmValue.Value, 1);
        Assert.Equal(250, obvm.Signal.Value, 1);

        // Bar 2: close=102 < 105 → OBV = 1000 - 2000 = -1000
        // OBVM = FMA(0.5, -1000 - 500, 500) = 500 + 0.5*(-1500) = -250
        // Signal = FMA(0.5, -250 - 250, 250) = 250 + 0.5*(-500) = 0
        obvm.Update(new TBar(time.AddMinutes(2), 103, 106, 101, 102, 2000));
        Assert.Equal(-250, obvm.ObvmValue.Value, 1);
        Assert.Equal(0, obvm.Signal.Value, 1);
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var obvm7 = new Obvm(7, 10);
        var obvm14 = new Obvm(14, 20);

        for (int i = 0; i < _bars.Count; i++)
        {
            obvm7.Update(_bars[i]);
            obvm14.Update(_bars[i]);
        }

        // Larger periods produce more smoothing → different values
        Assert.NotEqual(obvm7.ObvmValue.Value, obvm14.ObvmValue.Value);
        Assert.NotEqual(obvm7.Signal.Value, obvm14.Signal.Value);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var ((resultObvm, resultSignal), indicator) = Obvm.Calculate(_bars);

        Assert.True(indicator.IsHot);
        Assert.Equal(_bars.Count, resultObvm.Count);
        Assert.Equal(_bars.Count, resultSignal.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        var obvm1 = new Obvm();
        var obvm2 = new Obvm();

        // Both process same history
        for (int i = 0; i < 50; i++)
        {
            obvm1.Update(_bars[i]);
            obvm2.Update(_bars[i]);
        }

        // obvm1: gets a new bar + correction
        obvm1.Update(_bars[50], isNew: true);
        obvm1.Update(_bars[50], isNew: false);

        // obvm2: only processes the bar once
        obvm2.Update(_bars[50], isNew: true);

        Assert.Equal(obvm2.ObvmValue.Value, obvm1.ObvmValue.Value, 10);
        Assert.Equal(obvm2.Signal.Value, obvm1.Signal.Value, 10);
    }
}
