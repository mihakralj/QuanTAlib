using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PWMA: Parabolic Weighted Moving Average
/// </summary>
/// <remarks>
/// Quadratic weighting (w[i]=i²) emphasizing recent values via O(1) triple running sums.
/// Kahan compensated summation prevents floating-point drift without periodic resync.
///
/// Calculation: <c>PWMA = Σ(i²×P_i) / Σ(i²)</c> with efficient incremental updates.
/// </remarks>
/// <seealso href="Pwma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Pwma : AbstractBase
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double WSum, double PSum, double SumComp, double WSumComp, double PSumComp, double LastInput, double LastValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _buffer.IsFull;

    public Pwma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _divisor = (double)period * ((double)period + 1.0) * ((2.0 * (double)period) + 1.0) / 6.0;
        _buffer = new RingBuffer(period);
        Name = $"Pwma({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Pwma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable RCS1032 // Remove redundant parentheses
    private static double GetValidValue(double input, double lastValid)
    {
        return double.IsFinite(input) ? input : lastValid;
    }
#pragma warning restore RCS1032

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLastValidValue(double val)
    {
        if (double.IsFinite(val))
        {
            _state.LastValidValue = val;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldSum = _state.Sum;
            double oldWSum = _state.WSum;
            double oldest = _buffer.Oldest;

            // Kahan compensated update for Sum: sum += (val - oldest)
            double deltaS = val - oldest;
            double yS = deltaS - _state.SumComp;
            double tS = _state.Sum + yS;
            _state.SumComp = (tS - _state.Sum) - yS;
            _state.Sum = tS;

            // Kahan compensated update for WSum: wsum += (period * val - oldSum)
            double deltaW = Math.FusedMultiplyAdd(_period, val, -oldSum);
            double yW = deltaW - _state.WSumComp;
            double tW = _state.WSum + yW;
            _state.WSumComp = (tW - _state.WSum) - yW;
            _state.WSum = tW;

            // Kahan compensated update for PSum: psum += (period² * val - 2 * oldWSum + oldSum)
            double deltaP = Math.FusedMultiplyAdd((double)_period * _period, val, (-2 * oldWSum) + oldSum);
            double yP = deltaP - _state.PSumComp;
            double tP = _state.PSum + yP;
            _state.PSumComp = (tP - _state.PSum) - yP;
            _state.PSum = tP;
        }
        else
        {
            int count = _buffer.Count + 1;

            // Kahan compensated addition for Sum
            double yS = val - _state.SumComp;
            double tS = _state.Sum + yS;
            _state.SumComp = (tS - _state.Sum) - yS;
            _state.Sum = tS;

            // Kahan compensated addition for WSum
            double wVal = count * val;
            double yW = wVal - _state.WSumComp;
            double tW = _state.WSum + yW;
            _state.WSumComp = (tW - _state.WSum) - yW;
            _state.WSum = tW;

            // Kahan compensated addition for PSum
            double pVal = (double)count * count * val;
            double yP = pVal - _state.PSumComp;
            double tP = _state.PSum + yP;
            _state.PSumComp = (tP - _state.PSum) - yP;
            _state.PSum = tP;
        }

        _buffer.Add(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value, _state.LastValidValue);
            UpdateLastValidValue(val);
            UpdateState(val);
            _state.LastInput = val;

            // Save state AFTER the update for rollback support
            _p_state = _state;
        }
        else
        {
            // Defensive check: isNew must be true for the first update
            if (_buffer.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot call Update with isNew=false when buffer is empty. " +
                    "The first update must have isNew=true to initialize state.");
            }

            // Restore state (not buffer - we just adjust sums mathematically)
            _state = _p_state;
            double val = GetValidValue(input.Value, _state.LastValidValue);

            // Adjust sums: replace LastInput with new value
            // The weight n is the count at the newest position (period if full, else current count)
            int n = _buffer.IsFull ? _period : _buffer.Count;
            double diff = val - _state.LastInput;

            _state.Sum += diff;
            _state.WSum = Math.FusedMultiplyAdd(n, diff, _state.WSum);
            _state.PSum = Math.FusedMultiplyAdd((double)n * n, diff, _state.PSum);

            _buffer.UpdateNewest(val);
            UpdateLastValidValue(val);
        }

        double count = _buffer.Count;
        double currentDivisor = _buffer.IsFull ? _divisor : count * (count + 1.0) * ((2.0 * count) + 1.0) / 6.0;
        Last = new TValue(input.Time, _state.PSum / currentDivisor);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
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
        _state.SumComp = 0;
        _state.WSumComp = 0;
        _state.PSumComp = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i], _state.LastValidValue);
            UpdateLastValidValue(val);
            UpdateState(val);
            _state.LastInput = val;
        }

        _p_state = _state;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
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
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Pwma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Pwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        double divisor = (double)period * ((double)period + 1.0) * ((2.0 * (double)period) + 1.0) / 6.0;
        double sum = 0;
        double wsum = 0;
        double psum = 0;
        double sumComp = 0;
        double wsumComp = 0;
        double psumComp = 0;
        double lastValid = 0;

        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
        int i = 0;

        // Warmup phase with Kahan compensated additions
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            // Kahan compensated addition for sum
            double yS = val - sumComp;
            double tS = sum + yS;
            sumComp = (tS - sum) - yS;
            sum = tS;

            // Kahan compensated addition for wsum
            double wVal = (i + 1) * val;
            double yW = wVal - wsumComp;
            double tW = wsum + yW;
            wsumComp = (tW - wsum) - yW;
            wsum = tW;

            // Kahan compensated addition for psum
            double pVal = (double)(i + 1) * (i + 1) * val;
            double yP = pVal - psumComp;
            double tP = psum + yP;
            psumComp = (tP - psum) - yP;
            psum = tP;

            buffer[i] = val;

            double currentDivisor = ((double)i + 1.0) * ((double)i + 2.0) * ((2.0 * ((double)i + 1.0)) + 1.0) / 6.0;
            output[i] = psum / currentDivisor;
        }

        // Steady-state: sliding window with Kahan compensated triple sums
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            double oldSum = sum;
            double oldWSum = wsum;
            double oldest = buffer[bufferIdx];

            // Kahan compensated update for Sum: sum += (val - oldest)
            double deltaS = val - oldest;
            double yS = deltaS - sumComp;
            double tS = sum + yS;
            sumComp = (tS - sum) - yS;
            sum = tS;

            // Kahan compensated update for WSum: wsum += (period * val - oldSum)
            double deltaW = Math.FusedMultiplyAdd(period, val, -oldSum);
            double yW = deltaW - wsumComp;
            double tW = wsum + yW;
            wsumComp = (tW - wsum) - yW;
            wsum = tW;

            // Kahan compensated update for PSum: psum += (period² * val - 2 * oldWSum + oldSum)
            double deltaP = Math.FusedMultiplyAdd((double)period * period, val, (-2 * oldWSum) + oldSum);
            double yP = deltaP - psumComp;
            double tP = psum + yP;
            psumComp = (tP - psum) - yP;
            psum = tP;

            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
            {
                bufferIdx = 0;
            }

            output[i] = psum / divisor;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}