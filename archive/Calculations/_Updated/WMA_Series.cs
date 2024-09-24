namespace QuanTAlib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/* <summary>
WMA: (linearly) Weighted Moving Average
    The weights are linearly decreasing over the period and the most recent data has
    the heaviest weight.

Sources:
    https://corporatefinanceinstitute.com/resources/knowledge/trading-investing/weighted-moving-average-wma/
    https://www.technicalindicators.net/indicators-technical-analysis/83-moving-averages-simple-exponential-weighted

</summary> */

public class WMA_Series : TSeries
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private System.Collections.Generic.List<double> _weights;
    protected int _period;
    protected readonly bool _NaN;
    protected readonly TSeries _data;
    protected int _len;
    public int Len
    {
        get { return _len; }
        set { _len = value; }
    }

    //core constructors
    public WMA_Series(int period, bool useNaN)
    {
        _period = period;
        _NaN = useNaN;
        Name = $"WMA({period})";
        _len = 1;
        _weights = CalculateWeights(_period);
    }
    public WMA_Series(TSeries source, int period, bool useNaN) : this(period, useNaN)
    {
        _data = source;
        Name = Name.Substring(0, Name.IndexOf(")")) + $", {(string.IsNullOrEmpty(_data.Name) ? "data" : _data.Name)})";
        _data.Pub += Sub;
        Add(_data);
    }
    public WMA_Series() : this(period: 0, useNaN: false) { }
    public WMA_Series(int period) : this(period: period, useNaN: false) { }
    public WMA_Series(TBars source) : this(source.Close, 0, false) { }
    public WMA_Series(TBars source, int period) : this(source.Close, period, false) { }
    public WMA_Series(TBars source, int period, bool useNaN) : this(source.Close, period, useNaN) { }
    public WMA_Series(TSeries source, int period) : this(source: source, period: period, useNaN: false) { }

    //////////////////
    // core Add() algo
    public override (DateTime t, double v) Add((DateTime t, double v) TValue, bool update = false)
    {
        BufferTrim(buffer: _buffer, value: TValue.v, period: _period, update: update);
        if (_period == 0)
        {
            _weights = CalculateWeights(_len);
            _len++;
        }
        double _wma = 0;
        double totalWeights = (_buffer.Count * (_buffer.Count + 1)) * 0.5;
        object lockObj = new object();
        Parallel.For(0, _buffer.Count, i =>
        {
            double temp = _buffer[i] * this._weights[i];
            lock (lockObj) { _wma += temp; }
        });
        _wma /= totalWeights;
        var res = (TValue.t, Count < _period - 1 && _NaN ? double.NaN : _wma);
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

    //calculating weights
    private static List<double> CalculateWeights(int period)
    {
        List<double> weights = new List<double>(period);
        for (int i = 0; i < period; i++)
        {
            weights.Add(i + 1);
        }
        return weights;
    }

    //reset calculation
    public override void Reset()
    {
        _len = 0;
        _weights = CalculateWeights(_period);
        _buffer.Clear();
    }
}