// MAX - Maximum value in the given period in the series
using System;
namespace QuantLib;

public class MAX_Series : TSeries {
  readonly TSeries _data;
  readonly int _period;
  
  public MAX_Series(TSeries source, int period = 0) {
    this._data = source;
    this._period = period;
    _data.Pub += this.Sub;

    if (_data.Count > 0) {
      int _p = (_period==0)?_data.Count:_period;
      
      for (int i = 0; i < _data.Count; i++) {
        double _max = _data[i].v;
        for (int j = Math.Max(i-_p+1,0); j < i; j++) 
          { 
            _max = (_data[j].v > _max)?_data[j].v:_max;
          }
        base.Add((_data[i].t, _max), false);
      }
    }
  }

  public new void Add((System.DateTime t, double v)d, bool update = false) {
    double _max = d.v;
    int _p = (_period==0)?_data.Count:_period;
    for (int j = Math.Max(_data.Count-1-_p,0); j < _data.Count-1; j++) 
      { 
        _max = (_data[j].v > _max)?_data[j].v:_max;
      }
      base.Add(_max, update);
  }

  public void Add(bool update = false) { this.Add(_data[_data.Count-1], update); }
  public new void Sub(object source, TSeriesEventArgs e) => this.Add(e.update);
}
