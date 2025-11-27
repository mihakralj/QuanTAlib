using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// A high-performance time series implementation using Structure of Arrays (SoA) layout.
/// Stores Time (long) and Value (double) in separate contiguous arrays for SIMD efficiency.
/// Supports "New Bar" vs "Update Last" streaming semantics.
/// </summary>
public class TSeries : IReadOnlyList<TValue>
{
    // Internal storage: SoA layout
    // We use List<T> for dynamic sizing but access internal arrays via CollectionsMarshal for speed
    protected readonly List<long> _t;
    protected readonly List<double> _v;

    public string Name { get; set; } = "Data";

    // Event optimization: Use Action<TValue> to avoid EventArgs allocation
    // Note: Events are generally discouraged in the hot path of this high-perf design, 
    // but kept for compatibility/chaining.
    public event Action<TValue>? Pub;

    public TSeries() 
    {
        _t = new List<long>();
        _v = new List<double>();
    }

    /// <summary>
    /// Constructor with capacity hint to avoid List growth overhead.
    /// </summary>
    public TSeries(int capacity) 
    {
        _t = new List<long>(capacity);
        _v = new List<double>(capacity);
    }

    /// <summary>
    /// Constructor for wrapping existing lists (e.g. from TBarSeries).
    /// </summary>
    public TSeries(List<long> time, List<double> values)
    {
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
        get => new(_t[index], _v[index]);
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
    public ReadOnlySpan<double> Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_v);
    }

    /// <summary>
    /// Direct access to the underlying Time array as a Span.
    /// </summary>
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
            // Update last bar
            int lastIdx = _v.Count - 1;
            _t[lastIdx] = value.Time;
            _v[lastIdx] = value.Value;
        }
        Pub?.Invoke(value);
    }

    // Overload for backward compatibility (assumes isNew=true)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(TValue value) => Add(value, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(long time, double value, bool isNew = true) => Add(new TValue(time, value), isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(DateTime time, double value, bool isNew = true) => Add(new TValue(time.Ticks, value), isNew);

    public void Add(IEnumerable<double> values)
    {
        long t = DateTime.UtcNow.Ticks;
        foreach (var v in values)
        {
            Add(new TValue(t, v), isNew: true);
            t += TimeSpan.TicksPerMinute; // Dummy time increment
        }
    }

    // IEnumerable implementation
    public IEnumerator<TValue> GetEnumerator()
    {
        for (int i = 0; i < _v.Count; i++)
        {
            yield return new TValue(_t[i], _v[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
