/*
MED - Median value

Median of numbers is the middlemost value of the given set of numbers. 
It separates the higher half and the lower half of a given data sample. 
At least half of the observations are smaller than or equal to median 
and at least half of the observations are greater than or equal to the median.

If the number of values is odd, the middlemost observation of the sorted
list is the median of the given data.  If the number of values is even, 
median is the average of (n/2)th and [(n/2) + 1]th values of the sorted list.

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/other/median/
    https://en.wikipedia.org/wiki/Median

*/

using System;
namespace QuantLib;

public class MED_Series : TSeries {
    readonly TSeries _data;
    readonly int _period;
    
    public MED_Series(TSeries source, int period = 0) {
      this._data = source;
      this._period = period;
      _data.Pub += this.Sub;
  
      if (_data.Count > 0) {
        int _p = (_period==0)?_data.Count:_period;
        for (int i = 0; i < _data.Count; i++) {
          List<double> _slice = new();
          for (int j = Math.Max(i-_p+1,0); j <= i; j++) 
            { _slice.Add(_data[j].v); }
          _slice.Sort();
          int _p1 = _slice.Count/2;
          int _p2 = Math.Max(0,_slice.Count/2-1);
          double _med = (_slice.Count%2==1) ? _slice[_p1] : (_slice[_p1]+_slice[_p2])/2;
          base.Add((_data[i].t, _med), false);
        }
      }
    }
  
    public new void Add((System.DateTime t, double v)d, bool update = false) {
      int _p = (_period==0)?_data.Count:_period;
      List<double> _slice = new();
      for (int j = Math.Max(_data.Count-_p,0); j < _data.Count; j++) 
        { _slice.Add(_data[j].v); }
      _slice.Sort();
      int _p1 = _slice.Count/2;
      int _p2 = Math.Max(0,_slice.Count/2-1);
      double _med = (_slice.Count%2==1) ? _slice[_p1] : (_slice[_p1]+_slice[_p2])/2;
      base.Add((d.t, _med), update);
    }
  
    public void Add(bool update = false) { this.Add(_data[_data.Count-1], update); }
    public new void Sub(object source, TSeriesEventArgs e) => this.Add(e.update);
  }