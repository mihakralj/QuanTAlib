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
}
