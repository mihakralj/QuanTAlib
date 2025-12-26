using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
public sealed class Mgdi : AbstractBase
{
    private readonly int _period;
    private readonly double _k;

    private record struct State(double LastMgdi, double LastValidValue, int Count, bool HasValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _state.Count >= _period;

    public Mgdi(int period = 14, double k = 0.6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (double.IsNaN(k) || double.IsInfinity(k) || k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be a finite value greater than 0");
        _period = period;
        _k = k;
        Name = $"Mgdi({period},{k})";
        WarmupPeriod = period;
        Init();
    }

    public Mgdi(ITValuePublisher source, int period = 14, double k = 0.6) : this(period, k)
    {
        source.Pub += (item) => Update(item);
    }

    private void Init()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _state.Count++;
        }
        else
        {
            _state = _p_state;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            if (_state.HasValidValue)
            {
                price = _state.LastValidValue;
            }
            else
            {
                Last = new TValue(input.Time, double.NaN);
                PubEvent(Last);
                return Last;
            }
        }
        else
        {
            _state.LastValidValue = price;
            _state.HasValidValue = true;
        }

        if (!_p_state.HasValidValue)
        {
            _state.LastMgdi = price;
        }
        else
        {
            double prev = _state.LastMgdi;
            if (Math.Abs(prev) > double.Epsilon)
            {
                double ratio = price / prev;
                ratio = Math.Clamp(ratio, 0.3, 3.0);
                double ratio4 = ratio * ratio;
                ratio4 *= ratio4;

                double denominator = _k * _period * ratio4;
                _state.LastMgdi = (Math.Abs(denominator) < 1e-9) ? price : prev + (price - prev) / denominator;
            }
            else
            {
                _state.LastMgdi = price;
            }
        }

        Last = new TValue(input.Time, _state.LastMgdi);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

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
        // Replay the whole series to restore state correctly as it is recursive
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period = 14, double k = 0.6)
    {
        var mgdi = new Mgdi(period, k);
        return mgdi.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 14, double k = 0.6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (double.IsNaN(k) || double.IsInfinity(k) || k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be a finite value greater than 0");

        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0) return;

        double lastMgdi = 0;
        double lastValid = 0;
        bool initialized = false;

        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price))
            {
                if (!initialized)
                {
                    output[i] = double.NaN;
                    continue;
                }
                price = lastValid;
            }
            else
            {
                lastValid = price;
                if (!initialized)
                {
                    initialized = true;
                    lastMgdi = price;
                    output[i] = lastMgdi;
                    continue;
                }
            }

            if (Math.Abs(lastMgdi) > double.Epsilon)
            {
                double ratio = price / lastMgdi;
                ratio = Math.Clamp(ratio, 0.3, 3.0);
                double ratio4 = ratio * ratio;
                ratio4 *= ratio4;

                double denominator = k * period * ratio4;
                lastMgdi = (Math.Abs(denominator) < 1e-9) ? price : lastMgdi + (price - lastMgdi) / denominator;
            }
            else
            {
                lastMgdi = price;
            }

            output[i] = lastMgdi;
        }
    }

    public override void Reset()
    {
        Init();
    }
}
