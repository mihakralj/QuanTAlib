namespace QuanTAlib; 
using System;
using System.Linq;

/* <summary>
WMAPE: Weighted Mean Absolute Percentage Error
    Measures the size of the error in percentage terms. Improves problems with MAPE
    when there are zero or close-to-zero values because there would be a division by zero 
    or values of MAPE tending to infinity.

Sources:
  https://en.wikipedia.org/wiki/WMAPE

</summary> */

public class WMAPE_Series : Single_TSeries_Indicator
{
    public WMAPE_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);
        double _sma = _buffer.Average();

        double _div = 0;
        double _wmape = 0;
        for (int i = 0; i < _buffer.Count; i++)
        {
            _wmape += Math.Abs(_buffer[i] - _sma);
            _div += Math.Abs(_buffer[i]);
        }
        _wmape = (_div!=0) ? _wmape/_div : double.PositiveInfinity;

        base.Add((TValue.t, _wmape), update, _NaN);
    }
}