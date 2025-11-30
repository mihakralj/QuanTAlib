using System;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class EmaVectorTests
{
    [Fact]
    public void Initialization_WithPeriods_SetsCorrectAlphas()
    {
        int[] periods = { 10, 20 };
        var emaVector = new EmaVector(periods);

        var res = emaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(100.0, res[0].Value, 1e-9);
        Assert.Equal(100.0, res[1].Value, 1e-9);
    }

    [Fact]
    public void Initialization_WithAlphas_Works()
    {
        double[] alphas = { 0.1, 0.2, 0.5 };
        var emaVector = new EmaVector(alphas);

        var res = emaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(3, res.Length);
        Assert.Equal(100.0, res[0].Value, 1e-9);
        Assert.Equal(100.0, res[1].Value, 1e-9);
        Assert.Equal(100.0, res[2].Value, 1e-9);
    }

    [Fact]
    public void Initialization_WithZeroPeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, 0, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaVector(periods));
    }

    [Fact]
    public void Initialization_WithNegativePeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, -5, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaVector(periods));
    }

    [Fact]
    public void Initialization_WithZeroAlpha_ThrowsArgumentException()
    {
        double[] alphas = { 0.1, 0.0, 0.5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaVector(alphas));
    }

    [Fact]
    public void Initialization_WithNegativeAlpha_ThrowsArgumentException()
    {
        double[] alphas = { 0.1, -0.1, 0.5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaVector(alphas));
    }

    [Fact]
    public void Initialization_WithAlphaGreaterThanOne_ThrowsArgumentException()
    {
        double[] alphas = { 0.1, 1.5, 0.5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new EmaVector(alphas));
    }

    [Fact]
    public void Initialization_WithAlphaEqualToOne_Works()
    {
        double[] alphas = { 0.1, 1.0, 0.5 };
        var emaVector = new EmaVector(alphas);

        var res = emaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(3, res.Length);
    }

    [Fact]
    public void Calc_Streaming_MatchesSingleEma()
    {
        int[] periods = { 5, 10, 20 };
        var emaVector = new EmaVector(periods);
        var emaSingles = periods.Select(p => new Ema(p)).ToArray();

        var values = new double[] { 10, 20, 30, 40, 50, 40, 30, 20, 10 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = emaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = emaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
                Assert.Equal(singleRes.Time, multiRes[i].Time);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Calc_Series_MatchesSingleEma()
    {
        int[] periods = { 5, 10, 20 };
        var emaVector = new EmaVector(periods);
        var emaSingles = periods.Select(p => new Ema(p)).ToArray();

        int len = 100;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);
        var now = DateTime.UtcNow;

        for (int i = 0; i < len; i++)
        {
            t.Add(now.AddMinutes(i).Ticks);
            v.Add(Math.Sin(i * 0.1) * 100);
        }

        var series = new TSeries(t, v);

        var multiRes = emaVector.Calculate(series);

        for (int i = 0; i < periods.Length; i++)
        {
            var singleRes = emaSingles[i].Update(series);

            Assert.Equal(singleRes.Count, multiRes[i].Count);
            for (int j = 0; j < len; j++)
            {
                Assert.Equal(singleRes.Values[j], multiRes[i].Values[j], 1e-8);
            }
        }
    }

    [Fact]
    public void Calc_Series_MatchesStreaming()
    {
        int[] periods = { 5, 10, 20 };
        var emaVectorBatch = new EmaVector(periods);
        var emaVectorStream = new EmaVector(periods);

        int len = 100;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);
        var now = DateTime.UtcNow;

        for (int i = 0; i < len; i++)
        {
            t.Add(now.AddMinutes(i).Ticks);
            v.Add(Math.Sin(i * 0.1) * 100);
        }

        var series = new TSeries(t, v);

        var batchRes = emaVectorBatch.Calculate(series);

        for (int i = 0; i < len; i++)
        {
            var tVal = new TValue(new DateTime(t[i], DateTimeKind.Utc), v[i]);
            var streamRes = emaVectorStream.Update(tVal);

            for (int j = 0; j < periods.Length; j++)
            {
                Assert.Equal(batchRes[j].Values[i], streamRes[j].Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Calculate_Static_MatchesInstanceMethod()
    {
        int[] periods = { 5, 10, 20 };

        int len = 50;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);
        var now = DateTime.UtcNow;

        for (int i = 0; i < len; i++)
        {
            t.Add(now.AddMinutes(i).Ticks);
            v.Add(Math.Sin(i * 0.1) * 100);
        }

        var series = new TSeries(t, v);

        var instanceEma = new EmaVector(periods);
        var instanceRes = instanceEma.Calculate(series);

        var staticRes = EmaVector.Calculate(series, periods);

        for (int i = 0; i < periods.Length; i++)
        {
            Assert.Equal(instanceRes[i].Count, staticRes[i].Count);
            for (int j = 0; j < len; j++)
            {
                Assert.Equal(instanceRes[i].Values[j], staticRes[i].Values[j], 1e-9);
            }
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        int[] periods = { 10 };
        var emaVector = new EmaVector(periods);

        emaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        emaVector.Reset();

        var res = emaVector.Update(new TValue(DateTime.UtcNow, 200.0));

        Assert.Equal(200.0, res[0].Value, 1e-9);
    }

    [Fact]
    public void Update_NaN_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var emaVector = new EmaVector(periods);

        emaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        emaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterNaN = emaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in resultAfterNaN)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Update_Infinity_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var emaVector = new EmaVector(periods);

        emaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        emaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterPosInf = emaVector.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        foreach (var result in resultAfterPosInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }

        var resultAfterNegInf = emaVector.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        foreach (var result in resultAfterNegInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Update_MultipleNaN_ContinuesWithLastValid()
    {
        int[] periods = { 5, 10 };
        var emaVector = new EmaVector(periods);

        emaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        emaVector.Update(new TValue(DateTime.UtcNow, 110.0));
        emaVector.Update(new TValue(DateTime.UtcNow, 120.0));

        var r1 = emaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = emaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = emaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in r1) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r2) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r3) Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calculate_Series_HandlesNaN()
    {
        int[] periods = { 5, 10 };
        var emaVector = new EmaVector(periods);

        var t = new System.Collections.Generic.List<long>();
        var v = new System.Collections.Generic.List<double>();
        var now = DateTime.UtcNow;

        t.Add(now.Ticks); v.Add(100.0);
        t.Add(now.AddMinutes(1).Ticks); v.Add(110.0);
        t.Add(now.AddMinutes(2).Ticks); v.Add(double.NaN);
        t.Add(now.AddMinutes(3).Ticks); v.Add(120.0);
        t.Add(now.AddMinutes(4).Ticks); v.Add(double.PositiveInfinity);
        t.Add(now.AddMinutes(5).Ticks); v.Add(130.0);

        var series = new TSeries(t, v);
        var results = emaVector.Calculate(series);

        foreach (var periodResults in results)
        {
            foreach (var val in periodResults.Values)
            {
                Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
            }
        }
    }

    [Fact]
    public void Reset_ClearsLastValidValue()
    {
        int[] periods = { 10 };
        var emaVector = new EmaVector(periods);

        emaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        emaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        emaVector.Reset();

        var result = emaVector.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, result[0].Value, 1e-9);
    }

    [Fact]
    public void NaN_Handling_MatchesSingleEma()
    {
        int[] periods = { 5, 10, 20 };
        var emaVector = new EmaVector(periods);
        var emaSingles = periods.Select(p => new Ema(p)).ToArray();

        var values = new double[] { 10, 20, double.NaN, 40, double.PositiveInfinity, 60, 70 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = emaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = emaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Values_Property_UpdatesAfterUpdate()
    {
        int[] periods = { 5, 10 };
        var emaVector = new EmaVector(periods);

        var result = emaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(result[0].Value, emaVector.Values[0].Value);
        Assert.Equal(result[1].Value, emaVector.Values[1].Value);
    }

    [Fact]
    public void Values_Property_UpdatesAfterCalculate()
    {
        int[] periods = { 5, 10 };
        var emaVector = new EmaVector(periods);

        var t = new System.Collections.Generic.List<long> { 100, 200, 300 };
        var v = new System.Collections.Generic.List<double> { 10.0, 20.0, 30.0 };
        var series = new TSeries(t, v);

        var results = emaVector.Calculate(series);

        Assert.Equal(results[0].Last.Value, emaVector.Values[0].Value, 1e-9);
        Assert.Equal(results[1].Last.Value, emaVector.Values[1].Value, 1e-9);
    }
}
