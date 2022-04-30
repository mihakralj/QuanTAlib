namespace QuanTAlib;
using System;

/* <summary>
LINREG: Linear Regression (using Least Square Method)
  Linear Regression provides a slope of a straight line that is the best approximation of the given set of data.
  The method of least squares is a standard approach in linear regression analysis to approximate the solution
  by minimizing the sum of the squares of the residuals made in the results of each individual equation.

Additional outputs provided by LINREG:
  .Intercept - y-intercept point of the best fit line
  .RSquared - R-Squared (R²), Coefficient of Determination
  .StdDev - Standard Deviation of data over given periods

  y = Slope * x + Intercept

Sources:
  https://en.wikipedia.org/wiki/Least_squares

</summary> */

public class LINREG_Series : Single_TSeries_Indicator
{
    public readonly TSeries Intercept = new();
    public readonly TSeries RSquared = new();
    public readonly TSeries StdDev = new();
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public LINREG_Series(TSeries source, int period, bool useNaN = false)
        : base(source, period, useNaN)
    {
        if (this._data.Count > 0) { base.Add(this._data); }
    }

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p) { this._buffer.RemoveAt(0); }

        int _len = this._buffer.Count;

        // get averages for period
        double sumX = 0;
        double sumY = 0;

        for (int p = 0; p < _len; p++)
        {
            sumX += this.Count - _len + 2 + p;
            sumY += _buffer[p];
        }
        double avgX = sumX / _len;
        double avgY = sumY / _len;

        // least squares method
        double sumSqX = 0;
        double sumSqY = 0;
        double sumSqXY = 0;

        for (int p = 0; p < _len; p++)
        {
            double devX = this.Count - _len + 2 + p - avgX;
            double devY = _buffer[p] - avgY;

            sumSqX += devX * devX;
            sumSqY += devY * devY;
            sumSqXY += devX * devY;
        }

        double _slope = sumSqXY / sumSqX;
        double _intercept = avgY - (_slope * avgX);

        // calculate Standard Deviation and R-Squared
        double stdDevX = Math.Sqrt((double)sumSqX / _len);
        double stdDevY = Math.Sqrt((double)sumSqY / _len);
        double _StdDev = stdDevY;

        double arrr = (stdDevX * stdDevY != 0) ? (double)sumSqXY / (stdDevX * stdDevY) / _len : 0;
        double _RSquared = arrr * arrr;

        var ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _slope);
        base.Add(ret, update);

        ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _intercept);
        Intercept.Add(ret, update);

        ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _StdDev);
        StdDev.Add(ret, update);

        ret = (TValue.t, this.Count < this._p - 1 && this._NaN ? double.NaN : _RSquared);
        RSquared.Add(ret, update);
    }
}