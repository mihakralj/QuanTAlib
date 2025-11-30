using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class WmaVectorTests
{
    [Fact]
    public void Initialization_WithPeriods_Works()
    {
        int[] periods = { 5, 10, 20 };
        var wmaVector = new WmaVector(periods);

        var res = wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(3, res.Length);
        Assert.Equal(100.0, res[0].Value, 1e-9);
        Assert.Equal(100.0, res[1].Value, 1e-9);
        Assert.Equal(100.0, res[2].Value, 1e-9);
    }

    [Fact]
    public void Initialization_WithZeroPeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, 0, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new WmaVector(periods));
    }

    [Fact]
    public void Initialization_WithNegativePeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, -5, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new WmaVector(periods));
    }

    [Fact]
    public void Calc_Streaming_MatchesSingleWma()
    {
        int[] periods = { 5, 10, 20 };
        var wmaVector = new WmaVector(periods);
        var wmaSingles = periods.Select(p => new Wma(p)).ToArray();

        var values = new double[] { 10, 20, 30, 40, 50, 40, 30, 20, 10 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = wmaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = wmaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
                Assert.Equal(singleRes.Time, multiRes[i].Time);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Calc_Series_MatchesSingleWma()
    {
        int[] periods = { 5, 10, 20 };
        var wmaVector = new WmaVector(periods);

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

        var multiRes = wmaVector.Calculate(series);

        // Reset and recalculate for comparison
        var wmaSingles = periods.Select(p => new Wma(p)).ToArray();
        for (int j = 0; j < len; j++)
        {
            var tVal = new TValue(new DateTime(t[j], DateTimeKind.Utc), v[j]);
            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = wmaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Values[j], 1e-8);
            }
        }
    }

    [Fact]
    public void Calc_Series_MatchesStreaming()
    {
        int[] periods = { 5, 10, 20 };
        var wmaVectorBatch = new WmaVector(periods);
        var wmaVectorStream = new WmaVector(periods);

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

        var batchRes = wmaVectorBatch.Calculate(series);

        for (int i = 0; i < len; i++)
        {
            var tVal = new TValue(new DateTime(t[i], DateTimeKind.Utc), v[i]);
            var streamRes = wmaVectorStream.Update(tVal);

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

        var instanceWma = new WmaVector(periods);
        var instanceRes = instanceWma.Calculate(series);

        var staticRes = WmaVector.Calculate(series, periods);

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
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, 200.0));
        wmaVector.Reset();

        var res = wmaVector.Update(new TValue(DateTime.UtcNow, 50.0));

        Assert.Equal(50.0, res[0].Value, 1e-9);
    }

    [Fact]
    public void Update_NaN_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterNaN = wmaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in resultAfterNaN)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Update_Infinity_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterPosInf = wmaVector.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        foreach (var result in resultAfterPosInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }

        var resultAfterNegInf = wmaVector.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        foreach (var result in resultAfterNegInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Update_MultipleNaN_ContinuesWithLastValid()
    {
        int[] periods = { 5, 10 };
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, 110.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, 120.0));

        var r1 = wmaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = wmaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = wmaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in r1) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r2) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r3) Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calculate_Series_HandlesNaN()
    {
        int[] periods = { 5, 10 };
        var wmaVector = new WmaVector(periods);

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
        var results = wmaVector.Calculate(series);

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
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        wmaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        wmaVector.Reset();

        var result = wmaVector.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, result[0].Value, 1e-9);
    }

    [Fact]
    public void NaN_Handling_MatchesSingleWma()
    {
        int[] periods = { 5, 10, 20 };
        var wmaVector = new WmaVector(periods);
        var wmaSingles = periods.Select(p => new Wma(p)).ToArray();

        var values = new double[] { 10, 20, double.NaN, 40, double.PositiveInfinity, 60, 70 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = wmaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = wmaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Values_Property_UpdatesAfterUpdate()
    {
        int[] periods = { 5, 10 };
        var wmaVector = new WmaVector(periods);

        var result = wmaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(result[0].Value, wmaVector.Values[0].Value);
        Assert.Equal(result[1].Value, wmaVector.Values[1].Value);
    }

    [Fact]
    public void Values_Property_UpdatesAfterCalculate()
    {
        int[] periods = { 5, 10 };
        var wmaVector = new WmaVector(periods);

        var t = new System.Collections.Generic.List<long> { 100, 200, 300 };
        var v = new System.Collections.Generic.List<double> { 10.0, 20.0, 30.0 };
        var series = new TSeries(t, v);

        var results = wmaVector.Calculate(series);

        Assert.Equal(results[0].Last.Value, wmaVector.Values[0].Value, 1e-9);
        Assert.Equal(results[1].Last.Value, wmaVector.Values[1].Value, 1e-9);
    }

    [Fact]
    public void Update_BarCorrection_WorksCorrectly()
    {
        int[] periods = { 3 };
        var wmaVector = new WmaVector(periods);

        wmaVector.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        wmaVector.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        wmaVector.Update(new TValue(DateTime.UtcNow, 30.0), isNew: true);

        // WMA(3) of 10,20,30 = (1*10 + 2*20 + 3*30) / 6 = 140/6 = 23.333...
        var res1 = wmaVector.Values[0].Value;
        Assert.Equal(140.0 / 6.0, res1, 1e-9);

        // Correct the last bar to 60
        var res2 = wmaVector.Update(new TValue(DateTime.UtcNow, 60.0), isNew: false);

        // WMA(3) of 10,20,60 = (1*10 + 2*20 + 3*60) / 6 = (10 + 40 + 180) / 6 = 230/6 = 38.333...
        Assert.Equal(230.0 / 6.0, res2[0].Value, 1e-9);
    }

    [Fact]
    public void WMA_MatchesExpectedValues()
    {
        int[] periods = { 3 };
        var wmaVector = new WmaVector(periods);

        // Test sequence: 10, 20, 30, 40, 50
        // WMA(3) weights: [1, 2, 3], divisor = 6
        // Bar 1: 10 (only value) = 10
        // Bar 2: (1*10 + 2*20) / 3 = 50/3 = 16.666...
        // Bar 3: (1*10 + 2*20 + 3*30) / 6 = 140/6 = 23.333...
        // Bar 4: (1*20 + 2*30 + 3*40) / 6 = 200/6 = 33.333...
        // Bar 5: (1*30 + 2*40 + 3*50) / 6 = 260/6 = 43.333...
        double[] expected = [10.0, 50.0/3.0, 140.0/6.0, 200.0/6.0, 260.0/6.0];
        var values = new double[] { 10, 20, 30, 40, 50 };
        var time = DateTime.UtcNow;

        for (int i = 0; i < values.Length; i++)
        {
            var res = wmaVector.Update(new TValue(time, values[i]));
            Assert.Equal(expected[i], res[0].Value, 1e-9);
            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void WMA_MoreWeightOnRecentValues()
    {
        int[] periods = { 3 };
        var wmaVector = new WmaVector(periods);
        var smaVector = new SmaVector(periods);

        var values = new double[] { 10, 20, 100 };  // High recent value
        var time = DateTime.UtcNow;

        TValue[] wmaRes = null!;
        TValue[] smaRes = null!;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            wmaRes = wmaVector.Update(tVal);
            smaRes = smaVector.Update(tVal);
            time = time.AddMinutes(1);
        }

        // WMA should be higher than SMA because it weights the high recent value more
        // SMA = (10 + 20 + 100) / 3 = 43.333...
        // WMA = (1*10 + 2*20 + 3*100) / 6 = (10 + 40 + 300) / 6 = 58.333...
        Assert.True(wmaRes[0].Value > smaRes[0].Value);
        Assert.Equal(350.0 / 6.0, wmaRes[0].Value, 1e-9);
        Assert.Equal(130.0 / 3.0, smaRes[0].Value, 1e-9);
    }
}
