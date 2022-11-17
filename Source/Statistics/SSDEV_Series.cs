namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
SSDEV: (Corrected) Sample Standard Deviation
  Sample Standard Deviaton uses Bessel's correction to correct the bias in the variance.

Sources:
  https://en.wikipedia.org/wiki/Standard_deviation#Corrected_sample_standard_deviation
  Bessel's correction: https://en.wikipedia.org/wiki/Bessel%27s_correction

Remark:
  SSDEV (Sample Standard Deviation) is also known as a unbiased/corrected Standard Deviation.
  For a population/biased/uncorrected Standard Deviation, use PSDEV instead
      
</summary> */

public class SSDEV_Series : Single_TSeries_Indicator
{
    public SSDEV_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _svar = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _svar += (_buffer[i] - _sma) * (_buffer[i] - _sma); }
        _svar /= (_buffer.Count > 1) ? _buffer.Count - 1 : 1; // Bessel's correction
        double _ssdev = Math.Sqrt(_svar);

        base.Add((TValue.t, _ssdev), update, _NaN);
    }
}