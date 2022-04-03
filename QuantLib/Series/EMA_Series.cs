<<<<<<< Updated upstream:QuantLib/Series/EMA_Series.cs
﻿namespace QuantLib;

/**
EMA: Exponential Moving Average

EMA needs very short history buffer and calculates the EMA value using just the
previous EMA value. The weight of the new datapoint (k) is k = 2 / (period-1)
Sources:
    https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
    https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
    https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA
Issues:
    There is no consensus what the first EMA value should be - a zero, a first
datapoint, or an average of the initial Period bars. All three starting methods
converge within 15+ bars to the same moving average - and the simplest method is
to use a first datapoint That's what this algo is using, and expects at least
*Period* of history (warm-up) datapoints before it provides reliable results.
**/

public class EMA_Series : TSeries {
  private readonly int _p;
  private readonly bool _NaN;
  private readonly TSeries _data;
  private readonly double _k, _k1m;
  private double _lastema, _lastlastema;

  public EMA_Series(TSeries source, int period, bool useNaN = false) {
    this._p = period;
    this._data = source;
    this._k = 2.0 / (double)(period + 1);
    this._k1m = 1.0 - this._k;
    this._NaN = useNaN;
    this._lastema = this._lastlastema = double.NaN;
    source.Pub += this.Sub;
    if (source.Count > 0) {
      for (int i = 0; i < source.Count; i++) {
        this.Add(source[i], false);
      }
    }
  }

  public new void Add((System.DateTime t, double v)data, bool update = false) {
    if (update) {
      this._lastema = this._lastlastema;
    }
    double _ema = System.Double.IsNaN(this._lastema)
                      ? data.v
                      : data.v * this._k + this._lastema * this._k1m;
    this._lastlastema = this._lastema;
    this._lastema = _ema;

    (System.DateTime t, double v) result =
        (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema);
    if (update) {
      base[base.Count - 1] = result;
    } else {
      base.Add(result);
    }
  }

  public void Add(bool update = false) {
    this.Add(this._data[this._data.Count - 1], update);
  }
  public new void Sub(object source, TSeriesEventArgs e) {
    this.Add(this._data[this._data.Count - 1], e.update);
  }
}
=======
﻿namespace QuantLib;

/** 
EMA: Exponential Moving Average 

EMA needs very short history buffer and calculates the EMA value using just the previous EMA value.
The weight of the new datapoint (k) is k = 2 / (period-1)
Sources:
    https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
    https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
    https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA
Issues:
    There is no consensus what the first EMA value should be - a zero, a first datapoint, or an average of the initial Period bars.
    All three starting methods converge within 15+ bars to the same moving average - and the simplest method is to use a first datapoint
    That's what this algo is using, and expects at least *Period* of history (warm-up) datapoints before it provides reliable results.
**/

public class EMA_Series : TSeries
{
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private readonly double _k, _k1m;
    private double _lastema, _lastlastema;

    public EMA_Series(TSeries source, int period, bool useNaN = false)
    {
        this._p = period;
        this._data = source;
        this._k = 2.0 / (double)(period + 1);
        this._k1m = 1.0 - this._k;
        this._NaN = useNaN;
        this._lastema = this._lastlastema = double.NaN;
        source.Pub += this.Sub;
        if (source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                this.Add(source[i], false);
            }
        }
    }

    public new void Add((System.DateTime t, double v) data, bool update = false)
    {
        if (update) { this._lastema = this._lastlastema; }
        double _ema = System.Double.IsNaN(this._lastema) ? data.v : data.v * this._k + this._lastema * this._k1m;
        this._lastlastema = this._lastema;
        this._lastema = _ema;

        (System.DateTime t, double v) result = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _ema);
        if (update)
        {
            base[base.Count - 1] = result;
        }
        else
        {
            base.Add(result);
        }
    }

    public void Add(bool update = false)
    {
        this.Add(this._data[this._data.Count - 1], update);
    }
    public new void Sub(object source, TSeriesEventArgs e) { this.Add(this._data[this._data.Count - 1], e.update); }

}
>>>>>>> Stashed changes:Algos/EMA_Series.cs
