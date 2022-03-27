using System;
namespace QuantLib;

/** 
JMA: Jurik Moving Average 
Mark Jurik's Moving Average (JMA) attempts to eliminate noise to see the  underlying activity. 
It has extremely low lag, is very smooth and is responsive to market gaps.

Sources:
    https://c.mql5.com/forextsd/forum/164/jurik_1.pdf
    https://www.prorealcode.com/prorealtime-indicators/jurik-volatility-bands/

Issues:
    Real JMA algorithm is not published and this formula is derived through deduction and reverse
    analysis of JMA behavior. It is close, but not exact JMA - the tests against JMA.CSV fail with
    small deviation. :-(
**/
public class JMA_Series : TSeries
{
    private double _p;
    private bool _NaN;
    private TSeries _source;
    private System.Collections.Generic.List<double> vbuffer10;
    private System.Collections.Generic.List<double> vsum65;

    private double prev_ma1, prev_det0, prev_det1, prev_jma, bsmax, bsmin;
    private double o_prev_ma1, o_prev_det0, o_prev_det1, o_prev_jma, o_bsmax, o_bsmin;

    private double len1, pr, pow1, len2, beta, rvolty;
    private int _l;

    public JMA_Series(TSeries source, int period, double phase = 0.0, bool useNaN = false)
    {
        _p = period;
        _NaN = useNaN;
        _source = source;
        vbuffer10 = new();
        vsum65 = new();
        source.Pub += this.Sub;
        if (source.Count > 0) for (int i = 0; i < source.Count; i++) this.Add(source[i], false);


        //constants
        double _pp = _p * 0.333333333333;
        pr = (phase < -100) ? 0.5 : (phase > 100) ? 2.5 : (phase * 0.01) + 1.5;
        len1 = Math.Max((Math.Log(Math.Sqrt(0.5 * (_pp - 1))) / Math.Log(2.0)) + 2.0, 0);
        pow1 = Math.Max(len1 - 2, 0.5);
        rvolty = Math.Exp((1 / pow1) * Math.Log(len1));
        len2 = Math.Sqrt(0.5 * (_pp - 1)) * len1;
        beta = 0.45 * (_pp - 1) / (0.45 * (_pp - 1) + 2);
        _l = (int)Math.Round(_p-1 * 0.5);
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (this.Count == 0)
        {
            prev_ma1 = prev_jma = data.v;
            bsmax = bsmin = prev_det0 = prev_det1 = 0;
        }

        if (update)
        {
            prev_jma = o_prev_jma;
            prev_ma1 = o_prev_ma1;
            prev_det0 = o_prev_det0;
            prev_det1 = o_prev_det1;
            bsmax = o_bsmax;
            bsmin = o_bsmin;
        }
        else
        {
            o_prev_jma = prev_jma;
            o_prev_ma1 = prev_ma1;
            o_prev_det0 = prev_det0;
            o_prev_det1 = prev_det1;
            o_bsmax = bsmax;
            o_bsmin = bsmin;
        }

        double _lagdata = _source[_source.Count - 1].v + (_source[_source.Count - 1].v - _source[Math.Max(_source.Count - 1 - _l, 0)].v);

        double hprice = data.v;
        double lprice = data.v;
        for (int i = 0; i <= Math.Min(9, _source.Count - 1); i++)
        {
            var _item = _source[_source.Count - 1 - i].v;
            hprice = (_item > hprice) ? _item : hprice;
            lprice = (_item < lprice) ? _item : lprice;
        }
        double del1 = hprice - bsmax;
        double del2 = lprice - bsmin;

        double volty = (Math.Abs(del1) != Math.Abs(del2)) ? Math.Max(Math.Abs(del1), Math.Abs(del2)) : 0;
        if (update) vbuffer10[vbuffer10.Count - 1] = volty; else vbuffer10.Add(volty);
        if (vbuffer10.Count > 10) vbuffer10.RemoveAt(0);

        double prevvsum = (vsum65.Count > 0) ? vsum65[vsum65.Count - 1] : 0;
        double vsumitem = prevvsum + 0.1 * (volty - vbuffer10[0]);
        if (update) vsum65[vsum65.Count - 1] = vsumitem; else vsum65.Add(vsumitem);
        if (vsum65.Count > 65) vsum65.RemoveAt(0);

        double avolty = 0;
        for (int i = 0; i < vsum65.Count; i++) avolty += vsum65[i];
        avolty /= vsum65.Count;
        double dvolty = (avolty > 0) ? volty / avolty : 0;
        dvolty = Math.Max((dvolty > rvolty) ? rvolty : dvolty, 1.0);

        double pow2 = Math.Exp(pow1 * Math.Log(dvolty));
        double kv = Math.Exp(Math.Sqrt(pow2) * Math.Log(len2 / (len2 + 1)));

        bsmax = (del1 > 0) ? hprice : hprice - (kv * del1);
        bsmin = (del2 < 0) ? lprice : lprice - (kv * del2);

        //adaptive EMA dynamic factor
        double pow = Math.Pow(dvolty, pow1);
        double alpha = Math.Pow(beta, pow);

        // 1st stage - preliminary smoothing by adaptive EMA
        double ma1 = data.v * (1 - alpha) + prev_ma1 * alpha;
        prev_ma1 = ma1;

        // 2nd stage - one more preliminary smoothing by Kalman filter
        double det0 = (data.v - ma1) * (1 - beta) + prev_det0 * beta;
        prev_det0 = det0;
        double ma2 = ma1 + (pr * det0);

        // 3rd stage - final smoothing by Jurik adaptive filter
        double det1 = ((ma2 - prev_jma) * (1 - alpha) * (1 - alpha)) + (prev_det1 * alpha * alpha);
        prev_det1 = det1;
        var jma = prev_jma + det1;
        prev_jma = jma;

        (System.DateTime t, double v) result = (data.t, (this.Count < _p - 1 && _NaN) ? double.NaN : jma);
        if (update) base[base.Count - 1] = result; else base.Add(result);

    }

    public void Add(bool update = false) { this.Add(_source[_source.Count - 1], update); }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(_source[_source.Count - 1], e.update); }

}