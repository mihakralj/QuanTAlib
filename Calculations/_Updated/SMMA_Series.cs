namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Linq;

/* <summary>
SMMA: Smoothed Moving Average
    The Smoothed Moving Average (SMMA) is a combination of a SMA and an EMA. It gives the recent prices
    an equal weighting as the historic prices as it takes all available price data into account.
    The main advantage of a smoothed moving average is that it removes short-term fluctuations.

    SMMA(i) = (SMMA-1*(N-1) + CLOSE (i)) / N

Sources:
    https://blog.earn2trade.com/smoothed-moving-average
    https://guide.traderevolution.com/traderevolution/mobile-applications/phone/android/technical-indicators/moving-averages/smma-smoothed-moving-average
    https://www.chartmill.com/documentation/technical-analysis-indicators/217-MOVING-AVERAGES-%7C-The-Smoothed-Moving-Average-%28SMMA%29

</summary> */

public class SMMA_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();

    protected readonly int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    private double _lastsmma, _lastlastsmma;

    //core constructors
    public SMMA_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"SMMA({period})";
    }
    public SMMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public SMMA_Series() : this(period: 0, useNaN: false) { }
    public SMMA_Series(int period) : this(period: period, useNaN: false) { }
    public SMMA_Series(TBars source) : this(source.Close, 0, false) { }
    public SMMA_Series(TBars source, int period) : this(source.Close, period, false) { }
    public SMMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public SMMA_Series(TSeries source) : this(source, 0, false) { }
    public SMMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        if (double.IsNaN(TValue.v))
        {
            return base.Add((TValue.t, double.NaN), update);
        }

        double _smma = 0;
        if (update) { this._lastsmma = this._lastlastsmma; }

        if (this.Count < this._period)
        {
            BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
            _smma = _buffer.Average();
        }
        else
        {
            _smma = ((_lastsmma * (_period - 1)) + TValue.v) / _period;
        }

        this._lastlastsmma = this._lastsmma;
        this._lastsmma = _smma;
        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _smma);
        return base.Add(res, update);
    }

    public override (DateTime t, double v) Add(TSeries data)
    {
        if (data == null) { return (DateTime.Today, Double.NaN); }
        foreach (var item in data) { Add(item, false); }
        return _data.Last;
    }
    public (DateTime t, double v) Add(bool update)
    {
        return this.Add(TValue: _data.Last, update: update);
    }
    public (DateTime t, double v) Add()
    {
        return Add(TValue: _data.Last, update: false);
    }
    private new void Sub(object source, TSeriesEventArgs e)
    {
        Add(TValue: _data.Last, update: e.update);
    }

    //reset calculation
    public override void Reset()
    {
        _buffer.Clear();
        this._lastsmma = this._lastlastsmma = 0;
    }
}