using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Correlation: Calculates Pearson's correlation coefficient between two price series
/// using a streaming single-pass algorithm with circular buffers.
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
public sealed class Correlation : AbstractBase
{
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    // Running sums for O(1) statistics
    private double _sumX, _sumY;
    private double _sumX2, _sumY2;
    private double _sumXY;

    // Last valid values for NaN handling
    private double _lastValidX, _lastValidY;
    private double _p_lastValidX, _p_lastValidY;

    private int _updateCount;
    private const int ResyncInterval = 1000;
    private const double Epsilon = 1e-10;

    /// <inheritdoc />
    public override bool IsHot => _bufferX.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Correlation indicator.
    /// </summary>
    /// <param name="period">Lookback period for calculation (must be > 1)</param>
    public Correlation(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);

        Name = $"Correlation({period})";
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
        }
        else
        {
            _lastValidX = _p_lastValidX;
            _lastValidY = _p_lastValidY;
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

        double correlation = CalculateCorrelation();

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
        return Update(new TValue(DateTime.UtcNow, seriesX), new TValue(DateTime.UtcNow, seriesY), isNew);
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Update(TValue, TValue, bool)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Update(seriesX, seriesY) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Correlation requires two inputs (seriesX and seriesY). Use Update(seriesX, seriesY).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Calculate(seriesX, seriesY, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Correlation requires two inputs. Use Batch(seriesX, seriesY, period).");
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
            _sumX -= oldX;
            _sumY -= oldY;
            _sumX2 = FusedMultiplyAdd(-oldX, oldX, _sumX2);
            _sumY2 = FusedMultiplyAdd(-oldY, oldY, _sumY2);
            _sumXY = FusedMultiplyAdd(-oldX, oldY, _sumXY);
        }

        // Add new values
        _bufferX.Add(x);
        _bufferY.Add(y);

        _sumX += x;
        _sumY += y;
        _sumX2 = FusedMultiplyAdd(x, x, _sumX2);
        _sumY2 = FusedMultiplyAdd(y, y, _sumY2);
        _sumXY = FusedMultiplyAdd(x, y, _sumXY);

        _updateCount++;
        if (_updateCount % ResyncInterval == 0)
        {
            Resync();
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

        // Update the running sums: remove old, add new (using FMA for consistency with ProcessNewBar)
        _sumX = _sumX - oldX + x;
        _sumY = _sumY - oldY + y;
        _sumX2 = FusedMultiplyAdd(x, x, FusedMultiplyAdd(-oldX, oldX, _sumX2));
        _sumY2 = FusedMultiplyAdd(y, y, FusedMultiplyAdd(-oldY, oldY, _sumY2));
        _sumXY = FusedMultiplyAdd(x, y, FusedMultiplyAdd(-oldX, oldY, _sumXY));

        // Update the buffer values
        _bufferX.UpdateNewest(x);
        _bufferY.UpdateNewest(y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateCorrelation()
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

    private void Resync()
    {
        _sumX = 0;
        _sumY = 0;
        _sumX2 = 0;
        _sumY2 = 0;
        _sumXY = 0;

        for (int i = 0; i < _bufferX.Count; i++)
        {
            double x = _bufferX[i];
            double y = _bufferY[i];
            _sumX += x;
            _sumY += y;
            _sumX2 = FusedMultiplyAdd(x, x, _sumX2);
            _sumY2 = FusedMultiplyAdd(y, y, _sumY2);
            _sumXY = FusedMultiplyAdd(x, y, _sumXY);
        }
    }
    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Correlation requires two inputs.");
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

        _lastValidX = 0;
        _lastValidY = 0;
        _p_lastValidX = 0;
        _p_lastValidY = 0;

        _updateCount = 0;
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

        var indicator = new Correlation(period);

        for (int i = 0; i < seriesX.Length; i++)
        {
            var result = indicator.Update(seriesX[i], seriesY[i], isNew: true);
            output[i] = result.Value;
        }
    }

    /// <summary>
    /// Calculates Pearson correlation for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Correlation Indicator) Calculate(TSeries seriesX, TSeries seriesY, int period = 20)
    {
        if (seriesX.Count != seriesY.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        var indicator = new Correlation(period);
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
