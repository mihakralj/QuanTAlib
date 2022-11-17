namespace QuanTAlib;
using System;
using System.Linq;

/* <summary>
ZSCORE: number of standard deviations from SMA
  Z-score describes a value's relationship to the mean of a series, as measured in
  terms of standard deviations from the mean. If a Z-score is 0, it indicates that
  the data point's score is identical to the mean score. A Z-score of 1.0 would
  indicate a value that is one standard deviation from the mean. Z-scores may be
  positive or negative, with a positive value indicating the score is above the
  mean and a negative score indicating it is below the mean.

Sources:
  https://en.wikipedia.org/wiki/Z-score
  https://www.investopedia.com/terms/z/zscore.asp

Calculation:
    std = std * STDEV(close, length)
    mean = SMA(close, length)
    ZSCORE = (close - mean) / std
    
</summary> */

public class ZSCORE_Series : Single_TSeries_Indicator
{
    public ZSCORE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
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
        double _zscore = (_psdev == 0) ? double.NaN : (TValue.v - _sma) / _psdev;

        base.Add((TValue.t, _zscore), update, _NaN);
    }
}