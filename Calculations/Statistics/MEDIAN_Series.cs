namespace QuanTAlib;
using System;
using static System.Net.Mime.MediaTypeNames;

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

public class MEDIAN_Series : Single_TSeries_Indicator
{
    public MEDIAN_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();

    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        Add_Replace_Trim(_buffer, TValue.v, _p, update);

        System.Collections.Generic.List<double> _s = new(this._buffer);
        _s.Sort();
        int _p1 = _s.Count / 2;
        int _p2 = Math.Max(0, (_s.Count / 2) - 1);
        double _med = (_s.Count % 2 != 0) ? _s[_p1] : (_s[_p1] + _s[_p2]) / 2;

        base.Add((TValue.t, _med), update, _NaN);
    }
}
