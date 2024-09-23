using System.Linq;

namespace QuanTAlib;
using System;
using System.Collections.Generic;

/* <summary>
SLOPE: Slope of linear regression (using Least Square Method)
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

public class SLOPE_Series : TSeries
{
    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private readonly TSeries p_Intercept = new();
    private readonly TSeries p_RSquared = new();
    private readonly TSeries p_StdDev = new();
    private readonly System.Collections.Generic.List<double> _buffer = new();
    public TSeries Intercept => p_Intercept;
    public TSeries RSquared => p_RSquared;
    public TSeries StdDev => p_StdDev;
    //core constructors
    public SLOPE_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"SLOPE({period})";
    }
    public SLOPE_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public SLOPE_Series() : this(period: 0, useNaN: false) { }
    public SLOPE_Series(int period) : this(period: period, useNaN: false) { }
    public SLOPE_Series(TBars source) : this(source.Close, 0, false) { }
    public SLOPE_Series(TBars source, int period) : this(source.Close, period, false) { }
    public SLOPE_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public SLOPE_Series(TSeries source) : this(source, 0, false) { }
    public SLOPE_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);

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
        double stdDevX = Math.Sqrt(sumSqX / _len);
        double stdDevY = Math.Sqrt(sumSqY / _len);
        double _StdDev = stdDevY;

        double arrr = (stdDevX * stdDevY != 0) ? sumSqXY / (stdDevX * stdDevY) / _len : 0;
        double _RSquared = arrr * arrr;

        var ret = (TValue.t, this.Count < this._period - 1 && this._NaN ? double.NaN : _intercept);
        p_Intercept.Add(ret, update);

        ret = (TValue.t, this.Count < this._period - 1 && this._NaN ? double.NaN : _StdDev);
        p_StdDev.Add(ret, update);

        ret = (TValue.t, this.Count < this._period - 1 && this._NaN ? double.NaN : _RSquared);
        p_RSquared.Add(ret, update);

        ret = (TValue.t, this.Count < this._period - 1 && this._NaN ? double.NaN : _slope);
        return base.Add(ret, update);
    }

    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }

    //reset calculation
    public override void Reset()
    {
        _buffer.Clear();
    }
}