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
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (Math.Sin(i * 0.1) * 5)));
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

    #region Coverage Gap Tests

    [Fact]
    public void ChainedConstructor_ReceivesUpdates()
    {
        var source = new TSeries();
        var phasor = new HtPhasor(source);

        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (Math.Sin(i * 0.2) * 10)));
        }

        Assert.True(phasor.IsHot);
        Assert.True(double.IsFinite(phasor.Last.Value));
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        double valueAfterNew = phasor.Last.Value;

        phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 999.0), isNew: false);
        double valueAfterCorrection = phasor.Last.Value;

        Assert.Equal(valueAfterNew, valueAfterCorrection, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_AtStart_CoversWmaPath()
    {
        var phasor = new HtPhasor();

        phasor.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        var result = phasor.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_NaNAsFirstInput_ReturnsNaN()
    {
        var phasor = new HtPhasor();
        var result = phasor.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_NaNAfterValid_SubstitutesLastValid()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var result = phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityAfterValid_SubstitutesLastValid()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var result = phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void UpdateTSeries_ProcessesAllBars()
    {
        var phasor = new HtPhasor();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        TSeries result = phasor.Update(tSeries);

        Assert.Equal(100, result.Count);
        Assert.True(phasor.IsHot);
    }

    [Fact]
    public void UpdateTSeries_EmptySource_ReturnsEmpty()
    {
        var phasor = new HtPhasor();
        var emptySource = new TSeries();

        TSeries result = phasor.Update(emptySource);

        Assert.Empty(result);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var phasor1 = new HtPhasor();
        var phasor2 = new HtPhasor();

        double[] data = new double[50];
        for (int i = 0; i < 50; i++)
        {
            data[i] = 100.0 + (Math.Sin(i * 0.3) * 10);
        }

        phasor1.Prime(data);

        foreach (double val in data)
        {
            phasor2.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(phasor2.Last.Value, phasor1.Last.Value, Tolerance);
    }

    [Fact]
    public void BatchTSeries_ReturnsResults()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        TSeries result = HtPhasor.Batch(tSeries);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void BatchSpan_EmptyInput_ReturnsWithoutError()
    {
        double[] source = [];
        double[] inPhase = [];
        double[] quad = [];

        var ex = Record.Exception(() => HtPhasor.Batch(source, inPhase, quad));
        Assert.Null(ex);
    }

    [Fact]
    public void Calculate_ReturnsTupleWithResultsAndIndicator()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = HtPhasor.Calculate(tSeries);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(phasor.IsHot);

        phasor.Reset();

        Assert.False(phasor.IsHot);
        Assert.Equal(default, phasor.Last);
        Assert.Equal(0.0, phasor.Quadrature);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var phasor = new HtPhasor();

        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 200.0), isNew: true);
        double afterNew = phasor.Last.Value;

        phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 250.0), isNew: false);
        phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 300.0), isNew: false);
        phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 200.0), isNew: false);

        Assert.Equal(afterNew, phasor.Last.Value, Tolerance);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var phasor = new HtPhasor();
        for (int i = 0; i < 50; i++)
        {
            phasor.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var ex = Record.Exception(() => phasor.Dispose());
        Assert.Null(ex);
    }

    #endregion
}
