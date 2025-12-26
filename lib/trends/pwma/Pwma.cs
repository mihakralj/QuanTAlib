using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PWMA: Parabolic Weighted Moving Average
/// </summary>
/// <remarks>
/// PWMA applies parabolic weighting to data points, giving significantly more weight to recent values.
/// Uses triple running sums for O(1) complexity per update.
///
/// Weights: w(i) = i^2
///
/// Calculation:
/// PWMA = Sum(i^2 * P_i) / Sum(i^2)
///
/// O(1) update logic:
/// S1_new = S1_old - oldest + newest
/// S2_new = S2_old - S1_old + n * newest
/// S3_new = S3_old - 2*S2_old + S1_old + n^2 * newest
///
/// Where:
/// S1 is simple sum
/// S2 is linear weighted sum
/// S3 is parabolic weighted sum
/// </remarks>
[SkipLocalsInit]
public sealed class Pwma : AbstractBase
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;
    private readonly RingBuffer _p_buffer;

    private record struct State(double Sum, double WSum, double PSum, double LastInput, double LastValidValue, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    public Pwma(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _divisor = (double)period * ((double)period + 1.0) * (2.0 * (double)period + 1.0) / 6.0;
        _buffer = new RingBuffer(period);
        _p_buffer = new RingBuffer(period);
        Name = $"Pwma({period})";
        WarmupPeriod = period;
    }

    public Pwma(ITValuePublisher source, int period) : this(period)
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
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldSum = _state.Sum;
            double oldWSum = _state.WSum;
            double oldest = _buffer.Oldest;

            _state.Sum = _state.Sum - oldest + val;
            _state.WSum = Math.FusedMultiplyAdd(_period, val, _state.WSum - oldSum);
            _state.PSum = Math.FusedMultiplyAdd((double)_period * _period, val, _state.PSum - 2 * oldWSum + oldSum);
        }
        else
        {
            int count = _buffer.Count + 1;
            _state.Sum += val;
            _state.WSum = Math.FusedMultiplyAdd(count, val, _state.WSum);
            _state.PSum = Math.FusedMultiplyAdd((double)count * count, val, _state.PSum);
        }

        _buffer.Add(val);

        _state.TickCount++;
        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            double recalcSum = 0;
            double recalcWsum = 0;
            double recalcPsum = 0;
            int i = 1;
            foreach (double item in _buffer)
            {
                recalcSum += item;
                recalcWsum = Math.FusedMultiplyAdd(i, item, recalcWsum);
                recalcPsum = Math.FusedMultiplyAdd((double)i * i, item, recalcPsum);
                i++;
            }
            _state.Sum = recalcSum;
            _state.WSum = recalcWsum;
            _state.PSum = recalcPsum;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);
            _state.LastInput = val;
            _p_state = _state;
            _p_buffer.CopyFrom(_buffer);
        }
        else
        {
            _state = _p_state;
            _buffer.CopyFrom(_p_buffer);
            double val = GetValidValue(input.Value);

            // Recalculate for the updated last value
            // We can't easily use the O(1) update formula here because we are replacing the newest value,
            // not shifting the window.
            // But we can adjust the sums directly.
            // S1' = S1 - last + new
            // S2' = S2 - n*last + n*new
            // S3' = S3 - n^2*last + n^2*new

            int n = _buffer.IsFull ? _period : _buffer.Count;
            double diff = val - _state.LastInput;

            _state.Sum += diff;
            _state.WSum = Math.FusedMultiplyAdd(n, diff, _state.WSum);
            _state.PSum = Math.FusedMultiplyAdd((double)n * n, diff, _state.PSum);

            _buffer.UpdateNewest(val);
        }

        double count = _buffer.Count;
        double currentDivisor = _buffer.IsFull ? _divisor : count * (count + 1.0) * (2.0 * count + 1.0) / 6.0;
        Last = new TValue(input.Time, _state.PSum / currentDivisor);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        if (startIndex > 0)
        {
            _state.LastValidValue = 0;
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source.Values[i]))
                {
                    _state.LastValidValue = source.Values[i];
                    break;
                }
            }
        }
        else
        {
            _state.LastValidValue = 0;
        }

        _buffer.Clear();
        _state.Sum = 0;
        _state.WSum = 0;
        _state.PSum = 0;
        _state.TickCount = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
            _state.LastInput = val;
        }

        _p_state = _state;
        _p_buffer.CopyFrom(_buffer);

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

    public static TSeries Batch(TSeries source, int period)
    {
        var pwma = new Pwma(period);
        return pwma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        CalculateScalarCore(source, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        double divisor = (double)period * ((double)period + 1.0) * (2.0 * (double)period + 1.0) / 6.0;
        double sum = 0;
        double wsum = 0;
        double psum = 0;
        double lastValid = 0;

        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
        int i = 0;

        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            sum += val;
            wsum = Math.FusedMultiplyAdd(i + 1, val, wsum);
            psum = Math.FusedMultiplyAdd((double)(i + 1) * (i + 1), val, psum);
            buffer[i] = val;

            double currentDivisor = ((double)i + 1.0) * ((double)i + 2.0) * (2.0 * ((double)i + 1.0) + 1.0) / 6.0;
            output[i] = psum / currentDivisor;
        }

        int tickCount = period;
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double oldSum = sum;
            double oldWSum = wsum;
            double oldest = buffer[bufferIdx];

            sum = sum - oldest + val;
            wsum = Math.FusedMultiplyAdd(period, val, wsum - oldSum);
            psum = Math.FusedMultiplyAdd((double)period * period, val, psum - 2 * oldWSum + oldSum);

            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
                bufferIdx = 0;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                double recalcWsum = 0;
                double recalcPsum = 0;

                for (int k = 0; k < period; k++)
                {
                    int idx = bufferIdx + k;
                    if (idx >= period) idx -= period;

                    double v = buffer[idx];
                    recalcSum += v;
                    recalcWsum = Math.FusedMultiplyAdd(k + 1, v, recalcWsum);
                    recalcPsum = Math.FusedMultiplyAdd((double)(k + 1) * (k + 1), v, recalcPsum);
                }
                sum = recalcSum;
                wsum = recalcWsum;
                psum = recalcPsum;
            }

            output[i] = psum / divisor;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _p_buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
