namespace QuanTAlib.Tests;

public class TrimaVectorTests
{
    [Fact]
    public void Initialization_WithPeriods_Works()
    {
        int[] periods = { 5, 10, 20 };
        var trimaVector = new TrimaVector(periods);

        var res = trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(3, res.Length);
        Assert.Equal(100.0, res[0].Value, 1e-9);
        Assert.Equal(100.0, res[1].Value, 1e-9);
        Assert.Equal(100.0, res[2].Value, 1e-9);
    }

    [Fact]
    public void Initialization_WithZeroPeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, 0, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new TrimaVector(periods));
    }

    [Fact]
    public void Initialization_WithNegativePeriod_ThrowsArgumentException()
    {
        int[] periods = { 10, -5, 20 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new TrimaVector(periods));
    }

    [Fact]
    public void Calc_Streaming_MatchesSingleTrima()
    {
        int[] periods = { 5, 10, 20 };
        var trimaVector = new TrimaVector(periods);
        var trimaSingles = periods.Select(p => new Trima(p)).ToArray();

        var values = new double[] { 10, 20, 30, 40, 50, 40, 30, 20, 10 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = trimaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = trimaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
                Assert.Equal(singleRes.Time, multiRes[i].Time);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Calc_Series_MatchesSingleTrima()
    {
        int[] periods = { 5, 10, 20 };
        var trimaVector = new TrimaVector(periods);

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

        var multiRes = trimaVector.Calculate(series);

        // Reset and recalculate for comparison
        var trimaSingles = periods.Select(p => new Trima(p)).ToArray();
        for (int j = 0; j < len; j++)
        {
            var tVal = new TValue(new DateTime(t[j], DateTimeKind.Utc), v[j]);
            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = trimaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Values[j], 1e-8);
            }
        }
    }

    [Fact]
    public void Calc_Series_MatchesStreaming()
    {
        int[] periods = { 5, 10, 20 };
        var trimaVectorBatch = new TrimaVector(periods);
        var trimaVectorStream = new TrimaVector(periods);

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

        var batchRes = trimaVectorBatch.Calculate(series);

        for (int i = 0; i < len; i++)
        {
            var tVal = new TValue(new DateTime(t[i], DateTimeKind.Utc), v[i]);
            var streamRes = trimaVectorStream.Update(tVal);

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

        var instanceTrima = new TrimaVector(periods);
        var instanceRes = instanceTrima.Calculate(series);

        var staticRes = TrimaVector.Calculate(series, periods);

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
        var trimaVector = new TrimaVector(periods);

        trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, 200.0));
        trimaVector.Reset();

        var res = trimaVector.Update(new TValue(DateTime.UtcNow, 50.0));

        Assert.Equal(50.0, res[0].Value, 1e-9);
    }

    [Fact]
    public void Update_NaN_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var trimaVector = new TrimaVector(periods);

        trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterNaN = trimaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in resultAfterNaN)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Update_Infinity_Input_UsesLastValidValue()
    {
        int[] periods = { 10, 20 };
        var trimaVector = new TrimaVector(periods);

        trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, 110.0));

        var resultAfterPosInf = trimaVector.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        foreach (var result in resultAfterPosInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }

        var resultAfterNegInf = trimaVector.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        foreach (var result in resultAfterNegInf)
        {
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Update_MultipleNaN_ContinuesWithLastValid()
    {
        int[] periods = { 5, 10 };
        var trimaVector = new TrimaVector(periods);

        trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, 110.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, 120.0));

        var r1 = trimaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = trimaVector.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = trimaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        foreach (var result in r1) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r2) Assert.True(double.IsFinite(result.Value));
        foreach (var result in r3) Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calculate_Series_HandlesNaN()
    {
        int[] periods = { 5, 10 };
        var trimaVector = new TrimaVector(periods);

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
        var results = trimaVector.Calculate(series);

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
        var trimaVector = new TrimaVector(periods);

        trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));
        trimaVector.Update(new TValue(DateTime.UtcNow, double.NaN));

        trimaVector.Reset();

        var result = trimaVector.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, result[0].Value, 1e-9);
    }

    [Fact]
    public void NaN_Handling_MatchesSingleTrima()
    {
        int[] periods = { 5, 10, 20 };
        var trimaVector = new TrimaVector(periods);
        var trimaSingles = periods.Select(p => new Trima(p)).ToArray();

        var values = new double[] { 10, 20, double.NaN, 40, double.PositiveInfinity, 60, 70 };
        var time = DateTime.UtcNow;

        foreach (var val in values)
        {
            var tVal = new TValue(time, val);
            var multiRes = trimaVector.Update(tVal);

            for (int i = 0; i < periods.Length; i++)
            {
                var singleRes = trimaSingles[i].Update(tVal);
                Assert.Equal(singleRes.Value, multiRes[i].Value, 1e-9);
            }

            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Values_Property_UpdatesAfterUpdate()
    {
        int[] periods = { 5, 10 };
        var trimaVector = new TrimaVector(periods);

        var result = trimaVector.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(result[0].Value, trimaVector.Values[0].Value);
        Assert.Equal(result[1].Value, trimaVector.Values[1].Value);
    }

    [Fact]
    public void Values_Property_UpdatesAfterCalculate()
    {
        int[] periods = { 5, 10 };
        var trimaVector = new TrimaVector(periods);

        var t = new System.Collections.Generic.List<long> { 100, 200, 300 };
        var v = new System.Collections.Generic.List<double> { 10.0, 20.0, 30.0 };
        var series = new TSeries(t, v);

        var results = trimaVector.Calculate(series);

        Assert.Equal(results[0].Last.Value, trimaVector.Values[0].Value, 1e-9);
        Assert.Equal(results[1].Last.Value, trimaVector.Values[1].Value, 1e-9);
    }

    [Fact]
    public void Update_BarCorrection_WorksCorrectly()
    {
        int[] periods = { 3 };
        var trimaVector = new TrimaVector(periods);

        // TRIMA(3) = SMA(SMA(3, 2), 2)
        // p1 = 3/2 + 1 = 2
        // p2 = (3+1)/2 = 2
        // SMA1(2): 10 -> 10
        // SMA2(2): 10 -> 10
        trimaVector.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        
        // SMA1(2): 10, 20 -> 15
        // SMA2(2): 10, 15 -> 12.5
        trimaVector.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        
        // SMA1(2): 20, 30 -> 25
        // SMA2(2): 15, 25 -> 20
        trimaVector.Update(new TValue(DateTime.UtcNow, 30.0), isNew: true);

        var res1 = trimaVector.Values[0].Value;
        Assert.Equal(20.0, res1, 1e-9);

        // Correct the last bar: 30 -> 60
        // SMA1(2): 20, 60 -> 40
        // SMA2(2): 15, 40 -> 27.5
        var res2 = trimaVector.Update(new TValue(DateTime.UtcNow, 60.0), isNew: false);

        Assert.Equal(27.5, res2[0].Value, 1e-9);
    }
}
