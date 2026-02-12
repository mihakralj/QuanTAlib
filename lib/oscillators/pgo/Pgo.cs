using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PGO: Pretty Good Oscillator
/// </summary>
/// <remarks>
/// Measures the distance of the current price from its Simple Moving Average,
/// normalized by the Average True Range (ATR). Output is in ATR multiples:
/// <c>PGO = (source − SMA(source, period)) / ATR(period)</c>
///
/// ATR uses EMA smoothing with warmup compensation (PineScript convention).
/// Values above +3 suggest overbought; below −3 suggest oversold.
///
/// References:
///   Mark Johnson, "Pretty Good Oscillator"
///   PineScript reference: pgo.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Pgo : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _decay;
    private readonly RingBuffer _smaBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SmaSum,
        double Ema,
        double E,
        double Atr,
        double PrevClose,
        double LastValid,
        bool Warmup,
        bool HasPrevClose);
    private State _s;
    private State _ps;

    private TValue _pLast;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current PGO value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public bool IsHot => _smaBuffer.IsFull;

    /// <summary>
    /// The number of bars required to warm up the indicator.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Lookback period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Creates PGO with specified period.
    /// </summary>
    /// <param name="period">Lookback period for SMA and ATR (must be &gt; 0)</param>
    public Pgo(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;
        _smaBuffer = new RingBuffer(period);
        WarmupPeriod = period;
        Name = $"Pgo({period})";

        _s = new State(0.0, 0.0, 1.0, 0.0, 0.0, 0.0, true, false);
        _ps = _s;
    }

    /// <summary>
    /// Resets the PGO state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _smaBuffer.Clear();
        _s = new State(0.0, 0.0, 1.0, 0.0, 0.0, 0.0, true, false);
        _ps = _s;
        Last = default;
        _pLast = default;
    }

    /// <summary>
    /// Updates PGO with a new bar (primary API — provides full OHLC for ATR).
    /// </summary>
    /// <param name="input">The new bar data</param>
    /// <param name="isNew">Whether this is a new bar or an update to the last bar</param>
    /// <returns>The updated PGO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double close = input.Close;

        // Sanitize input
        if (!double.IsFinite(close))
        {
            close = double.IsFinite(_s.LastValid) ? _s.LastValid : 0.0;
        }

        if (isNew)
        {
            _ps = _s;
            _pLast = Last;
        }
        else
        {
            _s = _ps;
            Last = _pLast;
        }

        // Update last valid
        if (double.IsFinite(input.Close))
        {
            _s.LastValid = close;
        }

        // --- SMA of close ---
        if (_smaBuffer.Count == _smaBuffer.Capacity)
        {
            _s.SmaSum -= _smaBuffer.Oldest;
        }
        _s.SmaSum += close;

        if (isNew)
        {
            _smaBuffer.Add(close);
        }
        else
        {
            _smaBuffer.UpdateNewest(close);
            // Recalculate sum after UpdateNewest
            _s.SmaSum = 0.0;
            for (int i = 0; i < _smaBuffer.Count; i++)
            {
                _s.SmaSum += _smaBuffer[i];
            }
        }

        double sma = _smaBuffer.Count > 0 ? _s.SmaSum / _smaBuffer.Count : close;

        // --- ATR via EMA(TR) with warmup compensation ---
        double high = double.IsFinite(input.High) ? input.High : close;
        double low = double.IsFinite(input.Low) ? input.Low : close;
        double prevClose = _s.HasPrevClose ? _s.PrevClose : close;

        double tr1 = high - low;
        double tr2 = Math.Abs(high - prevClose);
        double tr3 = Math.Abs(low - prevClose);
        double tr = Math.Max(tr1, Math.Max(tr2, tr3));

        // EMA: ema = alpha * (tr - ema) + ema
        _s.Ema = Math.FusedMultiplyAdd(_alpha, tr - _s.Ema, _s.Ema);

        if (_s.Warmup)
        {
            _s.E *= _decay;
            double c = 1.0 / (1.0 - _s.E);
            _s.Atr = c * _s.Ema;
            _s.Warmup = _s.E > 1e-10;
        }
        else
        {
            _s.Atr = _s.Ema;
        }

        if (isNew)
        {
            _s.PrevClose = close;
            _s.HasPrevClose = true;
        }

        // --- PGO ---
        double pgo = _s.Atr > 0 ? (close - sma) / _s.Atr : 0.0;

        Last = new TValue(input.Time, pgo);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PGO with a new value. Uses value as close; TR = 0 (no OHLC context).
    /// For full accuracy, prefer <see cref="Update(TBar, bool)"/>.
    /// </summary>
    /// <param name="input">The new value (treated as close)</param>
    /// <param name="isNew">Whether this is a new value or an update to the last value</param>
    /// <returns>The updated PGO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double val = input.Value;
        // Create a synthetic bar: O=H=L=C=val → TR = 0 for single values
        return Update(new TBar(input.Time, val, val, val, val, 0), isNew);
    }

    /// <summary>
    /// Updates PGO with a series of bars.
    /// </summary>
    /// <param name="source">The source bar series</param>
    /// <returns>PGO output series</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, v, _period);

        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        var tSpan = CollectionsMarshal.AsSpan(tList);
        source.Open.Times.CopyTo(tSpan);

        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(vList, len);
        var vSpan = CollectionsMarshal.AsSpan(vList);
        v.AsSpan().CopyTo(vSpan);

        // Restore streaming state
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Initializes the indicator state using historical bar data.
    /// </summary>
    /// <param name="source">Historical bar series</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Batch PGO calculation over OHLC spans.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="close">Close prices (used for SMA and TR)</param>
    /// <param name="destination">Output PGO values</param>
    /// <param name="period">Lookback period (default 14)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> destination, int period = 14)
    {
        if (high.Length != low.Length || high.Length != close.Length || high.Length != destination.Length)
        {
            throw new ArgumentException(
                "High, low, close, and destination spans must have the same length.", nameof(destination));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // SMA buffer
        var smaBuffer = new RingBuffer(period);
        double smaSum = 0.0;

        // ATR via EMA with warmup compensation
        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;
        double ema = 0.0;
        double e = 1.0;
        double atr = 0.0;
        bool warmup = true;
        double prevClose = close[0];
        double lastValid = 0.0;

        for (int i = 0; i < len; i++)
        {
            double val = close[i];
            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            // SMA
            if (smaBuffer.Count == smaBuffer.Capacity)
            {
                smaSum -= smaBuffer.Oldest;
            }
            smaSum += val;
            smaBuffer.Add(val);
            double sma = smaSum / smaBuffer.Count;

            // TR
            double h = double.IsFinite(high[i]) ? high[i] : val;
            double l = double.IsFinite(low[i]) ? low[i] : val;
            double pc = i > 0 ? prevClose : val;

            double tr1 = h - l;
            double tr2 = Math.Abs(h - pc);
            double tr3 = Math.Abs(l - pc);
            double tr = Math.Max(tr1, Math.Max(tr2, tr3));

            // EMA of TR
            ema = Math.FusedMultiplyAdd(alpha, tr - ema, ema);

            if (warmup)
            {
                e *= decay;
                double c = 1.0 / (1.0 - e);
                atr = c * ema;
                warmup = e > 1e-10;
            }
            else
            {
                atr = ema;
            }

            prevClose = val;

            destination[i] = atr > 0 ? (val - sma) / atr : 0.0;
        }
    }

    /// <summary>
    /// Calculates PGO for the entire bar series using a stateless batch path.
    /// </summary>
    /// <param name="source">Input bar series</param>
    /// <param name="period">Lookback period (default 14)</param>
    /// <returns>PGO output series</returns>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, v, period);

        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        var tSpan = CollectionsMarshal.AsSpan(tList);
        source.Open.Times.CopyTo(tSpan);

        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(vList, len);
        var vSpan = CollectionsMarshal.AsSpan(vList);
        v.AsSpan().CopyTo(vSpan);

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Calculates PGO for the entire series, returning both results and indicator.
    /// </summary>
    public static (TSeries Results, Pgo Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Pgo(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
