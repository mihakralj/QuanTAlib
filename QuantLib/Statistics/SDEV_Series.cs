﻿/**
SDEV: (Corrected) Sample Standard Deviation

Sample Standard Deviaton uses Bessel's correction to correct the bias in the variance.

Sources:
  https://en.wikipedia.org/wiki/Standard_deviation#Corrected_sample_standard_deviation
  Bessel's correction: https://en.wikipedia.org/wiki/Bessel%27s_correction

Remark:

  SSDEV (Sample Standard Deviation) is also known as a unbiased/corrected Standard Deviation.
  For a population/biased/uncorrected Standard Deviation, use PSDEV instead
      
**/

using System;
namespace QuantLib;

public class SDEV_Series : Single_TSeries_Indicator
{
    public SDEV_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) d, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = d.v; }
        else { this._buffer.Add(d.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        double _sma = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _sma += this._buffer[i]; }
        _sma /= this._buffer.Count;

        double _svar = 0;
        for (int i = 0; i < this._buffer.Count; i++) { _svar += (this._buffer[i] - _sma) * (this._buffer[i] - _sma); }
        _svar /= (this._buffer.Count > 1) ? this._buffer.Count - 1 : 1; // Bessel's correction
        double _ssdev = Math.Sqrt(_svar);

        var result = (d.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ssdev);
        base.Add(result, update);
    }
}