using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace QuanTAlib;

/// <summary>
/// MGDI: McGinley Dynamic Indicator
/// A moving average that adjusts for shifts in market speed, designed to track the market better than existing indicators.
/// It looks like a moving average line, yet it is a smoothing mechanism for prices that turns out to track far better than any moving average.
/// It minimizes price separation and price hugs to avoid whipsaws.
/// </summary>
/// <remarks>
/// Sources:
/// https://www.investopedia.com/terms/m/mcginley-dynamic.asp
/// https://dotnet.stockindicators.dev/indicators/Dynamic/
/// Formula: MGDI = MGDI[1] + (Price - MGDI[1]) / (k * N * (Price/MGDI[1])^4)
/// Default k = 0.6
/// </remarks>
[SkipLocalsInit]
public sealed class Mgdi : ITValuePublisher
{
    public string Name { get; }
    public bool IsHot { get; private set; }
    public event Action<TValue>? Pub;
    public TValue Last { get; private set; }

    private readonly int _period;
    private readonly double _k;
    
    private record struct State(double LastMgdi, double LastValidValue, int Count);
    private State _state;
    private State _p_state;

    public Mgdi(int period = 14, double k = 0.6)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (double.IsNaN(k) || double.IsInfinity(k) || k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be a finite value greater than 0");
        _period = period;
        _k = k;
        Name = $"Mgdi({period},{k})";
        Init();
    }

    public Mgdi(ITValuePublisher source, int period = 14, double k = 0.6) : this(period, k)
    {
        source.Pub += (item) => Update(item);
    }

    public void Init()
    {
        _state = default;
        _p_state = default;
        IsHot = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew) _p_state = _state;
        else _state = _p_state;

        if (isNew) _state.Count++;

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = _state.LastValidValue;
        }
        else
        {
            _state.LastValidValue = price;
        }

        if (_state.Count == 1)
        {
            _state.LastMgdi = price;
        }
        else
        {
            double prev = _state.LastMgdi;
            if (Math.Abs(prev) > double.Epsilon)
            {
                double ratio = price / prev;
                double ratio4 = ratio * ratio;
                ratio4 *= ratio4;
                
                double denominator = _k * _period * ratio4;
                _state.LastMgdi = prev + (price - prev) / denominator;
            }
            else
            {
                 _state.LastMgdi = price;
            }
        }

        IsHot = _state.Count >= _period;
        Last = new TValue(input.Time, _state.LastMgdi);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period, _k);
        source.Times.CopyTo(tSpan);

        // Restore state
        Init();
        // Replay last portion to restore state
        int startIndex = Math.Max(0, len - Math.Max(_period * 2, 100));
        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period = 14, double k = 0.6)
    {
        var mgdi = new Mgdi(period, k);
        return mgdi.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 14, double k = 0.6)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0) return;

        double lastMgdi = source[0];
        double lastValid = source[0];
        output[0] = lastMgdi;

        for (int i = 1; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price)) price = lastValid;
            else lastValid = price;

            if (Math.Abs(lastMgdi) > double.Epsilon)
            {
                double ratio = price / lastMgdi;
                double ratio4 = ratio * ratio;
                ratio4 *= ratio4;
                
                double denominator = k * period * ratio4;
                lastMgdi += (price - lastMgdi) / denominator;
            }
            else
            {
                lastMgdi = price;
            }
            
            output[i] = lastMgdi;
        }
    }

    public void Reset()
    {
        Init();
    }
}
