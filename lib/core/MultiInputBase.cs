using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Abstract base class for stateless, per-bar nodes with a fixed number of inputs (2 to 8),
/// per the strategy-primitives specification (Section 8.3).
/// </summary>
/// <remarks>
/// <para>The current concrete users all take exactly 3 inputs (<see cref="InsideChannel"/>,
/// <see cref="OutsideChannel"/>, <see cref="ChannelPosition"/>: value, lower, upper), so this
/// base is exercised only at <c>inputCount = 3</c> today. It is written for up to 8 inputs
/// because the spec's Layer 4 combinators (<c>AllOf</c>, <c>AnyOf</c>, <c>KOfN</c>) will need
/// more; generalizing further (a true unbounded-N node, needed once combinators exceed 8
/// children) is deferred until that concrete case exists, per the "build the base together with
/// its first real consumer" rule this repository follows for infrastructure classes.</para>
///
/// <para><b>Time-join:</b> event-chained construction applies the same rule as
/// <see cref="BiInputIndicatorBase.Subscribe"/>: a result is only computed once every input has
/// reported for the same bar <c>Time</c>. An input reporting a newer <c>Time</c> before the
/// others have caught up starts a new pending set and drops the unmatched partial set.</para>
///
/// <para><b>NaN-substitution:</b> each input is sanitized independently (last-valid-value
/// substitution), mirroring <see cref="BiInputIndicatorBase"/>.</para>
///
/// <para><b>Cold state:</b> <see cref="IsHot"/> is false, and <see cref="AbstractBase.Last"/> is
/// <c>NaN</c>, until the first full set of inputs has been processed — the same "hot after the
/// first bar" contract as <c>numerics/Add</c> and the <see cref="ComparePredicateBase{TOp}"/>
/// predicates, appropriate here because these predicates need no history, only a value for
/// every input at the same bar.</para>
/// </remarks>
[SkipLocalsInit]
public abstract class MultiInputBase : AbstractBase
{
    private const int MaxInputs = 8;

    /// <summary>Fixed number of inputs this node was constructed with.</summary>
    protected readonly int InputCount;

    private readonly double[] _lastValid;
    private readonly double[] _p_lastValid;
    private readonly double[] _pending;

    private bool _hasValue;
    private bool _p_hasValue;

    private long _joinTime = long.MinValue;
    private int _seenMask;

    /// <summary>
    /// True once the first full set of inputs (one value per input, for the same bar) has been
    /// processed.
    /// </summary>
    public override bool IsHot => _hasValue;

    /// <summary>
    /// Creates a multi-input node with a fixed input count.
    /// </summary>
    /// <param name="inputCount">Number of inputs (2 to 8)</param>
    /// <param name="name">Node name for diagnostics</param>
    protected MultiInputBase(int inputCount, string name)
    {
        if (inputCount is < 2 or > MaxInputs)
        {
            throw new ArgumentException($"Input count must be between 2 and {MaxInputs}", nameof(inputCount));
        }

        InputCount = inputCount;
        _lastValid = new double[inputCount];
        _p_lastValid = new double[inputCount];
        _pending = new double[inputCount];
        Name = name;
        WarmupPeriod = 0;
        Last = new TValue(0, double.NaN);
    }

    /// <summary>
    /// Computes the result from the sanitized (finite) current-bar values, in input-index order.
    /// </summary>
    protected abstract double Compute(ReadOnlySpan<double> values);

    /// <summary>
    /// Wires this node to <see cref="InputCount"/> upstream publishers, in index order, using the
    /// time-join rule described in the class remarks.
    /// </summary>
    protected void Subscribe(params ITValuePublisher[] sources)
    {
        if (sources.Length != InputCount)
        {
            throw new ArgumentException($"Expected {InputCount} sources, got {sources.Length}", nameof(sources));
        }

        for (int i = 0; i < sources.Length; i++)
        {
            // Bind the index once at construction (not a hot-path closure): each of up to 8
            // subscriptions is wired exactly once and never re-created.
            int index = i;
            sources[i].Pub += (object? _, in TValueEventArgs e) => HandleJoined(index, e.Value, e.IsNew);
        }
    }

    private void HandleJoined(int index, TValue value, bool isNew)
    {
        long time = value.Time;

        if (time != _joinTime)
        {
            _joinTime = time;
            _seenMask = 0;
        }

        _pending[index] = value.Value;
        _seenMask |= 1 << index;

        int fullMask = (1 << InputCount) - 1;
        if (_seenMask == fullMask)
        {
            Span<TValue> inputs = stackalloc TValue[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                inputs[i] = new TValue(value.Time, _pending[i]);
            }

            Update(inputs, isNew);
        }
    }

    /// <summary>
    /// Updates the node with one current-bar value per input, in index order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(ReadOnlySpan<TValue> inputs, bool isNew = true)
    {
        if (inputs.Length != InputCount)
        {
            throw new ArgumentException($"Expected {InputCount} inputs, got {inputs.Length}", nameof(inputs));
        }

        if (isNew)
        {
            Array.Copy(_lastValid, _p_lastValid, InputCount);
            _p_hasValue = _hasValue;
        }
        else
        {
            Array.Copy(_p_lastValid, _lastValid, InputCount);
            _hasValue = _p_hasValue;
        }

        Span<double> sanitized = stackalloc double[InputCount];
        for (int i = 0; i < InputCount; i++)
        {
            double v = inputs[i].Value;
            if (double.IsFinite(v))
            {
                _lastValid[i] = v;
                sanitized[i] = v;
            }
            else
            {
                sanitized[i] = _lastValid[i];
            }
        }

        double result = Compute(sanitized);
        _hasValue = true;

        Last = new TValue(inputs[0].Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Convenience overload for exactly 3 inputs (value, lower, upper), the shape used by every
    /// current consumer of this base.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue a, TValue b, TValue c, bool isNew = true)
    {
        if (InputCount != 3)
        {
            throw new InvalidOperationException($"{Name} requires {InputCount} inputs; use Update(ReadOnlySpan<TValue>, bool).");
        }

        Span<TValue> inputs = [a, b, c];
        return Update(inputs, isNew);
    }

    public override TValue Update(TValue input, bool isNew = true) =>
        throw new NotSupportedException($"{Name} requires {InputCount} inputs. Use Update(ReadOnlySpan<TValue>, bool) or the fixed-arity overload.");

    public override TSeries Update(TSeries source) =>
        throw new NotSupportedException($"{Name} requires {InputCount} inputs; there is no single-series form.");

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null) =>
        throw new NotSupportedException($"{Name} requires {InputCount} inputs; there is no single-series form.");

    public override void Reset()
    {
        Array.Clear(_lastValid);
        Array.Clear(_p_lastValid);
        Array.Clear(_pending);
        _hasValue = false;
        _p_hasValue = false;
        _joinTime = long.MinValue;
        _seenMask = 0;
        Last = new TValue(0, double.NaN);
    }
}
