using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Jvoltyn: Normalized Jurik Volatility
/// </summary>
/// <remarks>
/// Normalized version of Jvolty that maps the dynamic exponent to a 0-100 scale.
/// Output of 0 indicates minimum volatility, 100 indicates maximum volatility.
///
/// Normalization: <c>Jvoltyn = ((d - 1) / (logParam - 1)) × 100</c>
/// where d is the raw Jurik dynamic exponent in range [1, logParam].
///
/// Key features:
/// - Same adaptive volatility calculation as Jvolty
/// - Output normalized to 0-100 for easy interpretation
/// - 0 = low volatility regime, 100 = high volatility regime
/// </remarks>
/// <seealso href="Jvoltyn.md">Detailed documentation</seealso>
/// <seealso cref="Jvolty">Raw Jurik Volatility indicator</seealso>
[SkipLocalsInit]
public sealed class Jvoltyn : AbstractBase
{
    private const int VolWindowSize = 128; // volatility history length
    private const int DevWindowSize = 10;  // short SMA length for deviation
    private const int JurikTrimCount = 65; // canonical JMA: middle 65 of 128 samples

    // Jurik core parameters derived from period
    private readonly double _logParam;       // log(sqrt(L))/log(2) + 2, clamped >= 0
    private readonly double _pExponent;      // max(logParam - 2, 0.5)
    private readonly double _sqrtDivider;    // sqrt(L)*logParam / (sqrt(L)*logParam + 1)
    private readonly double _normFactor;     // 100 / (logParam - 1) for fast normalization

    // Buffers
    private readonly RingBuffer _devBuffer;
    private readonly RingBuffer _volBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;

    // Streaming state (current + previous snapshot for isNew=false)
    private State _s;
    private State _ps;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        // Jurik "envelope" anchors (volatility bands)
        public double UpperBand;
        public double LowerBand;

        // last finite price (for NaN handling)
        public double LastPrice;

        // last computed raw volatility (d value)
        public double LastVolty;

        // counters
        public int Bars;
    }

    /// <summary>
    /// Gets the upper volatility band value.
    /// </summary>
    public double UpperBand => _s.UpperBand;

    /// <summary>
    /// Gets the lower volatility band value.
    /// </summary>
    public double LowerBand => _s.LowerBand;

    /// <summary>
    /// Gets the raw (non-normalized) Jurik volatility value in range [1, logParam].
    /// </summary>
    public double RawVolatility => _s.LastVolty;

    public override bool IsHot => _s.Bars >= WarmupPeriod;

    /// <summary>
    /// Creates Jvoltyn with specified period.
    /// </summary>
    /// <param name="period">Period for volatility calculation (must be >= 1)</param>
    public Jvoltyn(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        // --- Length / log / divider parameters (from decompiled JMA) ---
        // L_raw ~ (period - 1)/2, with a tiny lower bound to avoid log(0)
        double lengthParam = period < 1.0000000002
            ? 0.0000000001
            : (period - 1.0) / 2.0;

        double logParam = Math.Log(Math.Sqrt(lengthParam)) / Math.Log(2.0);
        logParam = (logParam + 2.0) < 0.0 ? 0.0 : (logParam + 2.0);
        _logParam = logParam;
        _pExponent = Math.Max(_logParam - 2.0, 0.5);

        double sqrtParam = Math.Sqrt(lengthParam) * _logParam;
        _sqrtDivider = sqrtParam / (sqrtParam + 1.0);

        // Normalization factor: maps [1, logParam] -> [0, 100]
        // Avoid division by zero when logParam == 1
        _normFactor = Math.Abs(_logParam - 1.0) > 1e-10 ? 100.0 / (_logParam - 1.0) : 0.0;

        // same warmup heuristic used in JMA
        WarmupPeriod = (int)Math.Ceiling(20.0 + 80.0 * Math.Pow(period, 0.36));

        _handler = Handle;
        Name = $"Jvoltyn({period})";

        _devBuffer = new RingBuffer(DevWindowSize);
        _volBuffer = new RingBuffer(VolWindowSize);

        Reset();
    }

    /// <summary>
    /// Creates Jvoltyn with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for volatility calculation</param>
    public Jvoltyn(ITValuePublisher source, int period)
        : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _s = default;
        _ps = default;
        _devBuffer.Clear();
        _volBuffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Core streaming step: feed a single value, get normalized Jvoltyn (0-100).
    /// Honors isNew semantics by snapshotting state+buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Step(double value, bool isNew)
    {
        HandleStateSnapshot(isNew);
        if (!double.IsFinite(value))
        {
            if (_s.Bars == 0)
            {
                return double.NaN;
            }

            value = _s.LastPrice;
        }
        else
        {
            _s.LastPrice = value;
        }

        _s.Bars++;
        if (_s.Bars == 1)
        {
            return InitializeFirstBar(value);
        }

        return CalculateJvoltyn(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            _devBuffer.Snapshot();
            _volBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _devBuffer.Restore();
            _volBuffer.Restore();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InitializeFirstBar(double value)
    {
        _s.UpperBand = value;
        _s.LowerBand = value;
        _s.LastVolty = 1.0; // minimum volatility
        return 0.0; // Normalized: d=1 maps to 0
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateJvoltyn(double value)
    {
        // 1. Local deviation: |price - {UpperBand, LowerBand}|
        double diffA = value - _s.UpperBand;
        double diffB = value - _s.LowerBand;
        double absA = Math.Abs(diffA);
        double absB = Math.Abs(diffB);
        double absValue = absA > absB ? absA : absB;
        double deviation = absValue + 1e-10;

        // 2. 10-bar SMA of local deviation -> "volatility"
        _devBuffer.Add(deviation);
        double volatility = _devBuffer.Average;

        // 3. 128-bar volatility history + middle-65 trimmed mean
        _volBuffer.Add(volatility);
        double refVolatility = CalculateTrimmedMean(volatility);
        refVolatility = refVolatility <= 0.0 ? deviation : refVolatility;

        // 4. Jurik dynamic exponent d from abs/refVolatility
        double d = CalculateJurikExponent(absValue, refVolatility);

        // 5. Update UpperBand / LowerBand using sqrtDivider ^ sqrt(d)
        UpdateBands(value, d);

        _s.LastVolty = d;

        // 6. Normalize d from [1, logParam] to [0, 100]
        return (d - 1.0) * _normFactor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateJurikExponent(double absValue, double refVolatility)
    {
        double ratio = Math.Max(absValue / refVolatility, 0.0);
        double d = Math.Pow(ratio, _pExponent);
        if (d > _logParam)
        {
            d = _logParam;
        }

        if (d < 1.0)
        {
            d = 1.0;
        }

        return d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateBands(double value, double d)
    {
        double adapt = Math.Pow(_sqrtDivider, Math.Sqrt(d));
        _s.UpperBand = (value > _s.UpperBand)
            ? value
            : Math.FusedMultiplyAdd(adapt, _s.UpperBand - value, value);
        _s.LowerBand = (value < _s.LowerBand)
            ? value
            : Math.FusedMultiplyAdd(adapt, _s.LowerBand - value, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double volty = Step(input.Value, isNew);
        Last = new TValue(input.Time, volty);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            vSpan[i] = Step(source.Values[i], isNew: true);
        }

        // Synchronize previous-state mirror to current state AND snapshot buffers
        _ps = _s;
        _devBuffer.Snapshot();
        _volBuffer.Snapshot();

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null)
        {
            _source.Pub -= _handler;
        }
        base.Dispose(disposing);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates Jvoltyn for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var jvoltyn = new Jvoltyn(period);
        return jvoltyn.Update(source);
    }

    /// <summary>
    /// Static helper for span-based calculation.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source,
                                 Span<double> output,
                                 int period)
    {
        if (output.Length != source.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        var jvoltyn = new Jvoltyn(period);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = jvoltyn.Step(source[i], isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateTrimmedMean(double fallback)
    {
        int count = _volBuffer.Count;
        if (count < 16)
        {
            return fallback;
        }

        // Stack-allocate scratch buffer for sorting (max 128 * 8 bytes = 1KB)
        Span<double> sorted = stackalloc double[count];
        _volBuffer.CopyTo(sorted);
        sorted.Sort();

        int start, end;
        if (count >= VolWindowSize)
        {
            // canonical JMA: central 65 of 128 -> indices 32..96
            int leftSkip = (int)Math.Ceiling((VolWindowSize - JurikTrimCount) / 2.0);
            start = leftSkip;
            end = start + JurikTrimCount - 1;
        }
        else
        {
            // for shorter history, use central ~50% as a reasonable proxy
            int slice = (int)Math.Max(5, Math.Round(count * 0.5));
            int drop = (count - slice) / 2;
            start = drop;
            end = drop + slice - 1;
        }

        if (start < 0)
        {
            start = 0;
        }

        if (end >= count)
        {
            end = count - 1;
        }

        int len = end - start + 1;
        return sorted.Slice(start, len).SumSIMD() / len;
    }
}