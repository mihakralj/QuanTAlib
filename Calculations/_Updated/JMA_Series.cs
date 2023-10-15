namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
JMA: Jurik Moving Average
    Mark Jurik's Moving Average (JMA) attempts to eliminate noise to see the
    underlying activity. It has extremely low lag, is very smooth and is responsive
    to market gaps.

Sources:
    https://c.mql5.com/forextsd/forum/164/jurik_1.pdf
    https://www.prorealcode.com/prorealtime-indicators/jurik-volatility-bands/

Issues:
    Real JMA algorithm is not published and this formula is derived through
    deduction and reverse analysis of JMA behavior. It is really close, but not
    exact - published JMA tests against JMA.CSV fail with small deviation. The
    original algo is slightly different, yet this approximation is close enough.

</summary> */

public class JMA_Series : TSeries
{
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private readonly System.Collections.Generic.List<double> volty_short = new();
    private readonly System.Collections.Generic.List<double> vsum_buff = new();
    private readonly double pr;
    private double upperBand, lowerBand, vsum, Kv;
    private double prev_ma1, prev_det0, prev_det1, prev_vsum, prev_jma;
    private double p_upperBand, p_lowerBand, p_Kv, p_prev_ma1, p_prev_det0, p_prev_det1, p_prev_vsum, p_prev_jma;
    private readonly int _voltyS, _voltyL;

    //core constructors
    public JMA_Series(int period, double phase, int vshort, int vlong, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"JMA({period})";
        upperBand = lowerBand = prev_ma1 = prev_det0 = prev_det1 = prev_vsum = prev_jma = Kv = 0.0;
        pr = (phase * 0.01) + 1.5;
        if (phase < -100) { pr = 0.5; }
        if (phase > 100) { pr = 2.5; }
        _voltyS = vshort;
        _voltyL = vlong;
    }

    public JMA_Series(TSeries source, int period, double phase, int vshort, int vlong, bool useNaN) : this(period, phase, vshort, vlong, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public JMA_Series() : this(period: 0, phase: 0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(int period) : this(period: period, phase: 0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(TBars source) : this(source.Close, period: 0, phase: 0.0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(TBars source, int period) : this(source.Close, period, phase: 0.0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, phase: 0.0, vshort: 10, vlong: 65, useNaN: useNaN) { }
    public JMA_Series(TSeries source) : this(source, period: 0, phase: 0.0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(TSeries source, int period) : this(source: source, period: period, phase: 0.0, vshort: 10, vlong: 65, useNaN: false) { }
    public JMA_Series(TSeries source, int period, bool useNaN) : this(source: source, period: period, phase: 0.0, vshort: 10, vlong: 65, useNaN: useNaN) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (this.Count == 0) { prev_ma1 = prev_jma = TValue.v; }
        if (update)
        {
            upperBand = p_upperBand;
            lowerBand = p_lowerBand;
            Kv = p_Kv;
            prev_vsum = p_prev_vsum;
            prev_ma1 = p_prev_ma1;
            prev_det0 = p_prev_det0;
            prev_det1 = p_prev_det1;
            prev_jma = p_prev_jma;
        }
        else
        {
            p_upperBand = upperBand;
            p_lowerBand = lowerBand;
            p_Kv = Kv;
            p_prev_vsum = prev_vsum;
            p_prev_ma1 = prev_ma1;
            p_prev_det0 = prev_det0;
            p_prev_det1 = prev_det1;
            p_prev_jma = prev_jma;
        }

        if (double.IsNaN(TValue.v))
        {
            return base.Add((TValue.t, double.NaN), update);
        }

        // from Tvalue to volty
        double del1 = TValue.v - upperBand;
        double del2 = TValue.v - lowerBand;
        upperBand = (del1 > 0) ? TValue.v : TValue.v - (Kv * del1);
        lowerBand = (del2 < 0) ? TValue.v : TValue.v - (Kv * del2);
        double volty = Math.Abs(del1) > Math.Abs(del2) ? Math.Abs(del1) :
                 (Math.Abs(del1) < Math.Abs(del2) ? Math.Abs(del2) :
                     Math.Abs(0.5 * (del1 + del2)));

        //// from volty to avolty
        if (update) { volty_short[volty_short.Count - 1] = volty; }
        else { volty_short.Add(volty); }
        if (volty_short.Count > _voltyS) { volty_short.RemoveAt(0); }
        vsum = prev_vsum + 0.1 * (volty - volty_short.First());
        prev_vsum = vsum;
        if (update) { vsum_buff[vsum_buff.Count - 1] = vsum; }
        else { vsum_buff.Add(vsum); }
        if (vsum_buff.Count > _voltyL) { vsum_buff.RemoveAt(0); }
        double avolty = 0;
        for (int i = 0; i < vsum_buff.Count; i++) { avolty += vsum_buff[i]; }
        avolty /= vsum_buff.Count;

        /// from avolty to rolty
        double rvolty = (avolty != 0) ? volty / avolty : 0;
        double len1 = (Math.Log(Math.Sqrt(_period)) / Math.Log(2.0)) + 2;
        if (len1 < 0) { len1 = 0; }

        double pow1 = Math.Max(len1 - 2.0, 0.5);
        if (rvolty > Math.Pow(len1, 1.0 / pow1)) { rvolty = Math.Pow(len1, 1.0 / pow1); }
        if (rvolty < 1) { rvolty = 1; }

        //// from rvolty to second smoothing
        double pow2 = Math.Pow(rvolty, pow1);
        double beta = 0.45 * (_period - 1) / (0.45 * (_period - 1) + 2);
        Kv = Math.Pow(beta, Math.Sqrt(pow2));
        double alpha = Math.Pow(beta, pow2);
        double ma1 = (1 - alpha) * TValue.v + alpha * prev_ma1;
        prev_ma1 = ma1;

        double det0 = (1 - beta) * (TValue.v - ma1) + beta * prev_det0;
        prev_det0 = det0;
        double ma2 = ma1 + pr * det0;

        double det1 = ((1 - alpha) * (1 - alpha) * (ma2 - prev_jma)) + (alpha * alpha * prev_det1);
        prev_det1 = det1;
        double jma = prev_jma + det1;
        prev_jma = jma;

        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : jma);
        return base.Add(res, update);
    }

    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }
    public (DateTime t, double v) Add(bool update)
    {
        return this.Add(TValue: _data.Last, update: update);
    }
    public (DateTime t, double v) Add()
    {
        return Add(TValue: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(TValue: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset()
    {
        upperBand = lowerBand = prev_ma1 = prev_det0 = prev_det1 = prev_vsum = prev_jma = Kv = 0.0;
    }
}