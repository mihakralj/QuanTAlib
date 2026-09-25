// ATLOWEST: At-Minimum Predicate
// Crisp predicate: publishes 1.0 when the current bar is the minimum of the trailing n-bar
// window that includes it, 0.0 otherwise, NaN during warmup.

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ATLOWEST: At-Minimum Predicate
/// Tests whether the current value is the minimum of the trailing n-bar window that includes it.
/// </summary>
/// <remarks>
/// Key properties:
/// - Definition: <c>AtLowest(n) = (current == min(current, previous n-1 bars))</c> — inclusive of
///   the current bar; mirrors <see cref="AtHighest"/> exactly using a min-tracking
///   <see cref="MonotonicDeque"/>
/// - Warmup: NaN until n bars have been observed
/// - Exact index comparison, not floating-point equality; see <see cref="AtHighest"/> for the
///   full rationale and the tie-breaking and correction-handling behavior, which apply here
///   unchanged
/// </remarks>
/// <seealso href="AtLowest.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class AtLowest : AbstractBase
{
    private readonly int _n;
    private readonly double[] _buf;
    private readonly MonotonicDeque _minDeque;
    private int _count;
    private long _index;

    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _count >= _n;

    /// <summary>
    /// Initializes a new AtLowest predicate.
    /// </summary>
    /// <param name="n">Window size, current bar included (must be &gt;= 1)</param>
    public AtLowest(int n)
    {
        if (n < 1)
        {
            throw new ArgumentException("n must be >= 1", nameof(n));
        }

        _n = n;
        _buf = new double[n];
        _minDeque = new MonotonicDeque(n);
        _index = -1;
        Name = $"AtLowest({n})";
        WarmupPeriod = n;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Initializes a new AtLowest predicate with source for event-based chaining.
    /// </summary>
    public AtLowest(ITValuePublisher source, int n) : this(n)
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
            _index++;
            if (_count < _n)
            {
                _count++;
            }
        }
        else
        {
            _state = _p_state;
        }

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;
        _state = new State(value);

        int bufIdx = (int)(_index % _n);
        _buf[bufIdx] = value;

        if (isNew)
        {
            _minDeque.PushMin(_index, value, _buf);
        }
        else
        {
            _minDeque.RebuildMin(_buf, _index, _count);
        }

        double result;
        if (_count < _n)
        {
            result = double.NaN;
        }
        else
        {
            result = _minDeque.FrontIndex == _index ? 1.0 : 0.0;
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
        var indicator = new AtLowest(n);
        return indicator.Update(source);
    }

    public static (TSeries Results, AtLowest Indicator) Calculate(TSeries source, int n)
    {
        var indicator = new AtLowest(n);
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
