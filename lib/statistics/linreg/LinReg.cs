using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LinReg: Linear Regression Curve
/// </summary>
/// <remarks>
/// The Linear Regression Curve plots the end point of the linear regression line for each bar.
/// It fits a straight line y = mx + b to the data points using the least squares method.
/// Uses Kahan compensated summation for numerical stability of running sums,
/// eliminating the need for periodic resynchronization.
///
/// Calculation:
/// Uses linear regression y = mx + b where x=0 is the current bar and x increases into the past.
/// m = (n * sum_xy - sum_x * sum_y) / denominator
/// b = (sum_y - m * sum_x) / n
/// LinReg = b - m * offset
///
/// O(1) update:
/// sum_y_new = sum_y_old - oldest + newest
/// sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
///
/// Properties:
/// - Slope (m): The rate of change of the regression line.
/// - Intercept (b): The value of the regression line at x=0 (current bar).
/// - RSquared (r^2): The coefficient of determination (goodness of fit).
/// </remarks>
[SkipLocalsInit]
public sealed class LinReg : AbstractBase
{
    private readonly int _period;
    private readonly int _offset;
    private readonly RingBuffer _buffer;

    private readonly double _sum_x;
    private readonly double _denominator;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumY, double SumXY, double SumY2, double LastVal, double LastValidValue,
        double SumYComp, double SumXYComp, double SumY2Comp);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    private const double MinDenominator = 1e-10;

    /// <summary>
    /// The slope (m) of the linear regression line.
    /// </summary>
    public double Slope { get; private set; }

    /// <summary>
    /// The intercept (b) of the linear regression line at x=0.
    /// </summary>
    public double Intercept { get; private set; }

    /// <summary>
    /// The coefficient of determination (R-squared).
    /// </summary>
    public double RSquared { get; private set; }

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates LinReg with specified period and offset.
    /// </summary>
    /// <param name="period">Lookback period (must be > 0)</param>
    /// <param name="offset">
    /// Offset from current bar (default 0).
    /// Positive: project into future (offset=1 gives next bar's expected value)
    /// Negative: project into past (offset=-1 gives previous bar's fitted value)
    /// Zero: current bar (end point of regression line)
    /// </param>
    public LinReg(int period, int offset = 0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _offset = offset;
        _buffer = new RingBuffer(period);
        Name = $"LinReg({period})";
        WarmupPeriod = period;
        _handler = Handle;

        // Precalculate constants
        // sum_x = 0 + 1 + ... + (n-1) = n(n-1)/2
        _sum_x = 0.5 * period * (period - 1);

        // sum_x2 = 0^2 + ... + (n-1)^2 = (n-1)n(2n-1)/6
        double sum_x2 = (period - 1.0) * period * ((2.0 * period) - 1.0) / 6.0;

        // denominator = n * sum_x2 - sum_x^2
        _denominator = (period * sum_x2) - (_sum_x * _sum_x);
    }

    public LinReg(ITValuePublisher source, int period, int offset = 0) : this(period, offset)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

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
            double oldest = _buffer.Oldest;
            double prev_sum_y = _state.SumY;

            // O(1) update for sum_xy with Kahan compensation
            // sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
            {
                double delta = prev_sum_y - (_period * oldest);
                double y = delta - _state.SumXYComp;
                double t = _state.SumXY + y;
                _state.SumXYComp = (t - _state.SumXY) - y;
                _state.SumXY = t;
            }

            // O(1) update for sum_y with Kahan: subtract oldest, add val
            {
                double delta = val - oldest;
                double y = delta - _state.SumYComp;
                double t = _state.SumY + y;
                _state.SumYComp = (t - _state.SumY) - y;
                _state.SumY = t;
            }

            // O(1) update for sum_y2 with Kahan: subtract oldest², add val²
            {
                double delta = (val * val) - (oldest * oldest);
                double y = delta - _state.SumY2Comp;
                double t = _state.SumY2 + y;
                _state.SumY2Comp = (t - _state.SumY2) - y;
                _state.SumY2 = t;
            }

            _buffer.Add(val);
        }
        else
        {
            _buffer.Add(val);

            // Kahan add val to SumY
            {
                double y = val - _state.SumYComp;
                double t = _state.SumY + y;
                _state.SumYComp = (t - _state.SumY) - y;
                _state.SumY = t;
            }

            // Kahan add val² to SumY2
            {
                double y = (val * val) - _state.SumY2Comp;
                double t = _state.SumY2 + y;
                _state.SumY2Comp = (t - _state.SumY2) - y;
                _state.SumY2 = t;
            }

            // Recalculate sum_xy from scratch during warmup
            _state.SumXY = 0;
            _state.SumXYComp = 0;
            var span = _buffer.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                // x=0 is newest (index count-1), x=count-1 is oldest (index 0)
                int x = span.Length - 1 - i;
                _state.SumXY = Math.FusedMultiplyAdd(x, span[i], _state.SumXY);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);

            _p_state = _state;
            _state.LastVal = val;
        }
        else
        {
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);

            _state.SumY = _p_state.SumY - _p_state.LastVal + val;
            _state.SumYComp = _p_state.SumYComp;
            _state.SumY2 = Math.FusedMultiplyAdd(-_p_state.LastVal, _p_state.LastVal, _p_state.SumY2);
            _state.SumY2 = Math.FusedMultiplyAdd(val, val, _state.SumY2);
            _state.SumY2Comp = _p_state.SumY2Comp;
            _state.SumXY = _p_state.SumXY; // Unchanged: newest value at x=0 contributes 0 to sum_xy
            _state.SumXYComp = _p_state.SumXYComp;

            _buffer.UpdateNewest(val);
            _state.LastVal = val;
        }

        double result;
        if (_buffer.Count <= 1)
        {
            result = _buffer.Newest;
            Slope = 0;
            Intercept = result;
            RSquared = 0;
        }
        else
        {
            double n = _buffer.Count;
            double sx = _sum_x;
            double denom = _denominator;

            if (!_buffer.IsFull)
            {
                sx = 0.5 * n * (n - 1);
                double sx2 = (n - 1.0) * n * ((2.0 * n) - 1.0) / 6.0;
                denom = (n * sx2) - (sx * sx);
            }

            if (Math.Abs(denom) < MinDenominator)
            {
                result = _buffer.Newest;
                Slope = 0;
                Intercept = result;
                RSquared = 0;
            }
            else
            {
                double m = Math.FusedMultiplyAdd(n, _state.SumXY, -sx * _state.SumY) / denom;
                double b = Math.FusedMultiplyAdd(-m, sx, _state.SumY) / n;

                // Convert slope to time-forward direction:
                // Our x-axis: x=0 (now), x=n-1 (past) — increases backward in time
                // For rising prices: newest > oldest, so y decreases as x increases → m < 0
                // Time-forward slope = -m → positive for rising prices
                Slope = -m;

                Intercept = b;
                result = Math.FusedMultiplyAdd(-m, _offset, b);

                // Calculate R-Squared
                // R2 = (n * sum_xy - sum_x * sum_y)^2 / ( (n * sum_x2 - sum_x^2) * (n * sum_y2 - sum_y^2) )
                double numerator = Math.FusedMultiplyAdd(n, _state.SumXY, -sx * _state.SumY);
                double term2 = Math.FusedMultiplyAdd(n, _state.SumY2, -_state.SumY * _state.SumY);

                RSquared = Math.Abs(term2) < MinDenominator
                    ? 1.0 // All y are same
                    : numerator * numerator / (denom * term2);
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

        double initialLastValid = _state.LastValidValue;
        Batch(source.Values, vSpan, _period, _offset, initialLastValid);
        source.Times.CopyTo(tSpan);

        // Restore state
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        Reset();

        if (startIndex > 0)
        {
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
            _state.LastValidValue = initialLastValid;
        }

        double lastProcessedValue = _state.LastValidValue;
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
            lastProcessedValue = val;
        }

        _state.LastVal = lastProcessedValue;
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

    public static TSeries Batch(TSeries source, int period, int offset = 0)
    {
        var linreg = new LinReg(period, offset);
        return linreg.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int offset = 0, double initialLastValid = 0)
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

        // Stack allocate for typical periods (most < 100)
        // ArrayPool for large periods to avoid stack overflow
        const int StackAllocThreshold = 256;
        double[]? rentedBuffer = null;

#pragma warning disable S1121
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (rentedBuffer = ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);
#pragma warning restore S1121

        try
        {
            double sum_y = 0;
            double sum_xy = 0;
            double sumYComp = 0;        // Kahan compensation for sum_y
            double sumXYComp = 0;       // Kahan compensation for sum_xy
            double lastValid = initialLastValid;
            int bufferIndex = 0;
            int count = 0;

            double full_sum_x = 0.5 * period * (period - 1);
            double full_sum_x2 = (period - 1.0) * period * ((2.0 * period) - 1.0) / 6.0;
            double full_denom = (period * full_sum_x2) - (full_sum_x * full_sum_x);

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
                    buffer[count] = val;
                    sum_y += val;
                    count++;

                    sum_xy = 0;
                    for (int j = 0; j < count; j++)
                    {
                        sum_xy = Math.FusedMultiplyAdd(count - 1 - j, buffer[j], sum_xy);
                    }

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

                        if (Math.Abs(denom) < MinDenominator)
                        {
                            output[i] = val;
                        }
                        else
                        {
                            double m = Math.FusedMultiplyAdd(n, sum_xy, -sx * sum_y) / denom;
                            double b = Math.FusedMultiplyAdd(-m, sx, sum_y) / n;
                            output[i] = Math.FusedMultiplyAdd(-m, offset, b);
                        }
                    }

                    if (count == period)
                    {
                        bufferIndex = 0;
                        // Reset Kahan compensation at transition to sliding window
                        sumYComp = 0;
                        sumXYComp = 0;
                    }
                }
                else
                {
                    double oldest = buffer[bufferIndex];
                    double prev_sum_y = sum_y;

                    // Kahan compensated update for sum_xy
                    {
                        double delta = prev_sum_y - (period * oldest);
                        double y = delta - sumXYComp;
                        double t = sum_xy + y;
                        sumXYComp = (t - sum_xy) - y;
                        sum_xy = t;
                    }

                    // Kahan compensated update for sum_y
                    {
                        double delta = val - oldest;
                        double y = delta - sumYComp;
                        double t = sum_y + y;
                        sumYComp = (t - sum_y) - y;
                        sum_y = t;
                    }

                    buffer[bufferIndex] = val;

                    bufferIndex++;
                    if (bufferIndex >= period)
                    {
                        bufferIndex = 0;
                    }

                    double m = Math.FusedMultiplyAdd(period, sum_xy, -full_sum_x * sum_y) / full_denom;
                    double b = Math.FusedMultiplyAdd(-m, full_sum_x, sum_y) / period;
                    output[i] = Math.FusedMultiplyAdd(-m, offset, b);
                }
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<double>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static (TSeries Results, LinReg Indicator) Calculate(TSeries source, int period, int offset = 0)
    {
        var indicator = new LinReg(period, offset);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
        Slope = 0;
        Intercept = 0;
        RSquared = 0;
    }
}
