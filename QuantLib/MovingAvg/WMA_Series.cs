namespace QuantLib;

/**
WMA: (linearly) Weighted Moving Average
The weights are linearly decreasing over the period and the most recent data has
the heaviest weight.

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/weighted-moving-average-wma/
    https://www.technicalindicators.net/indicators-technical-analysis/83-moving-averages-simple-exponential-weighted
**/

public class WMA_Series : Single_TSeries_Indicator
{
    public WMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        for (int i = 0; i < this._p; i++) { this._weights.Add(i + 1); }
        if (base._data.Count > 0) { base.Add(base._data); }
    }
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private readonly System.Collections.Generic.List<double> _weights = new();

    public override void Add((System.DateTime t, double v) d, bool update)
    {
        if (update) { _buffer[_buffer.Count - 1] = d.v; }
        else { _buffer.Add(d.v); }
        if (_buffer.Count > this._p && this._p != 0) { _buffer.RemoveAt(0); }

        double _wma = 0;
        for (int i = 0; i < _buffer.Count; i++) { _wma += _buffer[i] * this._weights[i]; }
        _wma /= (this._buffer.Count * (this._buffer.Count + 1)) * 0.5;

        var result = (d.t, (this.Count < this._p - 1 && this._NaN) ? double.NaN : _wma);

        base.Add(result, update);
    }
}