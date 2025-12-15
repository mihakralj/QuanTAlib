using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// A high-performance OHLCV time series implementation using Structure of Arrays (SoA) layout.
/// Stores Time, Open, High, Low, Close, Volume in separate contiguous arrays for SIMD efficiency.
/// Exposes TSeries views for each component that share the underlying Time array.
/// </summary>
public class TBarSeries : IReadOnlyList<TBar>
{
    protected readonly List<long> _t;
    protected readonly List<double> _o;
    protected readonly List<double> _h;
    protected readonly List<double> _l;
    protected readonly List<double> _c;
    protected readonly List<double> _v;

    public string Name { get; set; } = "Bar";
    public event Action<TBar>? Pub;

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

        Pub?.Invoke(bar);
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
            throw new ArgumentException("All arrays must have the same length");
        }

        for (int i = 0; i < tArr.Length; i++)
        {
            Add(tArr[i], oArr[i], hArr[i], lArr[i], cArr[i], vArr[i]);
        }
    }

    public IEnumerator<TBar> GetEnumerator()
    {
        for (int i = 0; i < _c.Count; i++)
        {
            yield return new TBar(_t[i], _o[i], _h[i], _l[i], _c[i], _v[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
