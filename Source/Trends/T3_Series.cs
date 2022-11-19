namespace QuanTAlib;
using System;
using System.Linq;
using System.Numerics;

/* <summary>
T3: Triple Exponential Moving Average
    TEMA uses EMA(EMA(EMA())) to calculate less laggy Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triple-exponential-moving-average-tema/


</summary> */

public class T3_Series : Single_TSeries_Indicator
{
    private int i;
    private double k, a; 
    private double c1, c2, c3, c4;
    private double o_c1, o_c2, o_c3, o_c4;

    private double e1, e2, e3, e4, e5, e6;
    private double o_e1, o_e2, o_e3, o_e4, o_e5, o_e6;

    private double sum1, sum2, sum3, sum4, sum5, sum6;
    private double o_sum1, o_sum2, o_sum3, o_sum4, o_sum5, o_sum6;

    public T3_Series(TSeries source, int period, double vfactor, bool useNaN = false) : base(source, period, useNaN)
    {
        i = 0;
        k = 2.0 / (_p + 1);
        a = vfactor;
        c1 = -a * a * a;
        c2 = (3 * a * a) + (3 * a * a * a);
        c3 = (-6 * a * a) - (3 * a) - (3 * a * a * a);
        c4 = 1 + (3 * a) + (3 * a * a) + (a * a * a) ;
        e1 = e2 = e3 = e4 = e5 = e6 = 0;
        sum1 = sum2 = sum3 = sum4 = sum5 = sum6 = 0;

        if (_data.Count > 0) { base.Add(data: _data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        if (update) {
            // roll back (x = oldx)
            c1 = o_c1; c2 = o_c2; c3 = o_c3; c4 = o_c4;
            e1 = o_e1; e2 = o_e2; e3 = o_e3; e4 = o_e4; e5 = o_e5; e6 = o_e6;
            sum1 = o_sum1; sum2 = o_sum2; sum3 = o_sum3; sum4 = o_sum4; sum5 = o_sum5; sum6 = o_sum6;
        } else {
            // roll forward (oldx = x)
            o_c1 = c1; o_c2 = c2; o_c3 = c3; o_c4 = c4;
            o_e1 = e1; o_e2 = e2; o_e3 = e3; o_e4 = e4; o_e5 = e5; o_e6 = e6;
            o_sum1 = sum1; o_sum2 = sum2; o_sum3 = sum3; o_sum4 = sum4; o_sum5 = sum5; o_sum6 = sum6;
        }
        double v = TValue.v;
        if (i > _p - 1) {
            e1 += k * (v - e1);
            if (i > 2 * (_p - 1)) {
                e2 += k * (e1 - e2);
                if (i > 3 * (_p - 1)) {
                    e3 += k * (e2 - e3);
                    if (i > 4 * (_p - 1)) {
                        e4 += k * (e3 - e4);
                        if (i > 5 * (_p - 1)) {
                            e5 += k * (e4 - e5);
                            if (i > 6 * (_p - 1)) {
                                e6 += k * (e5 - e6);
                                }
                            else {
                                sum6 += e5;
                                if (i == 6 * (_p - 1)) {
                                    e6 = sum6 / _p;
                                    }
                                }
                            }
                        else {
                            sum5 += e4;
                            if (i == 5 * (_p - 1)) {
                                sum6 = e5 = sum5 / _p;
                                }
                            }
                        }
                    else {
                        sum4 += e3;
                        if (i == 4 * (_p - 1)) {
                            sum5 = e4 = sum4 / _p;
                            }
                        }
                    }
                else {
                    sum3 += e2;
                    if (i == 3 * (_p - 1)) {
                        sum4 = e3 = sum3 / _p;
                        }
                    }
                }
            else {
                sum2 += e1;
                if (i == 2 * (_p - 1)) {
                    sum3 = e2 = sum2 / _p;
                    }
                }
            }
        else {
            sum1 += v;
            if (i == _p - 1) {
                sum2 = e1 = sum1 / _p;
                }
            }
        if (!update) { i++; }

        double t3 = (c1 * e6) + (c2 * e5) + (c3 * e4) + (c4 * e3);
        base.Add(TValue: (TValue.t, t3), update: update, useNaN: _NaN);
        }
}