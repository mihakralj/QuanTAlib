using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TSF: Time Series Forecast
/// </summary>
/// <remarks>
/// Projects the linear regression line one step forward, forecasting the
/// next bar's value based on the least-squares trend over the lookback period.
/// Kahan compensated summation prevents floating-point drift without periodic resync.
///
/// Calculation: <c>TSF = slope × period + intercept</c> (standard convention)
/// or equivalently <c>TSF = b − m</c> (reversed-x convention where b = current bar value).
///
/// Uses O(1) incremental running sums (SumY, SumXY) identical to LSMA.
/// Relationship: TSF = LSMA(offset=0) + slope = LSMA(offset=1).
/// </remarks>
/// <seealso href="Tsf.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Tsf : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    private readonly double _sumX;
    private readonly double _denominator;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private int _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumY, double SumXY, double SumYComp, double SumXYComp, double LastVal, double LastValidValue);
    private State _s;
    private State _ps;

    private bool _isNew;

    public override bool IsHot => _buffer.IsFull;
    public bool IsNew => _isNew;

    /// <summary>
    /// Creates TSF with specified period.
    /// </summary>
    /// <param name="period">Lookback period for linear regression (must be &gt; 0)</param>
    public Tsf(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Tsf({period})";
        WarmupPeriod = period;
        _handler = Handle;

        // Precompute constants (reversed-x convention: x=0=newest, x=n-1=oldest)
        // sumX = 0 + 1 + ... + (n-1) = n(n-1)/2
        _sumX = 0.5 * period * (period - 1);

        // sumX2 = 0^2 + ... + (n-1)^2 = (n-1)n(2n-1)/6
        double sumX2 = (period - 1.0) * period * ((2.0 * period) - 1.0) / 6.0;

        // denominator = n * sumX2 - sumX^2
        _denominator = (period * sumX2) - (_sumX * _sumX);
        _s.LastValidValue = double.NaN;
    }

    public Tsf(ITValuePublisher source, int period = 14) : this(period)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.Pub += _handler;
    }

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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
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

            // For isNew=false, update the current bar without advancing.
            // SumXY remains constant (depends on previous window state).
            // SumY updates to reflect the change in the newest value.
            _s.SumY = _ps.SumY - _ps.LastVal + val;
            _s.SumXY = _ps.SumXY;

            _buffer.UpdateNewest(val);
            _s.LastVal = val;
        }

        double result;
        if (_buffer.Count <= 1)
        {
            result = _buffer.Newest;
        }
        else
        {
            double n = _buffer.Count;
            double sx = _sumX;
            double denom = _denominator;

            if (!_buffer.IsFull)
            {
                // Recalculate constants for smaller n during warmup
                sx = 0.5 * n * (n - 1);
                double sx2 = (n - 1.0) * n * ((2.0 * n) - 1.0) / 6.0;
                denom = (n * sx2) - (sx * sx);
            }

            if (Math.Abs(denom) < 1e-10)
            {
                result = _buffer.Newest;
            }
            else
            {
                // Reversed-x convention: m is negative for uptrend
                double m = Math.FusedMultiplyAdd(n, _s.SumXY, -sx * _s.SumY) / denom;
                double b = Math.FusedMultiplyAdd(-m, sx, _s.SumY) / n;

                // b = value at x=0 (current bar endpoint)
                // TSF = forecast one step ahead = b - m
                // (In reversed-x, stepping forward means x=-1, so y = b - m*(-1)... wait)
                // Actually: b - m * offset, where offset=1 projects one step ahead
                // TSF = b - m * 1 = b - m
                result = b - m;
            }
        }

        Last = new TValue(input.Time, result);
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
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        double initialLastValid = _s.LastValidValue;
        Batch(source.Values, vSpan, _period, initialLastValid);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying the last 'period' bars
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        Reset();

        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source.Values[i]))
                {
                    _s.LastValidValue = source.Values[i];
                    break;
                }
            }
        }
        else
        {
            _s.LastValidValue = initialLastValid;
        }

        double lastProcessedValue = _s.LastValidValue;
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
            lastProcessedValue = val;
        }

        _s.LastVal = lastProcessedValue;
        _ps = _s;

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

    public static TSeries Batch(TSeries source, int period = 14)
    {
        var tsf = new Tsf(period);
        return tsf.Update(source);
    }

    /// <summary>
    /// Calculates TSF in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14, double initialLastValid = double.NaN)
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

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sumY = 0;
        double sumXY = 0;
        double lastValid = initialLastValid;
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

                if (count <= 1)
                {
                    output[i] = val;
                }
                else
                {
                    double n = count;
                    double sx = 0.5 * n * (n - 1);
                    double sx2 = (n - 1.0) * n * ((2.0 * n) - 1.0) / 6.0;
                    double denom = (n * sx2) - (sx * sx);

                    if (Math.Abs(denom) < 1e-10)
                    {
                        output[i] = val;
                    }
                    else
                    {
                        double m = Math.FusedMultiplyAdd(n, sumXY, -sx * sumY) / denom;
                        double b = Math.FusedMultiplyAdd(-m, sx, sumY) / n;
                        // TSF = b - m (one step ahead forecast)
                        output[i] = b - m;
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

                double m = Math.FusedMultiplyAdd(period, sumXY, -fullSumX * sumY) / fullDenom;
                double b = Math.FusedMultiplyAdd(-m, fullSumX, sumY) / period;
                // TSF = b - m (one step ahead forecast)
                output[i] = b - m;
            }
        }
    }

    public static (TSeries Results, Tsf Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Tsf(period);
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
