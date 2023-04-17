namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
SDEV: Population Standard Deviation
  Population Standard Deviation is the square root of the biased variance, also knons as
  Uncorrected Sample Standard Deviation

Sources:
  https://en.wikipedia.org/wiki/Standard_deviation#Uncorrected_sample_standard_deviation

Remark:
  SDEV (Population Standard Deviation) is also known as a biased/uncorrected Standard Deviation.
  For unbiased version that uses Bessel's correction, use SDEV instead.
    
</summary> */

public class SDEV_Series : Single_TSeries_Indicator
{
    public SDEV_Series(TSeries source, int period=0, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _pvar = 0;
        for (int i = 0; i < _buffer.Count; i++) { _pvar += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _pvar /= this._buffer.Count;
        double _psdev = Math.Sqrt(_pvar);

        base.Add((TValue.t, _psdev), update, _NaN);
    }
}