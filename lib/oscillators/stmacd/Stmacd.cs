using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// STMACD: Stochastic MACD Oscillator (Vitali Apirine, TASC Nov 2019).
/// Normalises the classic MACD line by the high–low range over a lookback window:
///   FastStoch = (EMA(close, fast) − Lowest(low, periods)) / (Highest(high, periods) − Lowest(low, periods))
///   SlowStoch = (EMA(close, slow) − Lowest(low, periods)) / (Highest(high, periods) − Lowest(low, periods))
///   STMACD    = 100 × (FastStoch − SlowStoch) = 100 × MACD / PriceRange
///   Signal    = EMA(STMACD, signalLength)
/// Dual output: STMACD line + Signal line.  Typical OB/OS: +10 / −10.
/// </summary>
/// <remarks>
/// Streaming path uses <see cref="MonotonicDeque"/> for O(1) amortised Highest/Lowest;
/// three inline EMA accumulators (fast, slow, signal) avoid allocating sub-indicators.
/// </remarks>
[SkipLocalsInit]
public sealed class Stmacd : ITValuePublisher
{
    private const int DefaultPeriods = 45;
    private const int DefaultFastLength = 12;
    private const int DefaultSlowLength = 26;
    private const int DefaultSignalLength = 9;

    private readonly int _periods;
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly int _signalLength;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly double _signalAlpha;

    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double FastEma, double SlowEma, double SignalEma,
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }

    /// <summary>The STMACD oscillator value for the current bar.</summary>
    public TValue StmacdValue { get; private set; }

    /// <summary>The signal line (EMA of STMACD) for the current bar.</summary>
    public TValue Signal { get; private set; }

    public bool IsHot => _count >= _periods;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a new STMACD indicator with the specified parameters.
    /// </summary>
    /// <param name="periods">Lookback window for Highest(high)/Lowest(low). Default 45.</param>
    /// <param name="fastLength">Fast EMA period. Default 12.</param>
    /// <param name="slowLength">Slow EMA period. Default 26.</param>
    /// <param name="signalLength">Signal EMA period. Default 9.</param>
    public Stmacd(int periods = DefaultPeriods, int fastLength = DefaultFastLength,
        int slowLength = DefaultSlowLength, int signalLength = DefaultSignalLength)
    {
        if (periods <= 0)
        {
            throw new ArgumentException("Periods must be greater than 0", nameof(periods));
        }
        if (fastLength <= 0)
        {
            throw new ArgumentException("Fast length must be greater than 0", nameof(fastLength));
        }
        if (slowLength <= 0)
        {
            throw new ArgumentException("Slow length must be greater than 0", nameof(slowLength));
        }
        if (signalLength <= 0)
        {
            throw new ArgumentException("Signal length must be greater than 0", nameof(signalLength));
        }

        _periods = periods;
        _fastLength = fastLength;
        _slowLength = slowLength;
        _signalLength = signalLength;
        _fastAlpha = 2.0 / (_fastLength + 1);
        _slowAlpha = 2.0 / (_slowLength + 1);
        _signalAlpha = 2.0 / (_signalLength + 1);

        _hBuf = new double[_periods];
        _lBuf = new double[_periods];
        _maxDeque = new MonotonicDeque(_periods);
        _minDeque = new MonotonicDeque(_periods);

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"STMACD({periods},{fastLength},{slowLength},{signalLength})";
        WarmupPeriod = periods;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a STMACD indicator and immediately primes it from a source series.
    /// </summary>
    public Stmacd(TBarSeries source, int periods = DefaultPeriods,
        int fastLength = DefaultFastLength, int slowLength = DefaultSlowLength,
        int signalLength = DefaultSignalLength)
        : this(periods, fastLength, slowLength, signalLength)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Updates the indicator with a new TBar value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            if (_count < _periods)
            {
                _count++;
            }
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Guard inputs — substitute last-valid on NaN/Infinity
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            StmacdValue = new TValue(input.Time, double.NaN);
            Signal = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Update circular buffers for Highest/Lowest
        int bufIdx = _index < 0 ? 0 : (int)(_index % _periods);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxDeque.PushMax(_index, high, _hBuf);
            _minDeque.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxDeque.RebuildMax(_hBuf, _index, _count);
            _minDeque.RebuildMin(_lBuf, _index, _count);
        }

        double highest = _maxDeque.GetExtremum(_hBuf);
        double lowest = _minDeque.GetExtremum(_lBuf);

        // Update EMAs of close (inline)
        double fastEma, slowEma;
        if (double.IsNaN(s.FastEma))
        {
            // First valid bar — seed EMAs with close
            fastEma = close;
            slowEma = close;
        }
        else
        {
            fastEma = _fastAlpha * close + (1.0 - _fastAlpha) * s.FastEma;
            slowEma = _slowAlpha * close + (1.0 - _slowAlpha) * s.SlowEma;
        }

        // Compute STMACD = 100 * (FastStoch - SlowStoch)
        double range = highest - lowest;
        double stmacd;
        if (range > 0.0)
        {
            double fastStoch = (fastEma - lowest) / range;
            double slowStoch = (slowEma - lowest) / range;
            stmacd = (fastStoch - slowStoch) * 100.0;
        }
        else
        {
            stmacd = 0.0;
        }

        // Update signal EMA
        double signalEma;
        if (double.IsNaN(s.SignalEma))
        {
            signalEma = stmacd;
        }
        else
        {
            signalEma = _signalAlpha * stmacd + (1.0 - _signalAlpha) * s.SignalEma;
        }

        s.FastEma = fastEma;
        s.SlowEma = slowEma;
        s.SignalEma = signalEma;
        _s = s;

        StmacdValue = new TValue(input.Time, stmacd);
        Signal = new TValue(input.Time, signalEma);
        Last = new TValue(input.Time, stmacd);

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a single TValue (treated as H=L=C=value).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    /// <summary>
    /// Batch-updates from a TBarSeries and returns dual output series.
    /// </summary>
    public (TSeries Stmacd, TSeries Signal) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tS = new List<long>(len);
        var vS = new List<double>(len);
        var tSig = new List<long>(len);
        var vSig = new List<double>(len);

        CollectionsMarshal.SetCount(tS, len);
        CollectionsMarshal.SetCount(vS, len);
        CollectionsMarshal.SetCount(tSig, len);
        CollectionsMarshal.SetCount(vSig, len);

        var vSSpan = CollectionsMarshal.AsSpan(vS);
        var vSigSpan = CollectionsMarshal.AsSpan(vSig);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            vSSpan, vSigSpan, _periods, _fastLength, _slowLength, _signalLength);

        var tSpan = CollectionsMarshal.AsSpan(tS);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSig));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        StmacdValue = new TValue(lastTime, vSSpan[^1]);
        Signal = new TValue(lastTime, vSigSpan[^1]);
        Last = new TValue(lastTime, vSSpan[^1]);

        return (new TSeries(tS, vS), new TSeries(tSig, vSig));
    }

    /// <summary>Primes the indicator from historical data.</summary>
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

    /// <summary>Resets the indicator to its initial state.</summary>
    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        _maxDeque.Reset();
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
        StmacdValue = default;
        Signal = default;
    }

    /// <summary>
    /// Batch computation over raw spans — zero-allocation hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> stmacdOut,
        Span<double> signalOut,
        int periods = DefaultPeriods,
        int fastLength = DefaultFastLength,
        int slowLength = DefaultSlowLength,
        int signalLength = DefaultSignalLength)
    {
        if (periods <= 0)
        {
            throw new ArgumentException("Periods must be greater than 0", nameof(periods));
        }
        if (fastLength <= 0)
        {
            throw new ArgumentException("Fast length must be greater than 0", nameof(fastLength));
        }
        if (slowLength <= 0)
        {
            throw new ArgumentException("Slow length must be greater than 0", nameof(slowLength));
        }
        if (signalLength <= 0)
        {
            throw new ArgumentException("Signal length must be greater than 0", nameof(signalLength));
        }
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        }
        if (stmacdOut.Length < high.Length)
        {
            throw new ArgumentException("STMACD output span must be at least as long as input", nameof(stmacdOut));
        }
        if (signalOut.Length < high.Length)
        {
            throw new ArgumentException("Signal output span must be at least as long as input", nameof(signalOut));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Compute Highest(high) and Lowest(low) via batch helpers
        const int StackallocThreshold = 256;
        double[]? rentedUpper = null;
        double[]? rentedLower = null;
        scoped Span<double> upperBuf;
        scoped Span<double> lowerBuf;

        if (len <= StackallocThreshold)
        {
            upperBuf = stackalloc double[len];
            lowerBuf = stackalloc double[len];
        }
        else
        {
            rentedUpper = ArrayPool<double>.Shared.Rent(len);
            rentedLower = ArrayPool<double>.Shared.Rent(len);
            upperBuf = rentedUpper.AsSpan(0, len);
            lowerBuf = rentedLower.AsSpan(0, len);
        }

        try
        {
            Highest.Batch(high, upperBuf, periods);
            Lowest.Batch(low, lowerBuf, periods);

            double fastAlpha = 2.0 / (fastLength + 1);
            double slowAlpha = 2.0 / (slowLength + 1);
            double sigAlpha = 2.0 / (signalLength + 1);

            double fastEma = close[0];
            double slowEma = close[0];
            double signalEma = 0.0;
            bool signalSeeded = false;

            for (int i = 0; i < len; i++)
            {
                if (i == 0)
                {
                    fastEma = close[0];
                    slowEma = close[0];
                }
                else
                {
                    fastEma = fastAlpha * close[i] + (1.0 - fastAlpha) * fastEma;
                    slowEma = slowAlpha * close[i] + (1.0 - slowAlpha) * slowEma;
                }

                double range = upperBuf[i] - lowerBuf[i];
                double stmacd;
                if (range > 0.0)
                {
                    double fastStoch = (fastEma - lowerBuf[i]) / range;
                    double slowStoch = (slowEma - lowerBuf[i]) / range;
                    stmacd = (fastStoch - slowStoch) * 100.0;
                }
                else
                {
                    stmacd = 0.0;
                }

                stmacdOut[i] = stmacd;

                if (!signalSeeded)
                {
                    signalEma = stmacd;
                    signalSeeded = true;
                }
                else
                {
                    signalEma = sigAlpha * stmacd + (1.0 - sigAlpha) * signalEma;
                }

                signalOut[i] = signalEma;
            }
        }
        finally
        {
            if (rentedUpper != null)
            {
                ArrayPool<double>.Shared.Return(rentedUpper);
            }
            if (rentedLower != null)
            {
                ArrayPool<double>.Shared.Return(rentedLower);
            }
        }
    }

    /// <summary>
    /// Convenience batch method that returns TSeries tuples.
    /// </summary>
    public static (TSeries Stmacd, TSeries Signal) Batch(TBarSeries source,
        int periods = DefaultPeriods, int fastLength = DefaultFastLength,
        int slowLength = DefaultSlowLength, int signalLength = DefaultSignalLength)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tS = new List<long>(len);
        var vS = new List<double>(len);
        var tSig = new List<long>(len);
        var vSig = new List<double>(len);

        CollectionsMarshal.SetCount(tS, len);
        CollectionsMarshal.SetCount(vS, len);
        CollectionsMarshal.SetCount(tSig, len);
        CollectionsMarshal.SetCount(vSig, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vS),
            CollectionsMarshal.AsSpan(vSig),
            periods, fastLength, slowLength, signalLength);

        var tSpan = CollectionsMarshal.AsSpan(tS);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSig));

        return (new TSeries(tS, vS), new TSeries(tSig, vSig));
    }

    /// <summary>
    /// Creates and primes a STMACD indicator, returning both results and indicator.
    /// </summary>
    public static ((TSeries Stmacd, TSeries Signal) Results, Stmacd Indicator) Calculate(
        TBarSeries source, int periods = DefaultPeriods,
        int fastLength = DefaultFastLength, int slowLength = DefaultSlowLength,
        int signalLength = DefaultSignalLength)
    {
        var indicator = new Stmacd(periods, fastLength, slowLength, signalLength);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
