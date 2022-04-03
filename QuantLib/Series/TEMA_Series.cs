namespace QuantLib;

/**
TEMA: Triple Exponential Moving Average
TEMA uses EMA(EMA(EMA())) to calculate less laggy Exponential moving average.

Sources:
    https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/triple-exponential-moving-average-tema/

Remark:
    ema1 = EMA(close, length)
    ema2 = EMA(ema1, length)
    ema3 = EMA(ema2, length)
    TEMA = 3 * (ema1 - ema2) + ema3
**/

public class TEMA_Series : TSeries
{
    private readonly int _p;
    private readonly bool _NaN;
    private readonly TSeries _data;
    private readonly double _k, _k1m;
    private double _lastema1, _lastlastema1;
    private double _lastema2, _lastlastema2;
    private double _lastema3, _lastlastema3;

    public TEMA_Series(TSeries source, int period, bool useNaN = false)
    {
        this._p = period;
        this._data = source;
        this._k = 2.0 / (double)(period + 1);
        this._k1m = 1.0 - this._k;
        this._NaN = useNaN;
        this._lastema1 = this._lastlastema1 = double.NaN;
        this._lastema2 = this._lastlastema2 = double.NaN;
        this._lastema3 = this._lastlastema3 = double.NaN;
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
        if (update)
        {
            this._lastema1 = this._lastlastema1;
            this._lastema2 = this._lastlastema2;
            this._lastema3 = this._lastlastema3;
        }
        double _ema1 = System.Double.IsNaN(this._lastema1) ? data.v : data.v * this._k + this._lastema1 * this._k1m;
        double _ema2 = System.Double.IsNaN(this._lastema2) ? _ema1 : _ema1 * this._k + this._lastema2 * this._k1m;
        double _ema3 = System.Double.IsNaN(this._lastema3) ? _ema2 : _ema2 * this._k + this._lastema3 * this._k1m;

        double _tema = 3 * (_ema1 - _ema2) + _ema3;

        this._lastlastema1 = this._lastema1;
        this._lastlastema2 = this._lastema2;
        this._lastlastema3 = this._lastema3;
        this._lastema1 = _ema1;
        this._lastema2 = _ema2;
        this._lastema3 = _ema3;

        (System.DateTime t, double v) result = (data.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _tema);
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
    public new void Sub(object source, TSeriesEventArgs e) {
        this.Add(this._data[this._data.Count - 1], e.update);
    }

}