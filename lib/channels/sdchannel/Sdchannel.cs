using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SDCHANNEL: Standard Deviation Channel
/// Linear regression centerline with bands at ±multiplier × standard deviation of residuals.
/// </summary>
/// <remarks>
/// The Standard Deviation Channel plots a linear regression line with parallel bands
/// positioned at a specified number of standard deviations of the residuals above and below.
///
/// Calculation:
/// 1. Compute linear regression line: y = mx + b using least squares
/// 2. Calculate residuals: residual_i = y_i - predicted_i
/// 3. Compute standard deviation of residuals: σ = √(Σ(residual²) / n)
/// 4. Upper = regression + multiplier × σ
/// 5. Lower = regression - multiplier × σ
///
/// Key characteristics:
/// - Middle line is the linear regression endpoint (LSMA)
/// - Bands measure dispersion around the regression line
/// - Wider bands indicate more noise/volatility around the trend
/// - Price touching bands suggests deviation from trend
///
/// Sources:
///     https://www.investopedia.com/terms/l/linearregressionindicator.asp
///     https://school.stockcharts.com/doku.php?id=technical_indicators:linear_regression_indicator
/// </remarks>
[SkipLocalsInit]
public sealed class Sdchannel : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;

    // Precomputed constants for linear regression
    private readonly double _sumX;      // sum of x indices: 0 + 1 + ... + (n-1)
    private readonly double _denominator; // n * sumX2 - sumX²

    // Ring buffer for values
    private readonly double[] _buffer;
    private double[]? _p_buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int Head,
        int Count,
        double LastValid,
        double Slope,
        double StdDev,
        bool IsHot);

    private State _state;
    private State _p_state;

    private readonly TValuePublishedHandler _valueHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _state.IsHot;

    /// <summary>
    /// The slope of the linear regression line
    /// </summary>
    public double Slope => _state.Slope;

    /// <summary>
    /// The standard deviation of residuals
    /// </summary>
    public double StdDev => _state.StdDev;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new Standard Deviation Channel indicator with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period for regression (default 20, must be > 1)</param>
    /// <param name="multiplier">Standard deviation multiplier for bands (default 2.0, must be > 0)</param>
    public Sdchannel(int period = 20, double multiplier = 2.0)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 1.");
        }

        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than 0.");
        }

        _period = period;
        _multiplier = multiplier;
        _buffer = new double[period];
        _p_buffer = new double[period];
        WarmupPeriod = period;
        Name = $"Sdchannel({period},{multiplier:F1})";
        _valueHandler = HandleValue;

        // Precompute constants
        // sumX = 0 + 1 + ... + (n-1) = n(n-1)/2
        _sumX = 0.5 * period * (period - 1);
        // sumX2 = 0² + 1² + ... + (n-1)² = (n-1)n(2n-1)/6
        double sumX2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        // denominator = n * sumX2 - sumX²
        _denominator = period * sumX2 - _sumX * _sumX;

        Reset();
    }

    public Sdchannel(TSeries source, int period = 20, double multiplier = 2.0) : this(period, multiplier)
    {
        Prime(source);
        source.Pub += _valueHandler;
    }

    private void HandleValue(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = new State(0, 0, double.NaN, 0, 0, false);
        _p_state = _state;
        Array.Fill(_buffer, 0.0);
        _p_buffer = (double[])_buffer.Clone();
        Last = default;
        Upper = default;
        Lower = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValid(double value, bool isNew)
    {
        if (double.IsFinite(value))
        {
            // Always update LastValid on finite input (including bar corrections)
            _state = _state with { LastValid = value };
            return value;
        }
        return double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_buffer, _p_buffer!, _period);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_buffer!, _buffer, _period);
        }

        double value = GetValid(input.Value, isNew);

        // Add to ring buffer
        int count = _state.Count;
        int head = _state.Head;

        if (count < _period)
        {
            count++;
        }

        _buffer[head] = value;
        int newHead = (head + 1) % _period;

        if (isNew)
        {
            _state = _state with { Head = newHead, Count = count };
        }

        // Calculate linear regression and std dev of residuals
        if (count <= 1)
        {
            Last = new TValue(input.Time, value);
            Upper = new TValue(input.Time, value);
            Lower = new TValue(input.Time, value);
            _state = _state with { Slope = 0, StdDev = 0 };
            PubEvent(Last, isNew);
            return Last;
        }

        // Build span of values in chronological order (oldest to newest)
        Span<double> values = stackalloc double[count];
        int readHead = (newHead - count + _period) % _period;
        for (int i = 0; i < count; i++)
        {
            values[i] = _buffer[(readHead + i) % _period];
        }

        // Calculate sums for linear regression
        double sumY = 0;
        double sumXY = 0;

        for (int i = 0; i < count; i++)
        {
            sumY += values[i];
            sumXY += i * values[i];
        }

        double n = count;
        double sx = _sumX;
        double denom = _denominator;

        // Adjust for partial window during warmup
        if (count < _period)
        {
            sx = 0.5 * n * (n - 1);
            double sx2 = (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
            denom = n * sx2 - sx * sx;
        }

        double slope, intercept, regression;

        if (Math.Abs(denom) < 1e-10)
        {
            slope = 0;
            intercept = sumY / n;
            regression = intercept;
        }
        else
        {
            slope = (n * sumXY - sx * sumY) / denom;
            intercept = (sumY - slope * sx) / n;
            // Regression value at current point (x = count - 1)
            regression = Math.FusedMultiplyAdd(slope, count - 1, intercept);
        }

        // Calculate standard deviation of residuals
        double sumResiduals2 = 0;
        for (int i = 0; i < count; i++)
        {
            double predicted = Math.FusedMultiplyAdd(slope, i, intercept);
            double residual = values[i] - predicted;
            sumResiduals2 = Math.FusedMultiplyAdd(residual, residual, sumResiduals2);
        }

        double stdDev = Math.Sqrt(sumResiduals2 / n);
        double band = _multiplier * stdDev;

        if (!_state.IsHot && count >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        _state = _state with { Slope = slope, StdDev = stdDev };

        Last = new TValue(input.Time, regression);
        Upper = new TValue(input.Time, regression + band);
        Lower = new TValue(input.Time, regression - band);

        PubEvent(Last, isNew);
        return Last;
    }

    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        var tSpan = CollectionsMarshal.AsSpan(tMiddle);
        var vMiddleSpan = CollectionsMarshal.AsSpan(vMiddle);
        var vUpperSpan = CollectionsMarshal.AsSpan(vUpper);
        var vLowerSpan = CollectionsMarshal.AsSpan(vLower);

        Batch(source.Values, vMiddleSpan, vUpperSpan, vLowerSpan, _period, _multiplier);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, vMiddleSpan[^1]);
        Upper = new TValue(lastTime, vUpperSpan[^1]);
        Lower = new TValue(lastTime, vLowerSpan[^1]);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public void Prime(TSeries source)
    {
        Reset();

        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double multiplier = 2.0)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 1.");
        }

        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than 0.");
        }

        if (middle.Length < source.Length || upper.Length < source.Length || lower.Length < source.Length)
        {
            throw new ArgumentException("Output spans must be at least as long as input", nameof(middle));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Precompute constants for full period
        double sumXFull = 0.5 * period * (period - 1);
        double sumX2Full = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        double denomFull = period * sumX2Full - sumXFull * sumXFull;

        for (int i = 0; i < len; i++)
        {
            int count = Math.Min(i + 1, period);
            int start = i - count + 1;

            if (count <= 1)
            {
                middle[i] = source[i];
                upper[i] = source[i];
                lower[i] = source[i];
                continue;
            }

            // Calculate sums for linear regression
            double sumY = 0;
            double sumXY = 0;

            for (int j = 0; j < count; j++)
            {
                double y = source[start + j];
                sumY += y;
                sumXY += j * y;
            }

            double n = count;
            double sx, denom;

            if (count < period)
            {
                sx = 0.5 * n * (n - 1);
                double sx2 = (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
                denom = n * sx2 - sx * sx;
            }
            else
            {
                sx = sumXFull;
                denom = denomFull;
            }

            double slope, intercept, regression;

            if (Math.Abs(denom) < 1e-10)
            {
                slope = 0;
                intercept = sumY / n;
                regression = intercept;
            }
            else
            {
                slope = (n * sumXY - sx * sumY) / denom;
                intercept = (sumY - slope * sx) / n;
                regression = Math.FusedMultiplyAdd(slope, count - 1, intercept);
            }

            // Calculate standard deviation of residuals
            double sumResiduals2 = 0;
            for (int j = 0; j < count; j++)
            {
                double predicted = Math.FusedMultiplyAdd(slope, j, intercept);
                double residual = source[start + j] - predicted;
                sumResiduals2 = Math.FusedMultiplyAdd(residual, residual, sumResiduals2);
            }

            double stdDev = Math.Sqrt(sumResiduals2 / n);
            double band = multiplier * stdDev;

            middle[i] = regression;
            upper[i] = regression + band;
            lower[i] = regression - band;
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TSeries source, int period = 20, double multiplier = 2.0)
    {
        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        Batch(source.Values,
              CollectionsMarshal.AsSpan(vMiddle),
              CollectionsMarshal.AsSpan(vUpper),
              CollectionsMarshal.AsSpan(vLower),
              period, multiplier);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMiddle));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tUpper));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tLower));

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Sdchannel Indicator) Calculate(TSeries source, int period = 20, double multiplier = 2.0)
    {
        var indicator = new Sdchannel(source, period, multiplier);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
