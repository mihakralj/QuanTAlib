namespace QuanTAlib;
using System;

/* <summary>
MACD: Moving Average Convergence/Divergence
    Moving average convergence divergence (MACD) is a trend-following momentum
    indicator that shows the relationship between two moving averages of a series.
    The MACD is calculated by subtracting the 26-period exponential moving average (EMA)
    from the 12-period EMA. MACD Signal is 9-day EMA of MACD.

Sources:
    https://www.investopedia.com/terms/m/macd.asp
    https://www.fidelity.com/learning-center/trading-investing/technical-analysis/technical-indicator-guide/macd

</summary> */

public class MACD_Series : Single_TSeries_Indicator
{
    private readonly EMA_Series _TSslow;
    private readonly EMA_Series _TSfast;
    private readonly SUB_Series _TSmacd;
    public EMA_Series Signal { get; }

    public MACD_Series(TSeries source, int slow = 26, int fast = 12, int signal = 9, bool useNaN = false)
        : base(source, period: 0, useNaN)
    {
        _TSslow = new(source: source, period: slow, useNaN: false);
        _TSfast = new(source: source, period: fast, useNaN: false);
        _TSmacd = new(_TSfast, _TSslow);
        this.Signal = new(source: _TSmacd, period: signal, useNaN: useNaN);

        if (source.Count > 0) { base.Add(_TSmacd); }
    }
    public override void Add((System.DateTime t, double v) TValue, bool update)
    {
        double _macd;
        if (update)
        {
            _TSslow.Add(TValue, true);
            _TSfast.Add(TValue, true);
        }
        _macd = this._TSmacd[(this.Count < this._TSmacd.Count) ? this.Count : this._TSmacd.Count - 1].v;
        var result = (TValue.t, _macd);
        base.Add(result, update);
    }
}