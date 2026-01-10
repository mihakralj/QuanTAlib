using System;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class NotchTests
{
    private readonly GBM _gbm;

    public NotchTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(10, -0.5));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var notch = new Notch(period: 10);
        var result = notch.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        double q = 1.0;
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Static Calculate (TSeries)
        var staticResult = Notch.Calculate(series, period, q);

        // 2. Static Calculate (Span)
        double[] spanResult = new double[series.Count];
        Notch.Calculate(series.Values, spanResult.AsSpan(), period, q);

        // 3. Instance Update (TSeries)
        var instance = new Notch(period, q);
        var instanceSeriesResult = instance.Update(series);

        // 4. Instance Update (Streaming)
        var streamingInstance = new Notch(period, q);
        double[] streamingResult = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamingResult[i] = streamingInstance.Update(series[i]).Value;
        }

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(staticResult[i].Value, spanResult[i], precision: 9);
            Assert.Equal(staticResult[i].Value, instanceSeriesResult[i].Value, precision: 9);
            Assert.Equal(staticResult[i].Value, streamingResult[i], precision: 9);
        }
    }

    [Fact]
    public void Notch_Passes_DC()
    {
        // DC input (constant value) should pass through with Gain 1
        var notch = new Notch(period: 10, q: 1.0);
        double input = 100.0;
        double output = 0;

        // Warmup to stabilize (IIR transient)
        for(int i=0; i<100; i++)
        {
            output = notch.Update(new TValue(DateTime.UtcNow, input)).Value;
        }

        Assert.Equal(input, output, precision: 6);
    }

    [Fact]
    public void Notch_Attenuates_CenterFrequency()
    {
        // Period 10 means frequency is 1/10 cycles per sample.
        // theta = 2*pi/10
        int period = 10;
        double q = 5.0; // High Q for sharp notch
        var notch = new Notch(period, q);

        double omega = 2.0 * Math.PI / period;

        double maxAmp = 0;
        for (int i = 0; i < 200; i++)
        {
            double val = Math.Sin(omega * i); // Input amplitude 1
            double outVal = notch.Update(new TValue(DateTime.UtcNow, val)).Value;

            if (i > 50) // ignore transient
            {
               maxAmp = Math.Max(maxAmp, Math.Abs(outVal));
            }
        }

        // At exact notch frequency, ideal is 0.
        // With Q=5, it should be very small.
        Assert.True(maxAmp < 0.1, $"Amplitude {maxAmp} should be attenuated ( < 0.1 )");
    }
}