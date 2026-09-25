// ISFALLING: Falling Predicate
// Crisp predicate: publishes 1.0 when the current value is below the minimum of the n
// previous (confirmed, current bar excluded) values, 0.0 otherwise, NaN during warmup.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ISFALLING: Falling Predicate
/// Tests whether the current value is strictly less than the minimum of the n bars preceding it.
/// </summary>
/// <remarks>
/// Key properties:
/// - Definition: <c>IsFalling(n) = current &lt; min(previous n confirmed values)</c>, current bar
///   excluded, comparison strict (a tie is false)
/// - Warmup: NaN until n confirmed prior bars exist
/// - Mirrors <see cref="IsRising"/> exactly, using a min-tracking <see cref="MonotonicDeque"/>
///   over a window of confirmed prior bars only; see <see cref="IsRising"/> for the full
///   correction-handling rationale (no rebuild needed, since the window never holds the
///   currently forming bar)
/// </remarks>
/// <seealso href="IsFalling.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class IsFalling : AbstractBase
{
    private readonly int _n;
    private readonly double[] _buf;
    private readonly MonotonicDeque _minDeque;
    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid, double Pending, bool HasPending);
    private State _state, _p_state;

    public override bool IsHot => _count >= _n;

    /// <summary>
    /// Initializes a new IsFalling predicate.
    /// </summary>
    /// <param name="n">Number of preceding confirmed bars to compare against (must be &gt;= 1)</param>
    public IsFalling(int n)
    {
        if (n < 1)
        {
            throw new ArgumentException("n must be >= 1", nameof(n));
        }

        _n = n;
        _buf = new double[n];
        _minDeque = new MonotonicDeque(n);
        _index = -1;
        Name = $"IsFalling({n})";
        WarmupPeriod = n + 1;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Initializes a new IsFalling predicate with source for event-based chaining.
    /// </summary>
    public IsFalling(ITValuePublisher source, int n) : this(n)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        double raw = input.Value;
        double lastValid = double.IsFinite(raw) ? raw : _state.LastValid;

        if (isNew && _state.HasPending)
        {
            _index++;
            if (_count < _n)
            {
                _count++;
            }

            int bufIdx = (int)(_index % _n);
            _buf[bufIdx] = _state.Pending;
            _minDeque.PushMin(_index, _state.Pending, _buf);
        }

        _state = new State(lastValid, lastValid, true);

        double result;
        if (_count < _n)
        {
            result = double.NaN;
        }
        else
        {
            result = lastValid < _minDeque.GetExtremum(_buf) ? 1.0 : 0.0;
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
            var tv = Update(new TValue(times[i], values[i]), true);
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

    public static TSeries Batch(TSeries source, int n)
    {
        var indicator = new IsFalling(n);
        return indicator.Update(source);
    }

    public static (TSeries Results, IsFalling Indicator) Calculate(TSeries source, int n)
    {
        var indicator = new IsFalling(n);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        Array.Clear(_buf);
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _state = default;
        _p_state = default;
        Last = new TValue(0, double.NaN);
    }
}
