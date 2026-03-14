using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ILRS: Integral of Linear Regression Slope
/// </summary>
/// <remarks>
/// Computes the linear regression slope over a rolling window, then accumulates
/// it via discrete integration (running sum) to reconstruct a smoothed price-level
/// signal. The integration step introduces a natural momentum quality.
/// Kahan compensated summation prevents floating-point drift without periodic resync.
///
/// Algorithm: slope via O(1) incremental linreg, then ILRS += slope.
/// Initialized to first price value.
///
/// Reference: John Ehlers, "Rocket Science for Traders" (Wiley, 2001).
/// </remarks>
/// <seealso href="Ilrs.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Ilrs : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    private readonly double _sumX;
    private readonly double _denominator;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private int _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumY, double SumXY,
        double SumYComp, double SumXYComp,
        double Integral, double LastVal,
        double LastValidValue, bool Initialized);
    private State _s;
    private State _ps;

    private bool _isNew;

    public override bool IsHot => _buffer.IsFull;
    public bool IsNew => _isNew;

    /// <summary>
    /// Creates ILRS with specified period.
    /// </summary>
    /// <param name="period">Lookback window for slope calculation (must be &gt;= 2)</param>
    public Ilrs(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Ilrs({period})";
        WarmupPeriod = period;
        _handler = Handle;

        // Precompute constants (reversed-x convention: x=0=newest, x=n-1=oldest)
        _sumX = 0.5 * period * (period - 1);
        double sumX2 = (period - 1.0) * period * ((2.0 * period) - 1.0) / 6.0;
        _denominator = (period * sumX2) - (_sumX * _sumX);
        _s.LastValidValue = double.NaN;
    }

    public Ilrs(ITValuePublisher source, int period = 14) : this(period)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        return Update(input, isNew, publish: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue Update(TValue input, bool isNew, bool publish)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);
            _s.LastVal = val;
            _ps = _s;
        }
        else
        {
            _s.LastValidValue = _ps.LastValidValue;
            double val = GetValidValue(input.Value);

            // Bar correction: recalculate slope with updated newest value
            _s.SumY = _ps.SumY - _ps.LastVal + val;
            _s.SumXY = _ps.SumXY;

            _buffer.UpdateNewest(val);
            _s.LastVal = val;

            // Recompute slope and re-apply to previous integral
            _s.Integral = _ps.Integral - ComputeSlope(_ps) + ComputeSlope(_s);
        }

        double result;
        if (!_s.Initialized || _buffer.Count < 2)
        {
            result = _s.Initialized ? _s.Integral : input.Value;
        }
        else
        {
            result = _s.Integral;
        }

        Last = new TValue(input.Time, result);
        if (publish) { PubEvent(Last, isNew); }
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying entire series (integral is cumulative)
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true, publish: false);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _s.LastValidValue = input;
            return input;
        }
        return _s.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldest = _buffer.Oldest;
            double prevSumY = _s.SumY;

            // Kahan compensated update for SumXY: sumXY += (prevSumY - period * oldest)
            double deltaXY = Math.FusedMultiplyAdd(-_period, oldest, prevSumY);
            double yXY = deltaXY - _s.SumXYComp;
            double tXY = _s.SumXY + yXY;
            _s.SumXYComp = (tXY - _s.SumXY) - yXY;
            _s.SumXY = tXY;

            // Kahan compensated update for SumY: sumY += (val - oldest)
            double deltaY = val - oldest;
            double yY = deltaY - _s.SumYComp;
            double tY = _s.SumY + yY;
            _s.SumYComp = (tY - _s.SumY) - yY;
            _s.SumY = tY;

            _buffer.Add(val);
        }
        else
        {
            if (_buffer.Count > 0)
            {
                // Kahan compensated addition for SumXY: sumXY += sumY
                double yXY = _s.SumY - _s.SumXYComp;
                double tXY = _s.SumXY + yXY;
                _s.SumXYComp = (tXY - _s.SumXY) - yXY;
                _s.SumXY = tXY;
            }

            // Kahan compensated addition for SumY
            double yY = val - _s.SumYComp;
            double tY = _s.SumY + yY;
            _s.SumYComp = (tY - _s.SumY) - yY;
            _s.SumY = tY;

            _buffer.Add(val);
        }

        // Initialize integral on first value
        if (!_s.Initialized)
        {
            _s.Integral = val;
            _s.Initialized = true;
        }
        else if (_buffer.Count >= 2)
        {
            // Integrate: ILRS += slope
            _s.Integral += ComputeSlope(_s);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeSlope(State state)
    {
        int n = _buffer.Count;
        if (n < 2)
        {
            return 0;
        }

        double sx = _sumX;
        double denom = _denominator;

        if (!_buffer.IsFull)
        {
            double nd = n;
            sx = 0.5 * nd * (nd - 1);
            double sx2 = (nd - 1.0) * nd * ((2.0 * nd) - 1.0) / 6.0;
            denom = (nd * sx2) - (sx * sx);
        }

        if (Math.Abs(denom) < 1e-10)
        {
            return 0;
        }

        // Reversed-x accumulation inverts the sign; negate to match standard orientation
        return -Math.FusedMultiplyAdd(n, state.SumXY, -sx * state.SumY) / denom;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates ILRS from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 14)
    {
        var ilrs = new Ilrs(period);
        return ilrs.Update(source);
    }

    /// <summary>
    /// Calculates ILRS in-place, writing results to pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sumY = 0;
        double sumXY = 0;
        double lastValid = double.NaN;
        double integral = double.NaN;
        int bufferIndex = 0;
        int count = 0;

        // Precalculate constants for full period
        double fullSumX = 0.5 * period * (period - 1);
        double fullSumX2 = (period - 1.0) * period * ((2.0 * period) - 1.0) / 6.0;
        double fullDenom = (period * fullSumX2) - (fullSumX * fullSumX);

        for (int i = 0; i < len; i++)
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

            if (count < period)
            {
                // Warmup phase
                buffer[count] = val;
                count++;

                if (count > 1)
                {
                    sumXY += sumY;
                }
                sumY += val;

                if (!double.IsFinite(integral))
                {
                    integral = val;
                    output[i] = integral;
                }
                else if (count < 2)
                {
                    output[i] = integral;
                }
                else
                {
                    double n = count;
                    double sx = 0.5 * n * (n - 1);
                    double sx2 = (n - 1.0) * n * ((2.0 * n) - 1.0) / 6.0;
                    double denom = (n * sx2) - (sx * sx);

                    if (Math.Abs(denom) < 1e-10)
                    {
                        output[i] = integral;
                    }
                    else
                    {
                        double slope = -Math.FusedMultiplyAdd(n, sumXY, -sx * sumY) / denom;
                        integral += slope;
                        output[i] = integral;
                    }
                }

                if (count == period)
                {
                    bufferIndex = 0;
                }
            }
            else
            {
                // Full buffer phase — O(1) update
                double oldest = buffer[bufferIndex];
                double prevSumY = sumY;

                sumXY = Math.FusedMultiplyAdd(-period, oldest, sumXY + prevSumY);
                sumY = sumY - oldest + val;
                buffer[bufferIndex] = val;

                bufferIndex++;
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

                double slope = -Math.FusedMultiplyAdd(period, sumXY, -fullSumX * sumY) / fullDenom;
                integral += slope;
                output[i] = integral;
            }
        }
    }

    public static (TSeries Results, Ilrs Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Ilrs(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _s.LastValidValue = double.NaN;
        _ps = default;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0 && _source != null)
        {
            _source.Pub -= _handler;
            _source = null;
        }
        base.Dispose(disposing);
    }
}
