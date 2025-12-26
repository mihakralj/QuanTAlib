using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KAMA: Kaufman's Adaptive Moving Average
/// </summary>
/// <remarks>
/// KAMA adapts to market volatility by adjusting its smoothing factor based on an Efficiency Ratio (ER).
/// ER is calculated as the ratio of the absolute price change over a period to the sum of absolute price changes (volatility).
///
/// Formula:
/// ER = Change / Volatility
/// Change = Abs(Price - Price[period])
/// Volatility = Sum(Abs(Price[i] - Price[i-1]), period)
/// SC = (ER * (fast_alpha - slow_alpha) + slow_alpha)^2
/// KAMA = KAMA[prev] + SC * (Price - KAMA[prev])
/// </remarks>
[SkipLocalsInit]
public sealed class Kama : AbstractBase
{
    private readonly int _period;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly RingBuffer _buffer;

    private record struct State(double Kama, double VolatilitySum, double NextDiffOut, double LastValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates KAMA with specified parameters.
    /// </summary>
    /// <param name="period">Lookback period for Efficiency Ratio (default 10).</param>
    /// <param name="fastPeriod">Fast EMA period for SC calculation (default 2).</param>
    /// <param name="slowPeriod">Slow EMA period for SC calculation (default 30).</param>
    public Kama(int period = 10, int fastPeriod = 2, int slowPeriod = 30)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (fastPeriod <= 0)
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        if (slowPeriod <= 0)
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));

        _period = period;
        // Buffer needs to hold period + 1 values to calculate Change over 'period' bars
        // Change = Price[0] - Price[period]
        _buffer = new RingBuffer(period + 1);

        _fastAlpha = 2.0 / (fastPeriod + 1);
        _slowAlpha = 2.0 / (slowPeriod + 1);

        Name = $"Kama({period}, {fastPeriod}, {slowPeriod})";
        WarmupPeriod = period + 1;

        _state.Kama = double.NaN;
        _state.LastValidValue = double.NaN;
        _p_state.Kama = double.NaN;
        _p_state.LastValidValue = double.NaN;
    }

    public Kama(ITValuePublisher source, int period = 10, int fastPeriod = 2, int slowPeriod = 30)
        : this(period, fastPeriod, slowPeriod)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

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

        double val = GetValidValue(input.Value);
        if (double.IsNaN(val))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last);
            return Last;
        }

        if (isNew)
        {
            bool wasFull = _buffer.IsFull;
            _buffer.Add(val);

            if (wasFull)
            {
                double diff_out = _p_state.NextDiffOut;
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _state.VolatilitySum += diff_in - diff_out;

                // Calculate NextDiffOut for the next step
                // NextDiffOut = abs(buffer[0] - buffer[1])
                _state.NextDiffOut = Math.Abs(_buffer[0] - _buffer[1]);
            }
            else if (_buffer.Count >= 2)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _state.VolatilitySum += diff_in;

                if (_buffer.IsFull)
                {
                    // Buffer just became full.
                    // NextDiffOut = abs(buffer[0] - buffer[1])
                    _state.NextDiffOut = Math.Abs(_buffer[0] - _buffer[1]);
                }
            }
        }
        else
        {
            _buffer.UpdateNewest(val);

            if (_buffer.IsFull)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                // Use NextDiffOut from _p_state (which is the correct DiffOut for this transition)
                _state.VolatilitySum = _p_state.VolatilitySum + diff_in - _p_state.NextDiffOut;
            }
            else if (_buffer.Count >= 2)
            {
                double diff_in = Math.Abs(_buffer[^1] - _buffer[^2]);
                _state.VolatilitySum = _p_state.VolatilitySum + diff_in;
            }
        }

        // Calculate KAMA
        if (double.IsNaN(_state.Kama))
        {
            _state.Kama = val;
        }
        else
        {
            double change = Math.Abs(_buffer[^1] - _buffer[0]);
            double volatility = _state.VolatilitySum;

            // Avoid division by zero
            double er = (volatility > 1e-10) ? change / volatility : 0.0;
            // Cap ER at 1.0 just in case floating point errors push it slightly over
            if (er > 1.0) er = 1.0;

            // double sc = er * (_fastAlpha - _slowAlpha) + _slowAlpha;  // skipcq: S125
            double sc = Math.FusedMultiplyAdd(er, _fastAlpha - _slowAlpha, _slowAlpha);
            sc *= sc;

            double prevKama = _p_state.Kama;
            if (double.IsNaN(prevKama))
            {
                prevKama = _state.Kama;
            }

            // _state.Kama = prevKama + sc * (val - prevKama); // skipcq: S125
            _state.Kama = Math.FusedMultiplyAdd(sc, val - prevKama, prevKama);
        }

        Last = new TValue(input.Time, _state.Kama);
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

        source.Times.CopyTo(tSpan);

        // Use static Calculate for performance
        // fastPeriod = 2/fastAlpha - 1.
        int fastPeriod = (int)Math.Round(2.0 / _fastAlpha - 1);
        int slowPeriod = (int)Math.Round(2.0 / _slowAlpha - 1);

        Calculate(source.Values, vSpan, _period, fastPeriod, slowPeriod);

        // Restore state by replaying the entire series
        // This is expensive but necessary to sync the object state correctly
        // because KAMA is recursive (IIR) and depends on the full history.
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], _state.Kama);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period, int fastPeriod = 2, int slowPeriod = 30)
    {
        var kama = new Kama(period, fastPeriod, slowPeriod);
        return kama.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int fastPeriod = 2, int slowPeriod = 30)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (fastPeriod <= 0) throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        if (slowPeriod <= 0) throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        if (fastPeriod >= slowPeriod) throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        if (source.Length != output.Length) throw new ArgumentException("Source and output must have the same length");

        double fastAlpha = 2.0 / (fastPeriod + 1);
        double slowAlpha = 2.0 / (slowPeriod + 1);

        // We need a buffer for price history to calculate ER
        // Size period + 1
        int bufSize = period + 1;
        Span<double> buffer = bufSize <= 256 ? stackalloc double[bufSize] : new double[bufSize];
        int bufferIdx = 0;
        int count = 0;

        double volatilitySum = 0;
        double kama = 0;
        bool kamaInitialized = false;
        double lastValid = double.NaN;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            // Add to buffer
            double removed = buffer[bufferIdx];
            buffer[bufferIdx] = val;

            // Update volatility
            if (count >= 1)
            {
                // diff_in = abs(val - prev)
                // prev is at bufferIdx-1 (circular)
                int prevIdx = (bufferIdx - 1 + bufSize) % bufSize;
                double diff_in = Math.Abs(val - buffer[prevIdx]);

                volatilitySum += diff_in;

                if (count == bufSize)
                {
                    // diff_out = abs(removed - new_oldest)
                    // new_oldest is at (bufferIdx + 1) % bufSize
                    int oldestIdx = (bufferIdx + 1) % bufSize;
                    double diff_out = Math.Abs(removed - buffer[oldestIdx]);
                    volatilitySum -= diff_out;
                }
            }

            bufferIdx = (bufferIdx + 1) % bufSize;
            if (count < bufSize) count++;

            if (!kamaInitialized)
            {
                kama = val;
                kamaInitialized = true;
                output[i] = kama;
            }
            else
            {
                // Calculate ER
                // Change = abs(current - oldest)
                // current = val
                // oldest:
                // if full, oldest is at bufferIdx (which is the next write pos, so it holds the oldest)
                // Wait, bufferIdx points to where we WILL write next.
                // So buffer[bufferIdx] is the oldest value (the one that will be overwritten next).
                // So Change = abs(val - buffer[bufferIdx])

                double change = 0;
                change = (count == bufSize) ? Math.Abs(val - buffer[bufferIdx]) : Math.Abs(val - buffer[0]);

                double er = (volatilitySum > 1e-10) ? change / volatilitySum : 0.0;
                if (er > 1.0) er = 1.0;

                // double sc = er * (fastAlpha - slowAlpha) + slowAlpha; // skipcq: S125
                double sc = Math.FusedMultiplyAdd(er, fastAlpha - slowAlpha, slowAlpha);
                sc *= sc;

                // kama += sc * (val - kama); // skipcq: S125
                kama = Math.FusedMultiplyAdd(sc, val - kama, kama);
                output[i] = kama;
            }
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _state.Kama = double.NaN;
        _state.LastValidValue = double.NaN;
        _p_state = _state;
        Last = default;
    }
}
