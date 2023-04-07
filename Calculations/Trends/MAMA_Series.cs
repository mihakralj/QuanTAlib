namespace QuanTAlib;
using System;

/* <summary>
MAMA: MESA Adaptive Moving Average
    Created by John Ehlers, the MAMA indicator is a 5-period adaptive moving average of
    high/low price that uses classic electrical radio-frequency signal processing algorithms
    to reduce noise.

    KAMAi = KAMAi - 1 + SC * ( price - KAMAi-1 )

Sources:
    https://mesasoftware.com/papers/MAMA.pdf
    https://www.tradingview.com/script/foQxLbU3-Ehlers-MESA-Adaptive-Moving-Average-LazyBear/

</summary> */

public class MAMA_Series : Single_TSeries_Indicator
{
    public MAMA_Series(TSeries source, double fastlimit = 0.5, double slowlimit = 0.05, bool useNaN = false) : base(source, period: 5, useNaN)
    {
        fastl = fastlimit;
        slowl = slowlimit;
        Fama = new();
        if (base._data.Count > 0) { base.Add(base._data); }
    }

  private double sumPr, jI, jQ;
  readonly double fastl, slowl;
    private (double i, double i1, double i2, double i3, double i4, double i5, double i6, double io) pr, i1, q1, sm, dt;
    private (double i, double i1, double io) i2, q2, re, im, pd, ph, mama, fama;
    public TSeries Fama { get; }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        
        if (!update) {
            // roll forward (oldx = x)
            pr.io = pr.i6; pr.i6 = pr.i5; pr.i5 = pr.i4; pr.i4 = pr.i3; pr.i3 = pr.i2; pr.i2 = pr.i1; pr.i1 = pr.i;
            i1.io = i1.i6; i1.i6 = i1.i5; i1.i5 = i1.i4; i1.i4 = i1.i3; i1.i3 = i1.i2; i1.i2 = i1.i1; i1.i1 = i1.i;
            q1.io = q1.i6; q1.i6 = q1.i5; q1.i5 = q1.i4; q1.i4 = q1.i3; q1.i3 = q1.i2; q1.i2 = q1.i1; q1.i1 = q1.i;
            dt.io = dt.i6; dt.i6 = dt.i5; dt.i5 = dt.i4; dt.i4 = dt.i3; dt.i3 = dt.i2; dt.i2 = dt.i1; dt.i1 = dt.i;
            sm.io = sm.i6; sm.i6 = sm.i5; sm.i5 = sm.i4; sm.i4 = sm.i3; sm.i3 = sm.i2; sm.i2 = sm.i1; sm.i1 = sm.i;
            i2.io = i2.i1; i2.i1 = i2.i;
            q2.io = q2.i1; q2.i1 = q2.i;
            re.io = re.i1; re.i1 = re.i;
            im.io = im.i1; im.i1 = im.i;
            pd.io = pd.i1; pd.i1 = pd.i;
            ph.io = ph.i1; ph.i1 = ph.i;
            mama.io = mama.i1; mama.i1 = mama.i;
            fama.io = fama.i1; fama.i1 = fama.i;
        }
        int i = base.Count;
        pr.i = TValue.v;
        if (i > 5) {
            double adj = (0.075 * pd.i1) + 0.54;

            // smooth and detrender
            sm.i = ((4 * pr.i) + (3 * pr.i1) + (2 * pr.i2) + pr.i3) / 10;
            dt.i = ((0.0962 * sm.i) + (0.5769 * sm.i2) - (0.5769 * sm.i4) - (0.0962 * sm.i6)) * adj;

            // in-phase and quadrature
            q1.i = ((0.0962 * dt.i) + (0.5769 * dt.i2) - (0.5769 * dt.i4) - (0.0962 * dt.i6)) * adj;
            i1.i = dt.i3;

            // advance the phases by 90 degrees
            jI = ((0.0962 * i1.i) + (0.5769 * i1.i2) - (0.5769 * i1.i4) - (0.0962 * i1.i6)) * adj;
            jQ = ((0.0962 * q1.i) + (0.5769 * q1.i2) - (0.5769 * q1.i4) - (0.0962 * q1.i6)) * adj;

            // phasor addition for 3-bar averaging
            i2.i = i1.i - jQ;
            q2.i = q1.i + jI;

            i2.i = (0.2 * i2.i) + (0.8 * i2.i1);  // smoothing it
            q2.i = (0.2 * q2.i) + (0.8 * q2.i1);

            // homodyne discriminator
            re.i = (i2.i * i2.i1) + (q2.i * q2.i1);
            im.i = (i2.i * q2.i1) - (q2.i * i2.i1);

            re.i = (0.2 * re.i) + (0.8 * re.i1);  // smoothing it
            im.i = (0.2 * im.i) + (0.8 * im.i1);

            // calculate period
            pd.i = (im.i != 0 && re.i != 0) ? (6.283185307179586 / Math.Atan(im.i / re.i)) : 0d;

            // adjust period to thresholds
            pd.i = (pd.i > 1.5 * pd.i1) ? 1.5 * pd.i1 : pd.i;
            pd.i = (pd.i < 0.67 * pd.i1) ? 0.67 * pd.i1 : pd.i;
            pd.i = (pd.i < 6d) ? 6d : pd.i;
            pd.i = (pd.i > 50d) ? 50d : pd.i;

            // smooth the period
            pd.i = (0.2 * pd.i) + (0.8 * pd.i1);

            // determine phase position
            ph.i = (i1.i != 0) ? Math.Atan(q1.i / i1.i) * 57.29577951308232 : 0;

            // change in phase
            double delta = Math.Max(ph.i1 - ph.i, 1d);

            // adaptive alpha value
            double alpha = Math.Max(fastl / delta, slowl);

            // final indicators
            mama.i = ((alpha * pr.i) + ((1d - alpha) * mama.i1));
            fama.i = ((0.5d * alpha * mama.i) + ((1d - (0.5d * alpha)) * fama.i1));
        }
        else {
            sumPr += pr.i;
            pd.i =  sm.i =  dt.i =  i1.i = q1.i = i2.i =  q2.i = re.i = im.i = ph.i = 0;
            mama.i = fama.i = sumPr / (i+1);
        }

        base.Add((TValue.t, mama.i), update, _NaN);
        var result = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : fama.i);
        Fama.Add(result, update);
    }
}
