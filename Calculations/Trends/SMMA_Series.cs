namespace QuanTAlib;
using System;
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

public class SMMA_Series : Single_TSeries_Indicator
{
    private readonly System.Collections.Generic.List<double> _buffer = new();
    private double _lastsmma, _lastlastsmma;

    public SMMA_Series(TSeries source, int period, bool useNaN = false) : base(source, period, useNaN)
    {
        this._lastsmma = this._lastlastsmma = double.NaN;
        if (this._data.Count > 0) { base.Add(this._data); }
    }

    public override void Add((DateTime t, double v) TValue, bool update)
    {
        double _smma = 0;
        if (update) { this._lastsmma = this._lastlastsmma; }

        if (this.Count < this._p)
        {
            Add_Replace_Trim(_buffer, TValue.v, _p, update);
            _smma = _buffer.Average();
        }
        else
        {
            _smma = ((_lastsmma * (_p-1)) + TValue.v) / _p ;
        }

        this._lastlastsmma = this._lastsmma;
        this._lastsmma = _smma;

        base.Add((TValue.t, _smma), update, _NaN);
        }
}