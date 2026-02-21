using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAVP: Moving Average Variable Period
/// </summary>
/// <remarks>
/// EMA-based moving average where the smoothing period changes per bar.
/// Each bar receives its own period value, clamped to [minPeriod, maxPeriod],
/// producing alpha = 2/(period+1). Adaptive warmup compensator tracks the
/// cumulative product of per-bar (1-alpha) for bias correction.
///
/// Calculation: <c>alpha = 2/(clamp(period)+1); EMA += alpha*(P-EMA); result = EMA/(1-E)</c>.
/// O(1) per bar, zero allocation, no buffer required.
/// </remarks>
/// <seealso href="Mavp.md">Detailed documentation</seealso>
/// <seealso href="mavp.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Mavp : AbstractBase
{
    private readonly int _minPeriod;
    private readonly int _maxPeriod;
    private readonly TValuePublishedHandler _handler;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Ema, double E, bool IsHot, bool IsCompensated, double LastValidValue);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Per-bar effective period. Set this before calling Update(TValue) to control
    /// the smoothing factor for the current bar. Automatically clamped to [minPeriod, maxPeriod].
    /// </summary>
    public double Period { get; set; }

    /// <summary>
    /// Minimum allowed period for clamping.
    /// </summary>
    public int MinPeriod => _minPeriod;

    /// <summary>
    /// Maximum allowed period for clamping.
    /// </summary>
    public int MaxPeriod => _maxPeriod;

    public override bool IsHot => _state.IsHot;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    /// <summary>
    /// Creates MAVP with specified period bounds.
    /// </summary>
    /// <param name="minPeriod">Minimum allowed period (default 2, must be >= 1).</param>
    /// <param name="maxPeriod">Maximum allowed period (default 30, must be >= minPeriod).</param>
    public Mavp(int minPeriod = 2, int maxPeriod = 30)
    {
        if (minPeriod < 1)
        {
            throw new ArgumentException("Minimum period must be >= 1", nameof(minPeriod));
        }

        if (maxPeriod < minPeriod)
        {
            throw new ArgumentException("Maximum period must be >= minimum period", nameof(maxPeriod));
        }

        _minPeriod = minPeriod;
        _maxPeriod = maxPeriod;
        Period = minPeriod;
        _handler = Handle;

        Name = $"Mavp({minPeriod}, {maxPeriod})";
        WarmupPeriod = maxPeriod;

        _state = new State(0, 1.0, false, false, double.NaN);
        _p_state = _state;
    }

    /// <summary>
    /// Creates MAVP subscribed to a source publisher.
    /// </summary>
    /// <param name="source">Source to subscribe to.</param>
    /// <param name="minPeriod">Minimum allowed period (default 2).</param>
    /// <param name="maxPeriod">Maximum allowed period (default 30).</param>
    public Mavp(ITValuePublisher source, int minPeriod = 2, int maxPeriod = 30)
        : this(minPeriod, maxPeriod)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    /// <summary>
    /// Updates MAVP with a value and explicit per-bar period.
    /// </summary>
    /// <param name="input">Price input.</param>
    /// <param name="period">Per-bar effective period (clamped to [minPeriod, maxPeriod]).</param>
    /// <param name="isNew">True if this is a new bar, false for bar correction.</param>
    /// <returns>Updated MAVP value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, double period, bool isNew = true)
    {
        Period = period;
        return Update(input, isNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);
        if (double.IsNaN(val))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Clamp period and compute alpha
        double p = Math.Clamp(Period, _minPeriod, _maxPeriod);
        double alpha = 2.0 / (p + 1.0);
        double beta = 1.0 - alpha;

        // Local copy for JIT struct promotion
        var s = _state;

        // EMA update: ema = ema * beta + alpha * input
        s.Ema = Math.FusedMultiplyAdd(s.Ema, beta, alpha * val);

        double result;
        if (!s.IsCompensated)
        {
            s.E *= beta;

            if (!s.IsHot && s.E <= COVERAGE_THRESHOLD)
            {
                s.IsHot = true;
            }

            if (s.E <= COMPENSATOR_THRESHOLD)
            {
                s.IsCompensated = true;
                result = s.Ema;
            }
            else
            {
                result = s.Ema / (1.0 - s.E);
            }
        }
        else
        {
            result = s.Ema;
        }

        _state = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        double savedPeriod = Period;
        Reset();
        Period = savedPeriod;
        for (int i = 0; i < len; i++)
        {
            vSpan[i] = Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch update with separate period series.
    /// </summary>
    /// <param name="source">Price series.</param>
    /// <param name="periods">Per-bar period series (same length as source).</param>
    /// <returns>Smoothed output series.</returns>
    public TSeries Update(TSeries source, TSeries periods)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        if (source.Count != periods.Count)
        {
            throw new ArgumentException("Source and periods must have the same length", nameof(periods));
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
            Period = periods.Values[i];
            vSpan[i] = Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Batch with fixed period for all bars.
    /// </summary>
    public static TSeries Batch(TSeries source, int minPeriod = 2, int maxPeriod = 30, double fixedPeriod = double.NaN)
    {
        var mavp = new Mavp(minPeriod, maxPeriod);
        if (!double.IsNaN(fixedPeriod))
        {
            mavp.Period = fixedPeriod;
        }
        return mavp.Update(source);
    }

    /// <summary>
    /// Batch with per-bar period series (TSeries).
    /// </summary>
    public static TSeries Batch(TSeries source, TSeries periods, int minPeriod = 2, int maxPeriod = 30)
    {
        var mavp = new Mavp(minPeriod, maxPeriod);
        return mavp.Update(source, periods);
    }

    /// <summary>
    /// High-performance span-based batch with per-bar periods.
    /// </summary>
    /// <param name="source">Input prices.</param>
    /// <param name="periods">Per-bar periods (same length as source).</param>
    /// <param name="output">Output span (same length as source).</param>
    /// <param name="minPeriod">Minimum allowed period.</param>
    /// <param name="maxPeriod">Maximum allowed period.</param>
    public static void Batch(ReadOnlySpan<double> source, ReadOnlySpan<double> periods, Span<double> output, int minPeriod = 2, int maxPeriod = 30)
    {
        if (minPeriod < 1)
        {
            throw new ArgumentException("Minimum period must be >= 1", nameof(minPeriod));
        }

        if (maxPeriod < minPeriod)
        {
            throw new ArgumentException("Maximum period must be >= minimum period", nameof(maxPeriod));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length != periods.Length)
        {
            throw new ArgumentException("Source and periods must have the same length", nameof(periods));
        }

        double ema = 0;
        double e = 1.0;
        bool isCompensated = false;
        double lastValid = double.NaN;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            double p = Math.Clamp(periods[i], minPeriod, maxPeriod);
            double alpha = 2.0 / (p + 1.0);
            double beta = 1.0 - alpha;

            // ema = ema * beta + alpha * val
            ema = Math.FusedMultiplyAdd(ema, beta, alpha * val);

            if (!isCompensated)
            {
                e *= beta;

                if (e <= COMPENSATOR_THRESHOLD)
                {
                    isCompensated = true;
                    output[i] = ema;
                }
                else
                {
                    output[i] = ema / (1.0 - e);
                }
            }
            else
            {
                output[i] = ema;
            }
        }
    }

    /// <summary>
    /// High-performance span-based batch with fixed period.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double fixedPeriod, int minPeriod = 2, int maxPeriod = 30)
    {
        if (minPeriod < 1)
        {
            throw new ArgumentException("Minimum period must be >= 1", nameof(minPeriod));
        }

        if (maxPeriod < minPeriod)
        {
            throw new ArgumentException("Maximum period must be >= minimum period", nameof(maxPeriod));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        double p = Math.Clamp(fixedPeriod, minPeriod, maxPeriod);
        double alpha = 2.0 / (p + 1.0);
        double beta = 1.0 - alpha;

        double ema = 0;
        double e = 1.0;
        bool isCompensated = false;
        double lastValid = double.NaN;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            ema = Math.FusedMultiplyAdd(ema, beta, alpha * val);

            if (!isCompensated)
            {
                e *= beta;

                if (e <= COMPENSATOR_THRESHOLD)
                {
                    isCompensated = true;
                    output[i] = ema;
                }
                else
                {
                    output[i] = ema / (1.0 - e);
                }
            }
            else
            {
                output[i] = ema;
            }
        }
    }

    public static (TSeries Results, Mavp Indicator) Calculate(TSeries source, TSeries periods, int minPeriod = 2, int maxPeriod = 30)
    {
        var indicator = new Mavp(minPeriod, maxPeriod);
        TSeries results = indicator.Update(source, periods);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = new State(0, 1.0, false, false, double.NaN);
        _p_state = _state;
        Period = _minPeriod;
        Last = default;
    }
}
