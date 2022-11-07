namespace QuanTAlib;
using System;

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

</summary> 
TODO: buggy - rework
*/

public class JMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> vbuffer10;
    private readonly System.Collections.Generic.List<double> vsum65;

    private double prev_ma1, prev_det0, prev_det1, prev_jma, bsmax, bsmin;
    private double o_prev_ma1, o_prev_det0, o_prev_det1, o_prev_jma, o_bsmax, o_bsmin;

    private readonly double pr, pow1, len2, beta, rvolty;

    public JMA_Series(TSeries source, int period, double phase = 0.0, bool useNaN = false) : base(source, period, useNaN)
    {
        this.vbuffer10 = new();
        this.vsum65 = new();

        // constants
        this.pr = (phase < -100) ? 0.5 : (phase > 100) ? 2.5 : (phase * 0.01) + 1.5;
        double len1 = Math.Max((Math.Log(Math.Sqrt(0.5 * (_p - 1))) / Math.Log(2.0)) + 2.0, 0);
        this.pow1 = Math.Max(len1 - 2, 0.5);
        this.rvolty = Math.Exp((1 / this.pow1) * Math.Log(len1));
        this.len2 = Math.Sqrt(0.5 * (_p - 1)) * len1;
        this.beta = 0.45 * (_p - 1) / (0.45 * (_p - 1) + 2);
        if (base._data.Count > 0) { base.Add(base._data); }
    }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (this.Count == 0)
        {
            this.prev_ma1 = this.prev_jma = TValue.v;
            this.bsmax = this.bsmin = this.prev_det0 = this.prev_det1 = 0;
        }

        if (update)
        {
            this.prev_jma = this.o_prev_jma;
            this.prev_ma1 = this.o_prev_ma1;
            this.prev_det0 = this.o_prev_det0;
            this.prev_det1 = this.o_prev_det1;
            this.bsmax = this.o_bsmax;
            this.bsmin = this.o_bsmin;
        }
        else
        {
            this.o_prev_jma = this.prev_jma;
            this.o_prev_ma1 = this.prev_ma1;
            this.o_prev_det0 = this.prev_det0;
            this.o_prev_det1 = this.prev_det1;
            this.o_bsmax = this.bsmax;
            this.o_bsmin = this.bsmin;
        }

        double hprice = TValue.v;
        double lprice = TValue.v;
        for (int i = 0; i <= Math.Min(9, this._data.Count - 1); i++)
        {
            var _item = this._data[this._data.Count - 1 - i].v;
            hprice = (_item > hprice) ? _item : hprice;
            lprice = (_item < lprice) ? _item : lprice;
        }
        double del1 = hprice - this.bsmax;
        double del2 = lprice - this.bsmin;

        double volty = (Math.Abs(del1) != Math.Abs(del2))
                           ? Math.Max(Math.Abs(del1), Math.Abs(del2))
                           : 0;
        if (update)
        {
            this.vbuffer10[this.vbuffer10.Count - 1] = volty;
        }
        else
        {
            this.vbuffer10.Add(volty);
        }
        if (this.vbuffer10.Count > 10)
        {
            this.vbuffer10.RemoveAt(0);
        }

        double prevvsum =
            (this.vsum65.Count > 0) ? this.vsum65[this.vsum65.Count - 1] : 0;
        double vsumitem = prevvsum + 0.1 * (volty - this.vbuffer10[0]);
        if (update)
        {
            this.vsum65[this.vsum65.Count - 1] = vsumitem;
        }
        else
        {
            this.vsum65.Add(vsumitem);
        }
        if (this.vsum65.Count > 65)
        {
            this.vsum65.RemoveAt(0);
        }

        double avolty = 0;
        for (int i = 0; i < this.vsum65.Count; i++)
        {
            avolty += this.vsum65[i];
        }

        avolty /= this.vsum65.Count;
        double dvolty = (avolty > 0) ? volty / avolty : 0;
        dvolty = Math.Max((dvolty > this.rvolty) ? this.rvolty : dvolty, 1.0);

        double pow2 = Math.Exp(this.pow1 * Math.Log(dvolty));
        double kv =
            Math.Exp(Math.Sqrt(pow2) * Math.Log(this.len2 / (this.len2 + 1)));

        this.bsmax = (del1 > 0) ? hprice : hprice - (kv * del1);
        this.bsmin = (del2 < 0) ? lprice : lprice - (kv * del2);

        // adaptive EMA dynamic factor
        double pow = Math.Pow(dvolty, this.pow1);
        double alpha = Math.Pow(this.beta, pow);

        // 1st stage - preliminary smoothing by adaptive EMA
        double ma1 = TValue.v * (1 - alpha) + this.prev_ma1 * alpha;
        this.prev_ma1 = ma1;

        // 2nd stage - one more preliminary smoothing by Kalman filter
        double det0 = (TValue.v - ma1) * (1 - this.beta) + this.prev_det0 * this.beta;
        this.prev_det0 = det0;
        double ma2 = ma1 + (this.pr * det0);

        // 3rd stage - final smoothing by Jurik adaptive filter
        double det1 = ((ma2 - this.prev_jma) * (1 - alpha) * (1 - alpha)) +
                      (this.prev_det1 * alpha * alpha);
        this.prev_det1 = det1;
        var jma = this.prev_jma + det1;
        this.prev_jma = jma;

        (System.DateTime t, double v) result =
            (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : jma);
        base.Add(result, update);

    }
}