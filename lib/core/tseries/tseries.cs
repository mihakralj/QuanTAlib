using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Tag type indicating that a TSeries constructor should share storage with the provided lists
/// rather than making defensive copies. Used internally for synchronized sub-series.
/// </summary>
public readonly struct ShareStorageTag
{
    /// <summary>
    /// Singleton instance for the share storage tag.
    /// </summary>
    public static readonly ShareStorageTag Instance = default;
}

/// <summary>
/// High-performance enumerator for TSeries.
/// </summary>
public struct TSeriesEnumerator : IEnumerator<TValue>, IEquatable<TSeriesEnumerator>
{
    private readonly List<long> _t;
    private readonly List<double> _v;
    private readonly int _count;
    private int _index;
    private TValue _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TSeriesEnumerator(List<long> t, List<double> v)
    {
        _t = t;
        _v = v;
        _count = v.Count;
        _index = -1;
        _current = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_index + 1 >= _count)
            return false;

        _index++;
        _current = new TValue(_t[_index], _v[_index]);
        return true;
    }

    public readonly TValue Current => _current;
    readonly object IEnumerator.Current => Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _index = -1;
        _current = default;
    }

    public readonly void Dispose() { }

    public readonly bool Equals(TSeriesEnumerator other) =>
        ReferenceEquals(_t, other._t) &&
        ReferenceEquals(_v, other._v) &&
        _count == other._count &&
        _index == other._index;

    public override readonly bool Equals(object? obj) =>
        obj is TSeriesEnumerator other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_t), RuntimeHelpers.GetHashCode(_v), _count, _index);

    public static bool operator ==(TSeriesEnumerator left, TSeriesEnumerator right) => left.Equals(right);
    public static bool operator !=(TSeriesEnumerator left, TSeriesEnumerator right) => !left.Equals(right);
}

/// <summary>
/// A high-performance time series implementation using Structure of Arrays (SoA) layout.
/// Stores Time (long) and Value (double) in separate contiguous arrays for SIMD efficiency.
/// Supports "New Bar" vs "Update Last" streaming semantics.
/// </summary>
public class TSeries : IReadOnlyList<TValue>, ITValuePublisher
{
#pragma warning disable MA0016 // Prefer using collection abstraction instead of implementation
    protected readonly List<long> _t;
    protected readonly List<double> _v;
#pragma warning restore MA0016

    public string Name { get; set; } = "Data";

    public event TValuePublishedHandler? Pub;

    public TSeries() : this(0)
    {
    }

    public TSeries(int capacity)
    {
        _t = new List<long>(capacity);
        _v = new List<double>(capacity);
    }

    public TSeries(IReadOnlyList<long> time, IReadOnlyList<double> values)
    {
        // Always make defensive copies to prevent external mutation of internal state
        _t = [.. time];
        _v = [.. values];
    }

    /// <summary>
    /// Internal constructor for creating views that share underlying storage.
    /// Used by TBarSeries to create synchronized OHLCV sub-series.
    /// </summary>
    /// <param name="time">The time list to share (not copied).</param>
    /// <param name="values">The values list to share (not copied).</param>
    /// <param name="_">Tag type indicating intentional storage sharing.</param>
    internal TSeries(List<long> time, List<double> values, ShareStorageTag _)
    {
        // Direct assignment for intentional storage sharing (internal use only)
        _t = time;
        _v = values;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _v.Count;
    }

    public TValue this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new TValue(_t[index], _v[index]);
    }

    public TValue Last
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _v.Count > 0 ? new(_t[^1], _v[^1]) : default;
    }

    public double LastValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _v.Count > 0 ? _v[^1] : double.NaN;
    }

    public long LastTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _t.Count > 0 ? _t[^1] : 0;
    }

    /// <summary>
    /// Direct access to the underlying Value array as a Span for SIMD operations.
    /// </summary>
    /// <remarks>
    /// <para><b>Lifetime:</b> The returned span is only valid while no structural mutations
    /// (Add, Clear, etc.) occur on this TSeries. Structural changes invalidate the span.</para>
    /// <para><b>Mutation:</b> The span reflects the internal List storage; modifications
    /// to the series after obtaining the span may cause undefined behavior if the span is still in use.</para>
    /// </remarks>
    public ReadOnlySpan<double> Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_v);
    }

    /// <summary>
    /// Direct access to the underlying Time array as a Span.
    /// </summary>
    /// <remarks>
    /// <para><b>Lifetime:</b> The returned span is only valid while no structural mutations
    /// (Add, Clear, etc.) occur on this TSeries. Structural changes invalidate the span.</para>
    /// <para><b>Mutation:</b> The span reflects the internal List storage; modifications
    /// to the series after obtaining the span may cause undefined behavior if the span is still in use.</para>
    /// </remarks>
    public ReadOnlySpan<long> Times
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(TValue value, bool isNew)
    {
        if (isNew || _v.Count == 0)
        {
            _t.Add(value.Time);
            _v.Add(value.Value);
        }
        else
        {
            int lastIdx = _v.Count - 1;
            _t[lastIdx] = value.Time;
            _v[lastIdx] = value.Value;
        }

        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });
    }

    // Overload for backward compatibility (assumes isNew=true)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(TValue value) => Add(value, isNew: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(long time, double value, bool isNew = true) => Add(new TValue(time, value), isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(DateTime time, double value, bool isNew = true) => Add(new TValue(time, value), isNew);

    public void Add(IEnumerable<double> values)
    {
        long t = DateTime.UtcNow.Ticks;
        foreach (var v in values)
        {
            Add(new TValue(t, v), isNew: true);
            t += TimeSpan.TicksPerMinute;
        }
    }

    // IEnumerable implementation with struct enumerator for zero-allocation iteration
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TSeriesEnumerator GetEnumerator() => new(_t, _v);

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}