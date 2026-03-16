using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Correlation: Calculates Pearson's correlation coefficient between two price series
/// using a streaming single-pass algorithm with circular buffers and Kahan compensated
/// summation for numerical stability over long streams.
/// </summary>
/// <remarks>
/// The Pearson correlation coefficient measures the linear relationship between two variables.
/// It ranges from -1 (perfect negative correlation) to +1 (perfect positive correlation).
///
/// Algorithm:
/// 1. Maintain running sums: Σx, Σy, Σx², Σy², Σxy
/// 2. Calculate means: μx = Σx/n, μy = Σy/n
/// 3. Calculate variances: σx² = Σx²/n - μx², σy² = Σy²/n - μy²
/// 4. Calculate covariance: cov(x,y) = Σxy/n - μx×μy
/// 5. Correlation: r = cov(x,y) / (σx × σy)
///
/// Interpretation:
/// - r = +1: Perfect positive linear relationship
/// - r = -1: Perfect negative linear relationship
/// - r = 0: No linear relationship
/// - |r| > 0.7: Strong correlation
/// - 0.3 < |r| < 0.7: Moderate correlation
/// - |r| < 0.3: Weak correlation
/// </remarks>
[SkipLocalsInit]
public sealed class Correl : AbstractBase
{
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    // Running sums for O(1) statistics
    private double _sumX, _sumY;
    private double _sumX2, _sumY2;
    private double _sumXY;

    // Kahan compensation terms
    private double _sumXComp, _sumYComp;
    private double _sumX2Comp, _sumY2Comp;
    private double _sumXYComp;

    // Previous compensation state for rollback
    private double _p_sumXComp, _p_sumYComp;
    private double _p_sumX2Comp, _p_sumY2Comp;
    private double _p_sumXYComp;

    // Last valid values for NaN handling
    private double _lastValidX, _lastValidY;
    private double _p_lastValidX, _p_lastValidY;

    private const double Epsilon = 1e-10;

    /// <inheritdoc />
    public override bool IsHot => _bufferX.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Correl indicator.
    /// </summary>
    /// <param name="period">Lookback period for calculation (must be > 1)</param>
    public Correl(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);

        Name = $"Correl({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Updates the Correlation indicator with new values from both series.
    /// </summary>
    /// <param name="seriesX">First series value</param>
    /// <param name="seriesY">Second series value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The Pearson correlation coefficient (-1 to +1)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue seriesX, TValue seriesY, bool isNew = true)
    {
        if (isNew)
        {
            _p_lastValidX = _lastValidX;
            _p_lastValidY = _lastValidY;
            _p_sumXComp = _sumXComp;
            _p_sumYComp = _sumYComp;
            _p_sumX2Comp = _sumX2Comp;
            _p_sumY2Comp = _sumY2Comp;
            _p_sumXYComp = _sumXYComp;
        }
        else
        {
            _lastValidX = _p_lastValidX;
            _lastValidY = _p_lastValidY;
            _sumXComp = _p_sumXComp;
            _sumYComp = _p_sumYComp;
            _sumX2Comp = _p_sumX2Comp;
            _sumY2Comp = _p_sumY2Comp;
            _sumXYComp = _p_sumXYComp;
        }

        double x = SanitizeX(seriesX.Value);
        double y = SanitizeY(seriesY.Value);

        if (isNew)
        {
            ProcessNewBar(x, y);
        }
        else
        {
            ProcessBarCorrection(x, y);
        }

        double correlation = CalculateCorrel();

        Last = new TValue(seriesX.Time, correlation);
        PubEvent(Last);
        return Last;
    }

    /// <summary>
    /// Updates with raw double values.
    /// </summary>
    /// <remarks>
    /// Stamps both inputs with <c>DateTime.UtcNow</c> as their timestamp. For
    /// deterministic or replay-safe sequences use
    /// <see cref="Update(TValue, TValue, bool)"/> with explicit timestamps instead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double seriesX, double seriesY, bool isNew = true)
    {
        DateTime now = DateTime.UtcNow;
        return Update(new TValue(now, seriesX), new TValue(now, seriesY), isNew);
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Update(TValue, TValue, bool)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Update(seriesX, seriesY) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Correl requires two inputs (seriesX and seriesY). Use Update(seriesX, seriesY).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Calculate(seriesX, seriesY, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Correl requires two inputs. Use Batch(seriesX, seriesY, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeX(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidX = value;
            return value;
        }
        return double.IsFinite(_lastValidX) ? _lastValidX : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeY(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidY = value;
            return value;
        }
        return double.IsFinite(_lastValidY) ? _lastValidY : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessNewBar(double x, double y)
    {
        // Remove oldest values if buffer is full
        if (_bufferX.IsFull)
        {
            double oldX = _bufferX.Oldest;
            double oldY = _bufferY.Oldest;

            // Kahan subtract oldX from _sumX
            {
                double yk = -oldX - _sumXComp;
                double t = _sumX + yk;
                _sumXComp = (t - _sumX) - yk;
                _sumX = t;
            }
            // Kahan subtract oldY from _sumY
            {
                double yk = -oldY - _sumYComp;
                double t = _sumY + yk;
                _sumYComp = (t - _sumY) - yk;
                _sumY = t;
            }
            // Kahan subtract oldX² from _sumX2
            {
                double yk = -(oldX * oldX) - _sumX2Comp;
                double t = _sumX2 + yk;
                _sumX2Comp = (t - _sumX2) - yk;
                _sumX2 = t;
            }
            // Kahan subtract oldY² from _sumY2
            {
                double yk = -(oldY * oldY) - _sumY2Comp;
                double t = _sumY2 + yk;
                _sumY2Comp = (t - _sumY2) - yk;
                _sumY2 = t;
            }
            // Kahan subtract oldX*oldY from _sumXY
            {
                double yk = -(oldX * oldY) - _sumXYComp;
                double t = _sumXY + yk;
                _sumXYComp = (t - _sumXY) - yk;
                _sumXY = t;
            }
        }

        // Add new values
        _bufferX.Add(x);
        _bufferY.Add(y);

        // Kahan add x to _sumX
        {
            double yk = x - _sumXComp;
            double t = _sumX + yk;
            _sumXComp = (t - _sumX) - yk;
            _sumX = t;
        }
        // Kahan add y to _sumY
        {
            double yk = y - _sumYComp;
            double t = _sumY + yk;
            _sumYComp = (t - _sumY) - yk;
            _sumY = t;
        }
        // Kahan add x² to _sumX2
        {
            double yk = (x * x) - _sumX2Comp;
            double t = _sumX2 + yk;
            _sumX2Comp = (t - _sumX2) - yk;
            _sumX2 = t;
        }
        // Kahan add y² to _sumY2
        {
            double yk = (y * y) - _sumY2Comp;
            double t = _sumY2 + yk;
            _sumY2Comp = (t - _sumY2) - yk;
            _sumY2 = t;
        }
        // Kahan add x*y to _sumXY
        {
            double yk = (x * y) - _sumXYComp;
            double t = _sumXY + yk;
            _sumXYComp = (t - _sumXY) - yk;
            _sumXY = t;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double x, double y)
    {
        if (_bufferX.Count == 0)
        {
            // Nothing to correct yet; no current bar exists
            return;
        }

        // Get the current newest values (which are wrong and need to be corrected)
        double oldX = _bufferX.Newest;
        double oldY = _bufferY.Newest;

        // Kahan subtract old + add new for _sumX
        {
            double yk = (-oldX + x) - _sumXComp;
            double t = _sumX + yk;
            _sumXComp = (t - _sumX) - yk;
            _sumX = t;
        }
        // Kahan subtract old + add new for _sumY
        {
            double yk = (-oldY + y) - _sumYComp;
            double t = _sumY + yk;
            _sumYComp = (t - _sumY) - yk;
            _sumY = t;
        }
        // Kahan subtract old² + add new² for _sumX2
        {
            double yk = (-(oldX * oldX) + (x * x)) - _sumX2Comp;
            double t = _sumX2 + yk;
            _sumX2Comp = (t - _sumX2) - yk;
            _sumX2 = t;
        }
        // Kahan subtract old² + add new² for _sumY2
        {
            double yk = (-(oldY * oldY) + (y * y)) - _sumY2Comp;
            double t = _sumY2 + yk;
            _sumY2Comp = (t - _sumY2) - yk;
            _sumY2 = t;
        }
        // Kahan subtract old*old + add new*new for _sumXY
        {
            double yk = (-(oldX * oldY) + (x * y)) - _sumXYComp;
            double t = _sumXY + yk;
            _sumXYComp = (t - _sumXY) - yk;
            _sumXY = t;
        }

        // Update the buffer values
        _bufferX.UpdateNewest(x);
        _bufferY.UpdateNewest(y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateCorrel()
    {
        int n = _bufferX.Count;
        if (n < 2)
        {
            return double.NaN;
        }

        // Calculate means
        double meanX = _sumX / n;
        double meanY = _sumY / n;

        // Calculate variances (population variance)
        double varX = Max(0.0, (_sumX2 / n) - (meanX * meanX));
        double varY = Max(0.0, (_sumY2 / n) - (meanY * meanY));

        // Calculate covariance
        double cov = (_sumXY / n) - (meanX * meanY);

        // Calculate standard deviations
        double stdX = Sqrt(varX);
        double stdY = Sqrt(varY);

        // Calculate correlation
        double denominator = stdX * stdY;
        if (Abs(denominator) < Epsilon)
        {
            return double.NaN;
        }

        double correlation = cov / denominator;

        // Clamp to [-1, 1] range to handle floating point precision issues
        return Max(-1.0, Min(1.0, correlation));
    }

    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Correl requires two inputs.");
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _bufferX.Clear();
        _bufferY.Clear();

        _sumX = 0;
        _sumY = 0;
        _sumX2 = 0;
        _sumY2 = 0;
        _sumXY = 0;

        _sumXComp = 0;
        _sumYComp = 0;
        _sumX2Comp = 0;
        _sumY2Comp = 0;
        _sumXYComp = 0;

        _lastValidX = 0;
        _lastValidY = 0;
        _p_lastValidX = 0;
        _p_lastValidY = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates correlation for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesX, TSeries seriesY, int period = 20)
        => Calculate(seriesX, seriesY, period).Results;

    /// <summary>
    /// Static batch calculation for span-based processing.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> seriesX,
        ReadOnlySpan<double> seriesY,
        Span<double> output,
        int period = 20)
    {
        if (seriesX.Length != seriesY.Length)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        if (seriesX.Length != output.Length)
        {
            throw new ArgumentException("Output must have the same length as input", nameof(output));
        }

        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        var indicator = new Correl(period);

        for (int i = 0; i < seriesX.Length; i++)
        {
            var result = indicator.Update(seriesX[i], seriesY[i], isNew: true);
            output[i] = result.Value;
        }
    }

    /// <summary>
    /// Calculates Pearson correlation for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Correl Indicator) Calculate(TSeries seriesX, TSeries seriesY, int period = 20)
    {
        if (seriesX.Count != seriesY.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        var indicator = new Correl(period);
        var result = new TSeries(seriesX.Count);

        var timesX = seriesX.Times;
        var valuesX = seriesX.Values;
        var valuesY = seriesY.Values;

        for (int i = 0; i < seriesX.Count; i++)
        {
            result.Add(indicator.Update(new TValue(timesX[i], valuesX[i]), new TValue(timesX[i], valuesY[i]), isNew: true));
        }

        return (result, indicator);
    }
}
