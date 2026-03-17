using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NET: Ehlers Noise Elimination Technology
/// Applies Kendall Tau-a rank correlation to the input series over a rolling window.
/// Positive output means the series is trending up (concordant pairs dominate);
/// negative means trending down. Output is bounded [-1, +1].
/// </summary>
/// <remarks>
/// Reference: John F. Ehlers, "Noise Elimination Technology" (TASC, December 2020)
///
/// Algorithm:
///   Store last 'period' values in buffer (newest at index Count-1).
///   For each pair (i, k) where i &gt; k (i is older index, k is newer index):
///     Num -= Sign(older - newer)
///   Denom = 0.5 × period × (period - 1)
///   NET = Num / Denom
///
/// Complexity: O(n²) per update where n = period (nested pairwise comparison)
/// No IIR state — purely FIR/windowed from RingBuffer.
/// </remarks>
[SkipLocalsInit]
public sealed class Net : AbstractBase
{
    private readonly int _period;
    private readonly double _denomRecip;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid, int Count);
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes an Ehlers Noise Elimination Technology indicator.
    /// </summary>
    /// <param name="period">Lookback window for Kendall tau (≥ 2). Default: 14.</param>
    public Net(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }

        _period = period;
        _denomRecip = 1.0 / (0.5 * period * (period - 1));
        _buffer = new RingBuffer(period);
        WarmupPeriod = period;
        Name = $"Net({_period})";
    }

    /// <summary>
    /// Initializes a NET indicator and subscribes it to a source publisher.
    /// </summary>
    /// <param name="source">Input data source for event-based chaining.</param>
    /// <param name="period">Lookback window for Kendall tau (≥ 2). Default: 14.</param>
    public Net(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _period;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // State management: direct buffer correction (no Snapshot/Restore)
        // NET reads individual buffer positions, so Snapshot/Restore is unsafe.
        // skipcq: CS-R1140
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // NaN/Infinity guard: substitute last-valid
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = value;
        }

        if (isNew)
        {
            _buffer.Add(value);
            s.Count++;
        }
        else
        {
            _buffer.UpdateNewest(value);
        }

        double result;
        int available = Math.Min(s.Count, _period);

        if (available < 2)
        {
            result = 0.0;
        }
        else
        {
            result = CalcKendallTau(available);
        }

        _s = s;

        var ret = new TValue(input.Time, result);
        Last = ret;
        PubEvent(ret, isNew);
        return ret;
    }

    public override TSeries Update(TSeries source)
    {
        TSeries result = [];
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(Update(source[i]));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalcKendallTau(int windowLen)
    {
        // Kendall Tau-a: count concordant/discordant pairs
        // Buffer: _buffer[0] = oldest, _buffer[Count-1] = newest
        // Map to Ehlers X[]: X[0]=newest=_buffer[Count-1], X[i]=_buffer[Count-1-i]
        //
        // Ehlers loop: for i=1 to N-1, for k=0 to i-1: Num -= Sign(X[i] - X[k])
        // X[i] is older than X[k] (i > k means further back in time)
        // So: Num -= Sign(older - newer)
        // Rising series: older < newer → Sign < 0 → -Sign > 0 → Num > 0 → positive tau

        double num = 0.0;
        int bufCount = _buffer.Count;

        for (int i = 1; i < windowLen; i++)
        {
            double xi = _buffer[bufCount - 1 - i]; // older value (X[i])
            for (int k = 0; k < i; k++)
            {
                double xk = _buffer[bufCount - 1 - k]; // newer value (X[k])
                num -= Math.Sign(xi - xk);
            }
        }

        double denom = 0.5 * windowLen * (windowLen - 1);
        return num * (windowLen == _period ? _denomRecip : 1.0 / denom);
    }

    public static TSeries Batch(TSeries source, int period = 14)
    {
        var indicator = new Net(period);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int period = 14)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination span is shorter than source span.", nameof(destination));
        }
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }

        var filter = new Net(period);
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = filter.Update(new TValue(0, source[i])).Value;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _ps = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        long initialTicks = DateTime.UtcNow.Ticks - source.Length * (step?.Ticks ?? TimeSpan.FromSeconds(1).Ticks);
        TimeSpan increment = step ?? TimeSpan.FromSeconds(1);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(initialTicks + i * increment.Ticks, source[i]));
        }
    }

    public static (TSeries Results, Net Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Net(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Clear();
        }
        base.Dispose(disposing);
    }
}
