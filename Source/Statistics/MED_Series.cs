namespace QuanTAlib;
using System;

/* <summary>
MED - Median value
    Median of numbers is the middlemost value of the given set of numbers. 
    It separates the higher half and the lower half of a given data sample. 
    At least half of the observations are smaller than or equal to median 
    and at least half of the observations are greater than or equal to the median.

    If the number of values is odd, the middlemost observation of the sorted
    list is the median of the given data.  If the number of values is even, 
    median is the average of (n/2)th and [(n/2) + 1]th values of the sorted list.

    If period = 0 => period is max

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/other/median/
    https://en.wikipedia.org/wiki/Median

</summary> */

public class MED_Series : Single_TSeries_Indicator
{
    public MED_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        if (update) { this._buffer[this._buffer.Count - 1] = TValue.v; }
        else { this._buffer.Add(TValue.v); }
        if (this._buffer.Count > this._p && this._p != 0) { this._buffer.RemoveAt(0); }

        System.Collections.Generic.List<double> _s = new(this._buffer);
        _s.Sort();
        int _p1 = _s.Count / 2;
        int _p2 = Math.Max(0, _s.Count / 2 - 1);
        double _med = (_s.Count % 2 != 0) ? _s[_p1] : (_s[_p1] + _s[_p2]) / 2;

        var result = (TValue.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _med);

        base.Add(result, update);
    }
}
