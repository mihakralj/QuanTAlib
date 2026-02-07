using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TTM_LRC: TTM Linear Regression Channel
/// John Carter's Linear Regression Channel with ±1σ and ±2σ standard deviation bands.
/// </summary>
/// <remarks>
/// The TTM LRC provides a clean, statistically-based price channel using linear regression
/// analysis. Unlike Bollinger Bands which measure volatility around a moving average, LRC
/// measures price deviation from the trend line, making it particularly useful for identifying
/// overbought/oversold conditions within a defined trend.
///
/// Calculation:
/// 1. Compute linear regression line: y = mx + b using least squares over N periods
/// 2. Calculate residuals: residual_i = y_i - predicted_i
/// 3. Compute standard deviation of residuals: σ = √(Σ(residual²) / N)
/// 4. Inner bands: ±1σ (68% of prices)
/// 5. Outer bands: ±2σ (95% of prices)
///
/// Key characteristics:
/// - Middle line is the linear regression endpoint (LSMA)
/// - Dual band pairs for statistical significance levels
/// - Slope indicates trend direction and strength
/// - R² indicates trend quality (higher = cleaner trend)
/// - Price at ±2σ suggests extreme deviation from trend
///
/// Sources:
///     John Carter's TTM Indicators
///     https://school.stockcharts.com/doku.php?id=technical_indicators:raff_regression_channel
/// </remarks>
[SkipLocalsInit]
public sealed class TtmLrc : ITValuePublisher
{
    private readonly int _period;

    // Precomputed constants for linear regression
    private readonly double _sumX;        // sum of x indices: 0 + 1 + ... + (n-1)
    private readonly double _denominator; // n * sumX² - sumX²

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
        double RSquared,
        bool IsHot);

    private State _state;
    private State _p_state;

    private readonly TValuePublishedHandler _valueHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }

    /// <summary>
    /// The linear regression line value (trend center)
    /// </summary>
    public TValue Midline { get; private set; }

    /// <summary>
    /// Upper band at +1 standard deviation
    /// </summary>
    public TValue Upper1 { get; private set; }

    /// <summary>
    /// Lower band at -1 standard deviation
    /// </summary>
    public TValue Lower1 { get; private set; }

    /// <summary>
    /// Upper band at +2 standard deviations
    /// </summary>
    public TValue Upper2 { get; private set; }

    /// <summary>
    /// Lower band at -2 standard deviations
    /// </summary>
    public TValue Lower2 { get; private set; }

    /// <summary>
    /// Primary output (Midline) for compatibility with AbstractBase
    /// </summary>
    public TValue Last => Midline;

    public bool IsHot => _state.IsHot;

    /// <summary>
    /// The slope of the linear regression line (trend direction)
    /// Positive = uptrend, Negative = downtrend
    /// </summary>
    public double Slope => _state.Slope;

    /// <summary>
    /// The standard deviation of residuals (price dispersion around trend)
    /// </summary>
    public double StdDev => _state.StdDev;

    /// <summary>
    /// Coefficient of determination (R²) measuring trend quality.
    /// Range: 0 to 1. Higher values indicate a cleaner, more reliable trend.
    /// R² > 0.8 suggests strong linear trend.
    /// </summary>
    public double RSquared => _state.RSquared;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the TTM Linear Regression Channel indicator.
    /// </summary>
    /// <param name="period">Lookback period for regression (default 100, must be > 1)</param>
    public TtmLrc(int period = 100)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 1.");
        }

        _period = period;
        _buffer = new double[period];
        _p_buffer = new double[period];
        WarmupPeriod = period;
        Name = $"TtmLrc({period})";
        _valueHandler = HandleValue;

        // Precompute constants
        // sumX = 0 + 1 + ... + (n-1) = n(n-1)/2
        _sumX = 0.5 * period * (period - 1);
        // sumX² = 0² + 1² + ... + (n-1)² = (n-1)n(2n-1)/6
        double sumX2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        // denominator = n * sumX² - sumX²
        _denominator = period * sumX2 - _sumX * _sumX;

        Reset();
    }

    public TtmLrc(TSeries source, int period = 100) : this(period)
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
        _state = new State(0, 0, double.NaN, 0, 0, 0, false);
        _p_state = _state;
        Array.Fill(_buffer, 0.0);
        _p_buffer = (double[])_buffer.Clone();
        Midline = default;
        Upper1 = default;
        Lower1 = default;
        Upper2 = default;
        Lower2 = default;
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
            Midline = new TValue(input.Time, value);
            Upper1 = new TValue(input.Time, value);
            Lower1 = new TValue(input.Time, value);
            Upper2 = new TValue(input.Time, value);
            Lower2 = new TValue(input.Time, value);
            _state = _state with { Slope = 0, StdDev = 0, RSquared = 0 };
            PubEvent(Midline, isNew);
            return Midline;
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

        // Calculate standard deviation of residuals and R²
        double sumResiduals2 = 0;
        double meanY = sumY / n;
        double ssTot = 0;

        for (int i = 0; i < count; i++)
        {
            double predicted = Math.FusedMultiplyAdd(slope, i, intercept);
            double residual = values[i] - predicted;
            sumResiduals2 = Math.FusedMultiplyAdd(residual, residual, sumResiduals2);

            double devFromMean = values[i] - meanY;
            ssTot = Math.FusedMultiplyAdd(devFromMean, devFromMean, ssTot);
        }

        double stdDev = Math.Sqrt(sumResiduals2 / n);

        // Compute R² (coefficient of determination)
        double rSquared = ssTot > 1e-10 ? 1.0 - (sumResiduals2 / ssTot) : 0.0;
        rSquared = Math.Clamp(rSquared, 0.0, 1.0);

        if (!_state.IsHot && count >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        _state = _state with { Slope = slope, StdDev = stdDev, RSquared = rSquared };

        Midline = new TValue(input.Time, regression);
        Upper1 = new TValue(input.Time, regression + stdDev);
        Lower1 = new TValue(input.Time, regression - stdDev);
        Upper2 = new TValue(input.Time, regression + 2.0 * stdDev);
        Lower2 = new TValue(input.Time, regression - 2.0 * stdDev);

        PubEvent(Midline, isNew);
        return Midline;
    }

    public (TSeries Midline, TSeries Upper1, TSeries Lower1, TSeries Upper2, TSeries Lower2) Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tMid = new List<long>(len);
        var vMid = new List<double>(len);
        var vU1 = new List<double>(len);
        var vL1 = new List<double>(len);
        var vU2 = new List<double>(len);
        var vL2 = new List<double>(len);

        CollectionsMarshal.SetCount(tMid, len);
        CollectionsMarshal.SetCount(vMid, len);
        CollectionsMarshal.SetCount(vU1, len);
        CollectionsMarshal.SetCount(vL1, len);
        CollectionsMarshal.SetCount(vU2, len);
        CollectionsMarshal.SetCount(vL2, len);

        var tSpan = CollectionsMarshal.AsSpan(tMid);
        var vMidSpan = CollectionsMarshal.AsSpan(vMid);
        var vU1Span = CollectionsMarshal.AsSpan(vU1);
        var vL1Span = CollectionsMarshal.AsSpan(vL1);
        var vU2Span = CollectionsMarshal.AsSpan(vU2);
        var vL2Span = CollectionsMarshal.AsSpan(vL2);

        Batch(source.Values, vMidSpan, vU1Span, vL1Span, vU2Span, vL2Span, _period);

        source.Times.CopyTo(tSpan);

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Midline = new TValue(lastTime, vMidSpan[^1]);
        Upper1 = new TValue(lastTime, vU1Span[^1]);
        Lower1 = new TValue(lastTime, vL1Span[^1]);
        Upper2 = new TValue(lastTime, vU2Span[^1]);
        Lower2 = new TValue(lastTime, vL2Span[^1]);

        return (
            new TSeries(tMid, vMid),
            new TSeries(new List<long>(tMid), vU1),
            new TSeries(new List<long>(tMid), vL1),
            new TSeries(new List<long>(tMid), vU2),
            new TSeries(new List<long>(tMid), vL2)
        );
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
    /// Batch calculation using spans. Outputs midline and all four bands.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> midline,
        Span<double> upper1,
        Span<double> lower1,
        Span<double> upper2,
        Span<double> lower2,
        int period)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 1.");
        }

        if (midline.Length < source.Length ||
            upper1.Length < source.Length ||
            lower1.Length < source.Length ||
            upper2.Length < source.Length ||
            lower2.Length < source.Length)
        {
            throw new ArgumentException("Output spans must be at least as long as input", nameof(midline));
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

        // Track last valid value for NaN substitution
        double lastValid = double.NaN;

        for (int i = 0; i < len; i++)
        {
            // Get valid value with last-valid substitution
            double currentValue = source[i];
            if (double.IsFinite(currentValue))
            {
                lastValid = currentValue;
            }
            else
            {
                currentValue = lastValid;
            }

            // If still NaN (no valid value seen yet), output NaN
            if (!double.IsFinite(currentValue))
            {
                midline[i] = double.NaN;
                upper1[i] = double.NaN;
                lower1[i] = double.NaN;
                upper2[i] = double.NaN;
                lower2[i] = double.NaN;
                continue;
            }

            int count = Math.Min(i + 1, period);
            int start = i - count + 1;

            if (count <= 1)
            {
                midline[i] = currentValue;
                upper1[i] = currentValue;
                lower1[i] = currentValue;
                upper2[i] = currentValue;
                lower2[i] = currentValue;
                continue;
            }

            // Calculate sums for linear regression with NaN handling
            double sumY = 0;
            double sumXY = 0;
            double lastValidInWindow = double.NaN;

            for (int j = 0; j < count; j++)
            {
                double rawY = source[start + j];
                double y;
                if (double.IsFinite(rawY))
                {
                    lastValidInWindow = rawY;
                    y = rawY;
                }
                else
                {
                    y = double.IsFinite(lastValidInWindow) ? lastValidInWindow : 0.0;
                }
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

            // Calculate standard deviation of residuals with NaN handling
            double sumResiduals2 = 0;
            lastValidInWindow = double.NaN;
            for (int j = 0; j < count; j++)
            {
                double rawY = source[start + j];
                double y;
                if (double.IsFinite(rawY))
                {
                    lastValidInWindow = rawY;
                    y = rawY;
                }
                else
                {
                    y = double.IsFinite(lastValidInWindow) ? lastValidInWindow : 0.0;
                }
                double predicted = Math.FusedMultiplyAdd(slope, j, intercept);
                double residual = y - predicted;
                sumResiduals2 = Math.FusedMultiplyAdd(residual, residual, sumResiduals2);
            }

            double stdDev = Math.Sqrt(sumResiduals2 / n);

            midline[i] = regression;
            upper1[i] = regression + stdDev;
            lower1[i] = regression - stdDev;
            upper2[i] = regression + 2.0 * stdDev;
            lower2[i] = regression - 2.0 * stdDev;
        }
    }

    public static (TSeries Midline, TSeries Upper1, TSeries Lower1, TSeries Upper2, TSeries Lower2) Batch(TSeries source, int period = 100)
    {
        int len = source.Count;
        var tMid = new List<long>(len);
        var vMid = new List<double>(len);
        var vU1 = new List<double>(len);
        var vL1 = new List<double>(len);
        var vU2 = new List<double>(len);
        var vL2 = new List<double>(len);

        CollectionsMarshal.SetCount(tMid, len);
        CollectionsMarshal.SetCount(vMid, len);
        CollectionsMarshal.SetCount(vU1, len);
        CollectionsMarshal.SetCount(vL1, len);
        CollectionsMarshal.SetCount(vU2, len);
        CollectionsMarshal.SetCount(vL2, len);

        Batch(source.Values,
              CollectionsMarshal.AsSpan(vMid),
              CollectionsMarshal.AsSpan(vU1),
              CollectionsMarshal.AsSpan(vL1),
              CollectionsMarshal.AsSpan(vU2),
              CollectionsMarshal.AsSpan(vL2),
              period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMid));

        return (
            new TSeries(tMid, vMid),
            new TSeries(new List<long>(tMid), vU1),
            new TSeries(new List<long>(tMid), vL1),
            new TSeries(new List<long>(tMid), vU2),
            new TSeries(new List<long>(tMid), vL2)
        );
    }

    public static ((TSeries Midline, TSeries Upper1, TSeries Lower1, TSeries Upper2, TSeries Lower2) Results, TtmLrc Indicator) Calculate(TSeries source, int period = 100)
    {
        var indicator = new TtmLrc(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
