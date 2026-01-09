using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Performance-focused event args for TBar updates.
/// Implemented as struct to avoid heap allocations in high-frequency event dispatch.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TBarEventArgs : IEquatable<TBarEventArgs>
{
    public TBar Value { get; init; }
    public bool IsNew { get; init; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TBarEventArgs other) =>
        Value.Equals(other.Value) && IsNew == other.IsNew;

    public override bool Equals(object? obj) =>
        obj is TBarEventArgs other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Value, IsNew);

    public static bool operator ==(TBarEventArgs left, TBarEventArgs right) =>
        left.Equals(right);

    public static bool operator !=(TBarEventArgs left, TBarEventArgs right) =>
        !left.Equals(right);
}

/// <summary>
/// High-performance enumerator for TBarSeries.
/// </summary>
public struct TBarSeriesEnumerator : IEnumerator<TBar>, IEquatable<TBarSeriesEnumerator>
{
    private readonly List<long> _t;
    private readonly List<double> _o;
    private readonly List<double> _h;
    private readonly List<double> _l;
    private readonly List<double> _c;
    private readonly List<double> _v;
    private readonly int _count;
    private int _index;
    private TBar _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TBarSeriesEnumerator(List<long> t, List<double> o, List<double> h, List<double> l, List<double> c, List<double> v)
    {
        _t = t;
        _o = o;
        _h = h;
        _l = l;
        _c = c;
        _v = v;
        _count = c.Count;
        _index = -1;
        _current = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_index + 1 >= _count)
            return false;

        _index++;
        _current = new TBar(_t[_index], _o[_index], _h[_index], _l[_index], _c[_index], _v[_index]);
        return true;
    }

    public readonly TBar Current => _current;
    readonly object IEnumerator.Current => Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _index = -1;
        _current = default;
    }

    public readonly void Dispose() { }

    public readonly bool Equals(TBarSeriesEnumerator other) =>
        ReferenceEquals(_t, other._t) &&
        ReferenceEquals(_c, other._c) &&
        _count == other._count &&
        _index == other._index;

    public override readonly bool Equals(object? obj) =>
        obj is TBarSeriesEnumerator other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_t), RuntimeHelpers.GetHashCode(_c), _count, _index);

    public static bool operator ==(TBarSeriesEnumerator left, TBarSeriesEnumerator right) => left.Equals(right);
    public static bool operator !=(TBarSeriesEnumerator left, TBarSeriesEnumerator right) => !left.Equals(right);
}

// Performance-focused event args struct; not derived from EventArgs by design.
// We intentionally deviate from the standard EventArgs pattern here for perf.
#pragma warning disable MA0046 // The second parameter must be of type 'System.EventArgs' or a derived type
public delegate void TBarPublishedHandler(object? sender, in TBarEventArgs args);

public class TBarSeries : IReadOnlyList<TBar>
{
    private readonly List<long> _t;
    private readonly List<double> _o;
    private readonly List<double> _h;
    private readonly List<double> _l;
    private readonly List<double> _c;
    private readonly List<double> _v;

    public string Name { get; set; } = "Bar";
    public event TBarPublishedHandler? Pub;
#pragma warning restore MA0046

    // Note: These views share underlying storage. Do not modify directly; use TBarSeries.Add() instead.
    public TSeries Open { get; }
    public TSeries High { get; }
    public TSeries Low { get; }
    public TSeries Close { get; }
    public TSeries Volume { get; }
    // Aliases for convenience
    public TSeries O => Open;
    public TSeries H => High;
    public TSeries L => Low;
    public TSeries C => Close;
    public TSeries V => Volume;

    public TBarSeries() : this(0)
    {
    }

    public TBarSeries(int capacity)
    {
        _t = new List<long>(capacity);
        _o = new List<double>(capacity);
        _h = new List<double>(capacity);
        _l = new List<double>(capacity);
        _c = new List<double>(capacity);
        _v = new List<double>(capacity);

        Open = new TSeries(_t, _o) { Name = "Open" };
        High = new TSeries(_t, _h) { Name = "High" };
        Low = new TSeries(_t, _l) { Name = "Low" };
        Close = new TSeries(_t, _c) { Name = "Close" };
        Volume = new TSeries(_t, _v) { Name = "Volume" };
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _c.Count;
    }

    public TBar this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_t[index], _o[index], _h[index], _l[index], _c[index], _v[index]);
    }

    public TBar Last
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _c.Count > 0 ? new(_t[^1], _o[^1], _h[^1], _l[^1], _c[^1], _v[^1]) : default;
    }

    public long LastTime { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _t.Count > 0 ? _t[^1] : 0; }
    public double LastOpen { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _o.Count > 0 ? _o[^1] : double.NaN; }
    public double LastHigh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _h.Count > 0 ? _h[^1] : double.NaN; }
    public double LastLow { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _l.Count > 0 ? _l[^1] : double.NaN; }
    public double LastClose { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _c.Count > 0 ? _c[^1] : double.NaN; }
    public double LastVolume { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _v.Count > 0 ? _v[^1] : double.NaN; }

    /// <summary>
    /// Direct access to the underlying Time array as a Span.
    /// </summary>
    public ReadOnlySpan<long> Times
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_t);
    }

    /// <summary>
    /// Direct access to the underlying Open array as a Span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> OpenValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_o);
    }

    /// <summary>
    /// Direct access to the underlying High array as a Span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> HighValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_h);
    }

    /// <summary>
    /// Direct access to the underlying Low array as a Span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> LowValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_l);
    }

    /// <summary>
    /// Direct access to the underlying Close array as a Span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> CloseValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_c);
    }

    /// <summary>
    /// Direct access to the underlying Volume array as a Span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> VolumeValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CollectionsMarshal.AsSpan(_v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TBar bar, bool isNew = true)
    {
        if (isNew || _c.Count == 0)
        {
            _t.Add(bar.Time);
            _o.Add(bar.Open);
            _h.Add(bar.High);
            _l.Add(bar.Low);
            _c.Add(bar.Close);
            _v.Add(bar.Volume);
        }
        else
        {
            int lastIdx = _c.Count - 1;
            _t[lastIdx] = bar.Time;
            _o[lastIdx] = bar.Open;
            _h[lastIdx] = bar.High;
            _l[lastIdx] = bar.Low;
            _c[lastIdx] = bar.Close;
            _v[lastIdx] = bar.Volume;
        }

        Pub?.Invoke(this, new TBarEventArgs { Value = bar, IsNew = isNew });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(long time, double open, double high, double low, double close, double volume, bool isNew = true) =>
        Add(new TBar(time, open, high, low, close, volume), isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(DateTime time, double open, double high, double low, double close, double volume, bool isNew = true) =>
        Add(new TBar(time.Ticks, open, high, low, close, volume), isNew);

    public void Add(IEnumerable<long> t, IEnumerable<double> o, IEnumerable<double> h, IEnumerable<double> l, IEnumerable<double> c, IEnumerable<double> v)
    {
        var tArr = t as long[] ?? t.ToArray();
        var oArr = o as double[] ?? o.ToArray();
        var hArr = h as double[] ?? h.ToArray();
        var lArr = l as double[] ?? l.ToArray();
        var cArr = c as double[] ?? c.ToArray();
        var vArr = v as double[] ?? v.ToArray();

        if (tArr.Length != oArr.Length || oArr.Length != hArr.Length ||
            hArr.Length != lArr.Length || lArr.Length != cArr.Length ||
            cArr.Length != vArr.Length)
        {
            throw new ArgumentException("All arrays must have the same length", nameof(t));
        }

        for (int i = 0; i < tArr.Length; i++)
        {
            Add(tArr[i], oArr[i], hArr[i], lArr[i], cArr[i], vArr[i]);
        }
    }

    // IEnumerable implementation with struct enumerator for zero-allocation iteration
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarSeriesEnumerator GetEnumerator() => new(_t, _o, _h, _l, _c, _v);

    IEnumerator<TBar> IEnumerable<TBar>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
