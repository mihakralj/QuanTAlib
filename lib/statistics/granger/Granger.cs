using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Granger Causality: Tests whether one time series (X) helps predict another (Y)
/// by comparing restricted and unrestricted OLS regression models with lag-1.
/// </summary>
/// <remarks>
/// Algorithm (lag-1 Granger Causality F-test):
/// 1. Restricted model:  y_t = c0 + c1*y_{t-1} + e1  (Y predicted only by its own lag)
/// 2. Unrestricted model: y_t = d0 + d1*y_{t-1} + d2*x_{t-1} + e2  (Y predicted by both lags)
/// 3. F = ((SSR1 - SSR2) / 1) / (SSR2 / (N - 3))
///
/// Higher F-statistic values indicate stronger evidence that X Granger-causes Y.
/// The indicator uses running sums with Kahan compensated summation for O(1)
/// streaming updates with numerical stability over long streams.
/// Period must be greater than 3 (need N-3 > 0 degrees of freedom).
/// </remarks>
[SkipLocalsInit]
public sealed class Granger : AbstractBase
{
    private readonly RingBuffer _bufferY;
    private readonly RingBuffer _bufferX;

    // Running sums for means, variances, covariances over the window
    // y_t, y_{t-1}, x_{t-1}
    private double _sumY, _sumYLag, _sumXLag;
    private double _sumYY, _sumYLagYLag, _sumXLagXLag;
    private double _sumYYLag, _sumYXLag, _sumYLagXLag;

    // Kahan compensation terms
    private double _sumYComp, _sumYLagComp, _sumXLagComp;
    private double _sumYYComp, _sumYLagYLagComp, _sumXLagXLagComp;
    private double _sumYYLagComp, _sumYXLagComp, _sumYLagXLagComp;

    // Previous compensation state for rollback
    private double _p_sumYComp, _p_sumYLagComp, _p_sumXLagComp;
    private double _p_sumYYComp, _p_sumYLagYLagComp, _p_sumXLagXLagComp;
    private double _p_sumYYLagComp, _p_sumYXLagComp, _p_sumYLagXLagComp;

    // Previous values for lag computation
    private double _prevY, _prevX;
    private double _p_prevY, _p_prevX;
    private bool _hasPrev;
    private bool _p_hasPrev;

    // Ring buffers for the lagged triplet window (y_t, y_lag, x_lag)
    private readonly RingBuffer _windowY;
    private readonly RingBuffer _windowYLag;
    private readonly RingBuffer _windowXLag;

    // Last valid values for NaN handling
    private double _lastValidY, _lastValidX;
    private double _p_lastValidY, _p_lastValidX;

    private const double Epsilon = 1e-10;

    /// <inheritdoc />
    public override bool IsHot => _windowY.IsFull;

    /// <summary>
    /// Creates a new Granger Causality indicator.
    /// </summary>
    /// <param name="period">Lookback period for OLS regression (must be > 3)</param>
    public Granger(int period = 20)
    {
        if (period <= 3)
        {
            throw new ArgumentException("Period must be greater than 3", nameof(period));
        }

        _bufferY = new RingBuffer(2); // only need current + previous
        _bufferX = new RingBuffer(2);
        _windowY = new RingBuffer(period);
        _windowYLag = new RingBuffer(period);
        _windowXLag = new RingBuffer(period);

        Name = $"Granger({period})";
        WarmupPeriod = period + 1; // Need extra bar for first lag
    }

    /// <summary>
    /// Updates the Granger Causality indicator with new values from both series.
    /// </summary>
    /// <param name="seriesY">Dependent variable (series being predicted)</param>
    /// <param name="seriesX">Independent variable (hypothesized cause)</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The F-statistic (higher = stronger evidence X Granger-causes Y)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue seriesY, TValue seriesX, bool isNew = true)
    {
        double y = SanitizeY(seriesY.Value);
        double x = SanitizeX(seriesX.Value);

        if (isNew)
        {
            ProcessNewBar(y, x);
        }
        else
        {
            ProcessBarCorrection(y, x);
        }

        double fStat = CalculateFStatistic();

        Last = new TValue(seriesY.Time, fStat);
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
    public TValue Update(double seriesY, double seriesX, bool isNew = true)
    {
        DateTime now = DateTime.UtcNow;
        return Update(new TValue(now, seriesY), new TValue(now, seriesX), isNew);
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Update(TValue, TValue, bool)"/> instead.</summary>
    /// <remarks>Not supported for dual-input indicator. Use Update(seriesY, seriesX) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Granger requires two inputs (seriesY and seriesX). Use Update(seriesY, seriesX).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for dual-input indicator. Use Batch(seriesY, seriesX, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Granger requires two inputs. Use Batch(seriesY, seriesX, period).");
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
    private void ProcessNewBar(double y, double x)
    {
        // Save state for bar correction
        _p_lastValidY = _lastValidY;
        _p_lastValidX = _lastValidX;
        _p_prevY = _prevY;
        _p_prevX = _prevX;
        _p_hasPrev = _hasPrev;
        _p_sumYComp = _sumYComp;
        _p_sumYLagComp = _sumYLagComp;
        _p_sumXLagComp = _sumXLagComp;
        _p_sumYYComp = _sumYYComp;
        _p_sumYLagYLagComp = _sumYLagYLagComp;
        _p_sumXLagXLagComp = _sumXLagXLagComp;
        _p_sumYYLagComp = _sumYYLagComp;
        _p_sumYXLagComp = _sumYXLagComp;
        _p_sumYLagXLagComp = _sumYLagXLagComp;

        if (_hasPrev)
        {
            double yLag = _prevY;
            double xLag = _prevX;

            // Remove oldest triplet if window is full
            if (_windowY.IsFull)
            {
                double oldY = _windowY.Oldest;
                double oldYLag = _windowYLag.Oldest;
                double oldXLag = _windowXLag.Oldest;
                { double yk = -oldY - _sumYComp; double t = _sumY + yk; _sumYComp = (t - _sumY) - yk; _sumY = t; }
                { double yk = -oldYLag - _sumYLagComp; double t = _sumYLag + yk; _sumYLagComp = (t - _sumYLag) - yk; _sumYLag = t; }
                { double yk = -oldXLag - _sumXLagComp; double t = _sumXLag + yk; _sumXLagComp = (t - _sumXLag) - yk; _sumXLag = t; }
                { double yk = -(oldY * oldY) - _sumYYComp; double t = _sumYY + yk; _sumYYComp = (t - _sumYY) - yk; _sumYY = t; }
                { double yk = -(oldYLag * oldYLag) - _sumYLagYLagComp; double t = _sumYLagYLag + yk; _sumYLagYLagComp = (t - _sumYLagYLag) - yk; _sumYLagYLag = t; }
                { double yk = -(oldXLag * oldXLag) - _sumXLagXLagComp; double t = _sumXLagXLag + yk; _sumXLagXLagComp = (t - _sumXLagXLag) - yk; _sumXLagXLag = t; }
                { double yk = -(oldY * oldYLag) - _sumYYLagComp; double t = _sumYYLag + yk; _sumYYLagComp = (t - _sumYYLag) - yk; _sumYYLag = t; }
                { double yk = -(oldY * oldXLag) - _sumYXLagComp; double t = _sumYXLag + yk; _sumYXLagComp = (t - _sumYXLag) - yk; _sumYXLag = t; }
                { double yk = -(oldYLag * oldXLag) - _sumYLagXLagComp; double t = _sumYLagXLag + yk; _sumYLagXLagComp = (t - _sumYLagXLag) - yk; _sumYLagXLag = t; }
            }

            // Add new triplet
            _windowY.Add(y);
            _windowYLag.Add(yLag);
            _windowXLag.Add(xLag);
            { double yk = y - _sumYComp; double t = _sumY + yk; _sumYComp = (t - _sumY) - yk; _sumY = t; }
            { double yk = yLag - _sumYLagComp; double t = _sumYLag + yk; _sumYLagComp = (t - _sumYLag) - yk; _sumYLag = t; }
            { double yk = xLag - _sumXLagComp; double t = _sumXLag + yk; _sumXLagComp = (t - _sumXLag) - yk; _sumXLag = t; }
            { double yk = (y * y) - _sumYYComp; double t = _sumYY + yk; _sumYYComp = (t - _sumYY) - yk; _sumYY = t; }
            { double yk = (yLag * yLag) - _sumYLagYLagComp; double t = _sumYLagYLag + yk; _sumYLagYLagComp = (t - _sumYLagYLag) - yk; _sumYLagYLag = t; }
            { double yk = (xLag * xLag) - _sumXLagXLagComp; double t = _sumXLagXLag + yk; _sumXLagXLagComp = (t - _sumXLagXLag) - yk; _sumXLagXLag = t; }
            { double yk = (y * yLag) - _sumYYLagComp; double t = _sumYYLag + yk; _sumYYLagComp = (t - _sumYYLag) - yk; _sumYYLag = t; }
            { double yk = (y * xLag) - _sumYXLagComp; double t = _sumYXLag + yk; _sumYXLagComp = (t - _sumYXLag) - yk; _sumYXLag = t; }
            { double yk = (yLag * xLag) - _sumYLagXLagComp; double t = _sumYLagXLag + yk; _sumYLagXLagComp = (t - _sumYLagXLag) - yk; _sumYLagXLag = t; }
        }

        _prevY = y;
        _prevX = x;
        _hasPrev = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double y, double x)
    {
        // Restore state
        _lastValidY = _p_lastValidY;
        _lastValidX = _p_lastValidX;
        _prevY = _p_prevY;
        _prevX = _p_prevX;
        _hasPrev = _p_hasPrev;
        _sumYComp = _p_sumYComp;
        _sumYLagComp = _p_sumYLagComp;
        _sumXLagComp = _p_sumXLagComp;
        _sumYYComp = _p_sumYYComp;
        _sumYLagYLagComp = _p_sumYLagYLagComp;
        _sumXLagXLagComp = _p_sumXLagXLagComp;
        _sumYYLagComp = _p_sumYYLagComp;
        _sumYXLagComp = _p_sumYXLagComp;
        _sumYLagXLagComp = _p_sumYLagXLagComp;

        if (_hasPrev)
        {
            double yLag = _prevY;
            double xLag = _prevX;

            if (_windowY.Count > 0)
            {
                double oldY = _windowY.Newest;
                double oldYLag = _windowYLag.Newest;
                double oldXLag = _windowXLag.Newest;

                // Replace newest values with Kahan
                { double yk = (-oldY + y) - _sumYComp; double t = _sumY + yk; _sumYComp = (t - _sumY) - yk; _sumY = t; }
                { double yk = (-oldYLag + yLag) - _sumYLagComp; double t = _sumYLag + yk; _sumYLagComp = (t - _sumYLag) - yk; _sumYLag = t; }
                { double yk = (-oldXLag + xLag) - _sumXLagComp; double t = _sumXLag + yk; _sumXLagComp = (t - _sumXLag) - yk; _sumXLag = t; }
                { double yk = (-(oldY * oldY) + (y * y)) - _sumYYComp; double t = _sumYY + yk; _sumYYComp = (t - _sumYY) - yk; _sumYY = t; }
                { double yk = (-(oldYLag * oldYLag) + (yLag * yLag)) - _sumYLagYLagComp; double t = _sumYLagYLag + yk; _sumYLagYLagComp = (t - _sumYLagYLag) - yk; _sumYLagYLag = t; }
                { double yk = (-(oldXLag * oldXLag) + (xLag * xLag)) - _sumXLagXLagComp; double t = _sumXLagXLag + yk; _sumXLagXLagComp = (t - _sumXLagXLag) - yk; _sumXLagXLag = t; }
                { double yk = (-(oldY * oldYLag) + (y * yLag)) - _sumYYLagComp; double t = _sumYYLag + yk; _sumYYLagComp = (t - _sumYYLag) - yk; _sumYYLag = t; }
                { double yk = (-(oldY * oldXLag) + (y * xLag)) - _sumYXLagComp; double t = _sumYXLag + yk; _sumYXLagComp = (t - _sumYXLag) - yk; _sumYXLag = t; }
                { double yk = (-(oldYLag * oldXLag) + (yLag * xLag)) - _sumYLagXLagComp; double t = _sumYLagXLag + yk; _sumYLagXLagComp = (t - _sumYLagXLag) - yk; _sumYLagXLag = t; }

                _windowY.UpdateNewest(y);
                _windowYLag.UpdateNewest(yLag);
                _windowXLag.UpdateNewest(xLag);
            }
            else
            {
                _windowY.Add(y);
                _windowYLag.Add(yLag);
                _windowXLag.Add(xLag);
                _sumY = y;
                _sumYLag = yLag;
                _sumXLag = xLag;
                _sumYY = y * y;
                _sumYLagYLag = yLag * yLag;
                _sumXLagXLag = xLag * xLag;
                _sumYYLag = y * yLag;
                _sumYXLag = y * xLag;
                _sumYLagXLag = yLag * xLag;
                _sumYComp = 0; _sumYLagComp = 0; _sumXLagComp = 0;
                _sumYYComp = 0; _sumYLagYLagComp = 0; _sumXLagXLagComp = 0;
                _sumYYLagComp = 0; _sumYXLagComp = 0; _sumYLagXLagComp = 0;
            }
        }

        _prevY = y;
        _prevX = x;
        _hasPrev = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateFStatistic()
    {
        int n = _windowY.Count;
        if (n < 4) // Need at least 4 observations (period > 3 constraint)
        {
            return double.NaN;
        }

        // Means
        double meanY = _sumY / n;
        double meanYLag = _sumYLag / n;
        double meanXLag = _sumXLag / n;

        // Population variances
        double varYLag = Max(0.0, (_sumYLagYLag / n) - (meanYLag * meanYLag));
        double varXLag = Max(0.0, (_sumXLagXLag / n) - (meanXLag * meanXLag));

        // Covariances
        double covYYLag = (_sumYYLag / n) - (meanY * meanYLag);
        double covYXLag = (_sumYXLag / n) - (meanY * meanXLag);
        double covYLagXLag = (_sumYLagXLag / n) - (meanYLag * meanXLag);

        // ---- Restricted model: y_t = c0 + c1*y_{t-1} ----
        if (varYLag < Epsilon)
        {
            return double.NaN; // Cannot compute OLS if y_lag has no variance
        }

        double slopeRestricted = covYYLag / varYLag;

        // SSR1 = sum((y_i - c0 - c1*yLag_i)^2) computed from running sums
        // = sumYY - 2*c0*sumY - 2*c1*sumYYLag + n*c0^2 + 2*c0*c1*sumYLag + c1^2*sumYLagYLag
        double varY = Max(0.0, (_sumYY / n) - (meanY * meanY));
        // skipcq: CS-R1073 - SSR from residual variance: Var(y) - slope^2*Var(ylag)
        double ssr1 = (varY - (slopeRestricted * slopeRestricted * varYLag)) * n;
        ssr1 = Max(0.0, ssr1);

        // ---- Unrestricted model: y_t = d0 + d1*y_{t-1} + d2*x_{t-1} ----
        double denom = FusedMultiplyAdd(varYLag, varXLag, -(covYLagXLag * covYLagXLag));
        if (Abs(denom) < Epsilon)
        {
            return double.NaN; // Multicollinearity - cannot compute 2-variable OLS
        }

        double d1 = FusedMultiplyAdd(covYYLag, varXLag, -(covYXLag * covYLagXLag)) / denom;
        double d2 = FusedMultiplyAdd(covYXLag, varYLag, -(covYYLag * covYLagXLag)) / denom;
        double d0 = meanY - (d1 * meanYLag) - (d2 * meanXLag);

        // SSR2 computed by iterating the window (more numerically stable for small n)
        double ssr2 = 0.0;
        for (int i = 0; i < n; i++)
        {
            double yi = _windowY[i];
            double yLagi = _windowYLag[i];
            double xLagi = _windowXLag[i];
            double resid = yi - (d0 + (d1 * yLagi) + (d2 * xLagi));
            ssr2 = FusedMultiplyAdd(resid, resid, ssr2);
        }

        if (ssr2 < Epsilon)
        {
            return double.NaN; // Perfect fit in unrestricted model
        }

        // F = ((SSR1 - SSR2) / q) / (SSR2 / (N - k))
        // q = 1 (one restriction: d2 = 0)
        // k = 3 (parameters in unrestricted: d0, d1, d2)
        int degreesOfFreedom = n - 3;
        if (degreesOfFreedom <= 0)
        {
            return double.NaN;
        }

        double fStat = ((ssr1 - ssr2) / 1.0) / (ssr2 / degreesOfFreedom);
        return Max(0.0, fStat);
    }

    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Granger requires two inputs.");
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _bufferY.Clear();
        _bufferX.Clear();
        _windowY.Clear();
        _windowYLag.Clear();
        _windowXLag.Clear();

        _sumY = 0;
        _sumYLag = 0;
        _sumXLag = 0;
        _sumYY = 0;
        _sumYLagYLag = 0;
        _sumXLagXLag = 0;
        _sumYYLag = 0;
        _sumYXLag = 0;
        _sumYLagXLag = 0;

        _sumYComp = 0;
        _sumYLagComp = 0;
        _sumXLagComp = 0;
        _sumYYComp = 0;
        _sumYLagYLagComp = 0;
        _sumXLagXLagComp = 0;
        _sumYYLagComp = 0;
        _sumYXLagComp = 0;
        _sumYLagXLagComp = 0;

        _prevY = 0;
        _prevX = 0;
        _p_prevY = 0;
        _p_prevX = 0;
        _hasPrev = false;
        _p_hasPrev = false;

        _lastValidY = 0;
        _lastValidX = 0;
        _p_lastValidY = 0;
        _p_lastValidX = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates Granger Causality F-statistic for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesY, TSeries seriesX, int period = 20)
    {
        if (seriesY.Count != seriesX.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesX));
        }

        var indicator = new Granger(period);
        var result = new TSeries(seriesY.Count);

        var timesY = seriesY.Times;
        var valuesY = seriesY.Values;
        var valuesX = seriesX.Values;

        for (int i = 0; i < seriesY.Count; i++)
        {
            var tvalY = new TValue(timesY[i], valuesY[i]);
            var tvalX = new TValue(timesY[i], valuesX[i]);
            result.Add(indicator.Update(tvalY, tvalX, isNew: true));
        }

        return result;
    }

    /// <summary>
    /// Static batch calculation for span-based processing.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> seriesY,
        ReadOnlySpan<double> seriesX,
        Span<double> output,
        int period = 20)
    {
        if (seriesY.Length != seriesX.Length)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesX));
        }

        if (seriesY.Length != output.Length)
        {
            throw new ArgumentException("Output must have the same length as input", nameof(output));
        }

        if (period <= 3)
        {
            throw new ArgumentException("Period must be greater than 3", nameof(period));
        }

        var indicator = new Granger(period);

        for (int i = 0; i < seriesY.Length; i++)
        {
            var result = indicator.Update(seriesY[i], seriesX[i], isNew: true);
            output[i] = result.Value;
        }
    }

    /// <summary>
    /// Calculates the Granger Causality F-statistic for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Granger Indicator) Calculate(TSeries seriesY, TSeries seriesX, int period = 20)
    {
        if (seriesY.Count != seriesX.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesX));
        }

        var indicator = new Granger(period);
        var result = new TSeries(seriesY.Count);

        var timesY = seriesY.Times;
        var valuesY = seriesY.Values;
        var valuesX = seriesX.Values;

        for (int i = 0; i < seriesY.Count; i++)
        {
            var tvalY = new TValue(timesY[i], valuesY[i]);
            var tvalX = new TValue(timesY[i], valuesX[i]);
            result.Add(indicator.Update(tvalY, tvalX, isNew: true));
        }

        return (result, indicator);
    }
}
