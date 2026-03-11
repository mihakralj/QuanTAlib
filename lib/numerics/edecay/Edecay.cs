using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EDECAY: Exponential Decay
/// </summary>
/// <remarks>
/// Tracks the maximum of the current input and the previous output multiplied by
/// a decay factor of (period-1)/period per bar. When price is rising or flat the
/// output follows price; when price drops the output decays exponentially toward it.
///
/// Calculation: <c>output = max(input, prev_output * (period-1)/period)</c>.
/// Origin: Tulip Indicators (ti_edecay).
/// </remarks>
/// <seealso href="Edecay.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Edecay : AbstractBase
{
    private readonly double _scale;
    private int _count;
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid, double LastOutput);
    private State _state, _p_state;
    private int _p_count;
    private ITValuePublisher? _source;
    private bool _disposed;

    public override bool IsHot => _count > 0;

    /// <summary>
    /// Initializes a new Exponential Decay indicator with specified period.
    /// </summary>
    /// <param name="period">Decay period (must be >= 1)</param>
    public Edecay(int period = 5)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _scale = (period - 1.0) / period;
        Name = $"Edecay({period})";
        WarmupPeriod = 1;
    }

    /// <summary>
    /// Initializes a new Exponential Decay indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="period">Decay period</param>
    public Edecay(ITValuePublisher source, int period = 5) : this(period)
    {
        _source = source;
        _source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_count = _count;
        }
        else
        {
            _state = _p_state;
            _count = _p_count;
        }

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;

        double result;
        if (_count == 0)
        {
            result = value;
        }
        else
        {
            double decayed = _state.LastOutput * _scale;
            result = value > decayed ? value : decayed;
        }

        _state = new State(value, result);
        if (isNew)
        {
            _count++;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }
        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int period = 5)
    {
        var indicator = new Edecay(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates exponential decay over a span of values. Zero-allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 5)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        double scale = (period - 1.0) / period;
        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        Unsafe.Add(ref outRef, 0) = Unsafe.Add(ref srcRef, 0);

        for (int i = 1; i < source.Length; i++)
        {
            double d = Unsafe.Add(ref outRef, i - 1) * scale;
            double s = Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref outRef, i) = s > d ? s : d;
        }
    }

    public static (TSeries Results, Edecay Indicator) Calculate(TSeries source, int period = 5)
    {
        var indicator = new Edecay(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _count = 0;
        _p_count = 0;
        _state = default;
        _p_state = default;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= HandleUpdate;
                _source = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
