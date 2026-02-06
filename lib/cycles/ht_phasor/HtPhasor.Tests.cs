using Xunit;

namespace QuanTAlib.Tests;

public class HtPhasorTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Constructor_SetsProperties()
    {
        var phasor = new HtPhasor();

        Assert.Equal("HtPhasor", phasor.Name);
        Assert.False(phasor.IsHot);
        Assert.Equal(32, phasor.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithNullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HtPhasor(null!));
    }

    [Fact]
    public void Update_ReturnsFinite()
    {
        var phasor = new HtPhasor();
        var result = phasor.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var phasor = new HtPhasor();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            phasor.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(phasor.IsHot);
    }

    [Fact]
    public void Update_QuadratureAccessible()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 100; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 5));
        }

        Assert.True(double.IsFinite(phasor.Quadrature));
    }

    [Fact]
    public void Update_StreamVsBatch_Match()
    {
        const int len = 300;
        var gbm = new GBM(seed: 7);
        var bars = gbm.Fetch(len, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var stream = new HtPhasor();
        var streamI = new double[len];
        var streamQ = new double[len];
        for (int i = 0; i < len; i++)
        {
            stream.Update(new TValue(bars[i].Time, bars[i].Close));
            streamI[i] = stream.Last.Value;
            streamQ[i] = stream.Quadrature;
        }

        var source = new double[len];
        for (int i = 0; i < len; i++)
        {
            source[i] = bars[i].Close;
        }

        var batchI = new double[len];
        var batchQ = new double[len];
        HtPhasor.Batch(source, batchI, batchQ);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(streamI[i], batchI[i], Tolerance);
            Assert.Equal(streamQ[i], batchQ[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_LengthValidation()
    {
        double[] source = new double[10];
        double[] inPhase = new double[5];
        double[] quad = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => HtPhasor.Batch(source, inPhase, quad));
        Assert.Equal("inPhase", ex.ParamName);
    }

    [Fact]
    public void Batch_LengthValidationQuadrature()
    {
        double[] source = new double[10];
        double[] inPhase = new double[10];
        double[] quad = new double[5];

        var ex = Assert.Throws<ArgumentException>(() => HtPhasor.Batch(source, inPhase, quad));
        Assert.Equal("quadrature", ex.ParamName);
    }
}
