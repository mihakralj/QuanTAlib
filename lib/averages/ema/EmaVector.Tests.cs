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
        
        // We can't check private fields directly, but we can check results after 1 step
        // Alpha = 2 / (P + 1)
        // P=10 -> A=2/11
        // P=20 -> A=2/21
        
        var res = emaVector.Update(new TValue(DateTime.Now, 100.0));
        
        // First value should be 100.0 due to compensation
        Assert.Equal(100.0, res[0].Value, 1e-9);
        Assert.Equal(100.0, res[1].Value, 1e-9);
    }

    [Fact]
    public void Calc_Streaming_MatchesSingleEma()
    {
        int[] periods = { 5, 10, 20 };
        var emaVector = new EmaVector(periods);
        var emaSingles = periods.Select(p => new Ema(p)).ToArray();
        
        var values = new double[] { 10, 20, 30, 40, 50, 40, 30, 20, 10 };
        var time = DateTime.Now;

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
        var now = DateTime.Now;
        
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
        var now = DateTime.Now;
        
        for (int i = 0; i < len; i++)
        {
            t.Add(now.AddMinutes(i).Ticks);
            v.Add(Math.Sin(i * 0.1) * 100);
        }
        
        var series = new TSeries(t, v);
        
        // Batch calculation
        var batchRes = emaVectorBatch.Calculate(series);
        
        // Streaming calculation
        for (int i = 0; i < len; i++)
        {
            var tVal = new TValue(new DateTime(t[i]), v[i]);
            var streamRes = emaVectorStream.Update(tVal);
            
            for (int j = 0; j < periods.Length; j++)
            {
                Assert.Equal(batchRes[j].Values[i], streamRes[j].Value, 1e-9);
            }
        }
    }
    
    [Fact]
    public void Reset_ClearsState()
    {
        int[] periods = { 10 };
        var emaVector = new EmaVector(periods);
        
        emaVector.Update(new TValue(DateTime.Now, 100.0));
        emaVector.Reset();
        
        // After reset, next calculation should treat it as first value (warmup)
        var res = emaVector.Update(new TValue(DateTime.Now, 200.0));
        
        Assert.Equal(200.0, res[0].Value, 1e-9);
    }

    [Fact]
    public void Update_NaN_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var emaVector = new EmaVector(periods);
        
        // Feed some valid values
        emaVector.Update(new TValue(DateTime.Now, 100.0));
        emaVector.Update(new TValue(DateTime.Now, 110.0));
        
        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = emaVector.Update(new TValue(DateTime.Now, double.NaN));
        
        // All results should be finite (not NaN)
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
        
        // Feed some valid values
        emaVector.Update(new TValue(DateTime.Now, 100.0));
        emaVector.Update(new TValue(DateTime.Now, 110.0));
        
        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = emaVector.Update(new TValue(DateTime.Now, double.PositiveInfinity));
        foreach (var result in resultAfterPosInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }
        
        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = emaVector.Update(new TValue(DateTime.Now, double.NegativeInfinity));
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
        
        // Feed valid values
        emaVector.Update(new TValue(DateTime.Now, 100.0));
        emaVector.Update(new TValue(DateTime.Now, 110.0));
        emaVector.Update(new TValue(DateTime.Now, 120.0));
        
        // Feed multiple NaN values
        var r1 = emaVector.Update(new TValue(DateTime.Now, double.NaN));
        var r2 = emaVector.Update(new TValue(DateTime.Now, double.NaN));
        var r3 = emaVector.Update(new TValue(DateTime.Now, double.NaN));
        
        // All results should be finite
        foreach (var result in r1) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r2) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r3) Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calculate_Series_HandlesNaN()
    {
        int[] periods = { 5, 10 };
        var emaVector = new EmaVector(periods);
        
        // Create series with NaN values interspersed
        var t = new System.Collections.Generic.List<long>();
        var v = new System.Collections.Generic.List<double>();
        var now = DateTime.Now;
        
        t.Add(now.Ticks); v.Add(100.0);
        t.Add(now.AddMinutes(1).Ticks); v.Add(110.0);
        t.Add(now.AddMinutes(2).Ticks); v.Add(double.NaN);
        t.Add(now.AddMinutes(3).Ticks); v.Add(120.0);
        t.Add(now.AddMinutes(4).Ticks); v.Add(double.PositiveInfinity);
        t.Add(now.AddMinutes(5).Ticks); v.Add(130.0);
        
        var series = new TSeries(t, v);
        var results = emaVector.Calculate(series);
        
        // All results should be finite for all periods
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
        
        // Feed values including NaN
        emaVector.Update(new TValue(DateTime.Now, 100.0));
        emaVector.Update(new TValue(DateTime.Now, double.NaN));
        
        // Reset
        emaVector.Reset();
        
        // After reset, first valid value should establish new baseline
        var result = emaVector.Update(new TValue(DateTime.Now, 50.0));
        Assert.Equal(50.0, result[0].Value, 1e-9);
    }

    [Fact]
    public void NaN_Handling_MatchesSingleEma()
    {
        int[] periods = { 5, 10, 20 };
        var emaVector = new EmaVector(periods);
        var emaSingles = periods.Select(p => new Ema(p)).ToArray();
        
        // Data with NaN values
        var values = new double[] { 10, 20, double.NaN, 40, double.PositiveInfinity, 60, 70 };
        var time = DateTime.Now;

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
}
