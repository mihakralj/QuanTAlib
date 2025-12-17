using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VIDYA: Variable Index Dynamic Average
/// </summary>
/// <remarks>
/// VIDYA is an adaptive moving average developed by Tushar Chande.
/// It adjusts the smoothing constant of an Exponential Moving Average (EMA) based on a volatility index.
/// The volatility index used is the Chande Momentum Oscillator (CMO).
///
/// Formula:
/// alpha = 2 / (period + 1)
/// CMO = (Sum(Up) - Sum(Down)) / (Sum(Up) + Sum(Down))
/// VI = Abs(CMO)
/// DynamicAlpha = alpha * VI
/// VIDYA = DynamicAlpha * Price + (1 - DynamicAlpha) * VIDYA_prev
///
/// Key characteristics:
/// - Adapts to market volatility
/// - Flattens in ranging markets (low volatility)
/// - Reacts quickly in trending markets (high volatility)
/// </remarks>
[SkipLocalsInit]
public sealed class Vidya : AbstractBase
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly RingBuffer _ups;
    private readonly RingBuffer _downs;

    private record struct State(
        double PrevClose, double LastVidya,
        double CurrentClose, double CurrentVidya,
        bool IsInitialized, int BarCount
    );
    private State _state;
    private State _p_state;

    public Vidya(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _alpha = 2.0 / (period + 1);
        _ups = new RingBuffer(period);
        _downs = new RingBuffer(period);
        Name = $"Vidya({period})";
        WarmupPeriod = period;
    }

    public Vidya(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    public override bool IsHot => _state.BarCount >= _period;

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

        if (isNew) _state.BarCount++;

        if (_state.IsInitialized)
        {
            _state.PrevClose = _state.CurrentClose;
            _state.LastVidya = _state.CurrentVidya;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            if (!_state.IsInitialized) return input;
            price = _state.CurrentClose;
        }

        if (_state.BarCount <= 1)
        {
            _state.PrevClose = price;
            _state.LastVidya = price;
            _state.CurrentClose = price;
            _state.CurrentVidya = price;
            _state.IsInitialized = true;
            _ups.Add(0, isNew);
            _downs.Add(0, isNew);
            Last = new TValue(input.Time, _state.CurrentVidya);
            PubEvent(Last);
            return Last;
        }

        double change = price - _state.PrevClose;
        double up = change > 0 ? change : 0;
        double down = change < 0 ? -change : 0;

        _ups.Add(up, isNew);
        _downs.Add(down, isNew);

        double sumUp = _ups.Sum;
        double sumDown = _downs.Sum;
        double sum = sumUp + sumDown;

        double vi = 0;
        if (sum > double.Epsilon)
        {
            vi = Math.Abs(sumUp - sumDown) / sum;
        }

        double dynamicAlpha = _alpha * vi;
        _state.CurrentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * _state.LastVidya;
        _state.CurrentClose = price;

        Last = new TValue(input.Time, _state.CurrentVidya);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        if (source.Length == 0) return;

        // Reset state
        Reset();

        // Process all data to build up state
        // For recursive indicators like VIDYA, we generally need to process from the start
        // or at least a significant warmup period.
        // Given we don't know the "correct" previous VIDYA without processing, 
        // we process the whole provided history.

        double prevClose = source[0];
        double lastVidya = source[0];

        // Initialize state
        _state.PrevClose = prevClose;
        _state.LastVidya = lastVidya;
        _state.CurrentClose = prevClose;
        _state.CurrentVidya = lastVidya;
        _state.IsInitialized = true;
        _state.BarCount = 1;
        _ups.Add(0);
        _downs.Add(0);

        for (int i = 1; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price)) price = prevClose;

            double change = price - prevClose;
            double up = change > 0 ? change : 0;
            double down = change < 0 ? -change : 0;

            _ups.Add(up);
            _downs.Add(down);
            _state.BarCount++;

            double sumUp = _ups.Sum;
            double sumDown = _downs.Sum;
            double sum = sumUp + sumDown;

            double vi = 0;
            if (sum > double.Epsilon)
            {
                vi = Math.Abs(sumUp - sumDown) / sum;
            }

            double dynamicAlpha = _alpha * vi;
            double currentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * lastVidya;

            _state.CurrentVidya = currentVidya;
            _state.CurrentClose = price;

            prevClose = price;
            lastVidya = currentVidya;
        }

        _state.PrevClose = prevClose;
        _state.LastVidya = lastVidya;

        // Set Last
        // Note: Time is not available in Span, so we use MinValue. 
        // It will be updated on next Update.
        Last = new TValue(DateTime.MinValue, _state.CurrentVidya);
        _p_state = _state;
    }

    public override void Reset()
    {
        _ups.Clear();
        _downs.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var vidya = new Vidya(period);
        return vidya.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0) return;

        double alpha = 2.0 / (period + 1);

        // Use arrays for buffers to avoid heap allocations if possible, 
        // but period is dynamic.
        // We can use ArrayPool or just new double[period] if period is small.
        // For simplicity and safety with large periods, let's use ArrayPool.

        double[] ups = System.Buffers.ArrayPool<double>.Shared.Rent(period);
        double[] downs = System.Buffers.ArrayPool<double>.Shared.Rent(period);
        Array.Clear(ups, 0, period);
        Array.Clear(downs, 0, period);

        try
        {
            int head = 0;
            double sumUp = 0;
            double sumDown = 0;

            double prevClose = source[0];
            double lastVidya = source[0];

            output[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                double price = source[i];
                if (!double.IsFinite(price))
                {
                    price = prevClose;
                }

                double change = price - prevClose;
                double up = change > 0 ? change : 0;
                double down = change < 0 ? -change : 0;

                sumUp -= ups[head];
                sumDown -= downs[head];

                ups[head] = up;
                downs[head] = down;

                sumUp += up;
                sumDown += down;

                head = (head + 1);
                if (head >= period) head = 0;

                double sum = sumUp + sumDown;
                double vi = 0;
                if (sum > double.Epsilon)
                {
                    vi = Math.Abs(sumUp - sumDown) / sum;
                }

                double dynamicAlpha = alpha * vi;
                double currentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * lastVidya;

                output[i] = currentVidya;

                prevClose = price;
                lastVidya = currentVidya;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(ups);
            System.Buffers.ArrayPool<double>.Shared.Return(downs);
        }
    }
}
