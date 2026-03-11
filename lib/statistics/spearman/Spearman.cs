using System.Buffers;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Spearman Rank Correlation Coefficient (Spearman's Ï), which measures
/// the monotonic relationship between two series by applying Pearson correlation to
/// their ranks.
/// </summary>
/// <remarks>
/// Spearman's Rho Algorithm:
/// <c>Ï = Pearson(rank(X), rank(Y))</c>
///
/// Ranks are 1-based with average-rank tie-breaking: if k values share the same value,
/// each receives the mean of the positions they would occupy.
///
/// When no ties exist, the simplified formula applies:
/// <c>Ï = 1 - 6Â·Î£dÂ² / (nÂ·(nÂ²-1))</c>, where d_i = rank(x_i) - rank(y_i).
///
/// This implementation uses the general Pearson-on-ranks method because ties can occur
/// in financial data (identical closes, rounded prices). Ranking is O(nÂ²) per series.
///
/// Non-finite inputs (NaN/Â±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Spearman.md">Detailed documentation</seealso>
/// <seealso href="spearman.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Spearman : AbstractBase
{
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    private double _lastValidX, _lastValidY;

    private const double Epsilon = 1e-10;
    private const int StackallocThreshold = 256;

    /// <inheritdoc />
    public override bool IsHot => _bufferX.Count >= 2;

    /// <summary>
    /// Creates a new Spearman Rank Correlation indicator.
    /// </summary>
    /// <param name="period">Lookback period for calculation (must be &gt; 1)</param>
    public Spearman(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);

        Name = $"Spearman({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Updates the Spearman indicator with new values from both series.
    /// </summary>
    /// <param name="seriesX">First series value</param>
    /// <param name="seriesY">Second series value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>Spearman's Ï coefficient (-1 to +1)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue seriesX, TValue seriesY, bool isNew = true)
    {
        double x = SanitizeX(seriesX.Value);
        double y = SanitizeY(seriesY.Value);

        if (isNew || _bufferX.Count == 0)
        {
            _bufferX.Add(x);
            _bufferY.Add(y);
        }
        else
        {
            _bufferX.UpdateNewest(x);
            _bufferY.UpdateNewest(y);
        }

        double rho = CalculateRho();

        Last = new TValue(seriesX.Time, rho);
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
    /// <remarks>Not supported for dual-input indicator. Use Update(seriesX, seriesY) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Spearman requires two inputs (seriesX and seriesY). Use Update(seriesX, seriesY).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for dual-input indicator. Use Batch(seriesX, seriesY, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Spearman requires two inputs. Use Batch(seriesX, seriesY, period).");
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
    private double CalculateRho()
    {
        int n = _bufferX.Count;
        if (n < 2)
        {
            return double.NaN;
        }

        // Allocate rank arrays â€” stackalloc for small, ArrayPool for large
        double[]? rentedRx = null;
        double[]? rentedRy = null;
        scoped Span<double> rankX;
        scoped Span<double> rankY;

        if (n <= StackallocThreshold)
        {
            rankX = stackalloc double[n];
            rankY = stackalloc double[n];
        }
        else
        {
            rentedRx = ArrayPool<double>.Shared.Rent(n);
            rentedRy = ArrayPool<double>.Shared.Rent(n);
            rankX = rentedRx.AsSpan(0, n);
            rankY = rentedRy.AsSpan(0, n);
        }

        try
        {
            // Compute ranks for X and Y (average-rank tie-breaking)
            ComputeRanks(_bufferX, n, rankX);
            ComputeRanks(_bufferY, n, rankY);

            // Pearson correlation on ranks
            // For ranks 1..n without ties, mean = (n+1)/2
            // With ties, mean still = (n+1)/2 because average-rank preserves sum
            double meanRank = (n + 1) * 0.5;

            double sumXY = 0;
            double sumXX = 0;
            double sumYY = 0;

            for (int i = 0; i < n; i++)
            {
                double dx = rankX[i] - meanRank;
                double dy = rankY[i] - meanRank;
                sumXY += dx * dy;
                sumXX += dx * dx;
                sumYY += dy * dy;
            }

            if (sumXX < Epsilon || sumYY < Epsilon)
            {
                return 0.0; // Constant series â†’ zero correlation
            }

            return sumXY / Math.Sqrt(sumXX * sumYY);
        }
        finally
        {
            if (rentedRx is not null)
            {
                ArrayPool<double>.Shared.Return(rentedRx);
            }
            if (rentedRy is not null)
            {
                ArrayPool<double>.Shared.Return(rentedRy);
            }
        }
    }

    /// <summary>
    /// Computes 1-based average ranks for buffer values. O(nÂ²).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeRanks(RingBuffer buffer, int n, Span<double> ranks)
    {
        for (int i = 0; i < n; i++)
        {
            double vi = buffer[i];
            int countSmaller = 0;
            int countEqual = 0;

            for (int j = 0; j < n; j++)
            {
                double vj = buffer[j];
                if (vj < vi)
                {
                    countSmaller++;
                }
                if (vj == vi) // skipcq: CS-R1077 - Exact-equality required: Spearman tie-detection needs bit-identical values; epsilon would create false ties
                {
                    countEqual++; // includes self
                }
            }

            // Average rank: 1-based position = countSmaller + (countEqual - 1) / 2.0 + 1
            ranks[i] = countSmaller + ((countEqual - 1) * 0.5) + 1.0;
        }
    }
    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Spearman requires two inputs.");
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _bufferX.Clear();
        _bufferY.Clear();

        _lastValidX = 0;
        _lastValidY = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates Spearman's Ï for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesX, TSeries seriesY, int period = 20, Spearman? indicator = null)
    {
        if (seriesX.Count != seriesY.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        indicator ??= new Spearman(period);
        var result = new TSeries(seriesX.Count);

        var timesX = seriesX.Times;
        var valuesX = seriesX.Values;
        var valuesY = seriesY.Values;

        for (int i = 0; i < seriesX.Count; i++)
        {
            var tvalX = new TValue(timesX[i], valuesX[i]);
            var tvalY = new TValue(timesX[i], valuesY[i]);
            result.Add(indicator.Update(tvalX, tvalY, isNew: true));
        }

        return result;
    }

    /// <summary>
    /// Static batch calculation for span-based processing with NaN sanitization.
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

        var indicator = new Spearman(period);
        double lastValidX = 0;
        double lastValidY = 0;

        for (int i = 0; i < seriesX.Length; i++)
        {
            double x = seriesX[i];
            double y = seriesY[i];

            if (double.IsFinite(x))
            {
                lastValidX = x;
            }
            else
            {
                x = lastValidX;
            }

            if (double.IsFinite(y))
            {
                lastValidY = y;
            }
            else
            {
                y = lastValidY;
            }

            var result = indicator.Update(x, y, isNew: true);
            output[i] = result.Value;
        }
    }

    /// <summary>
    /// Calculates Spearman's ρ for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Spearman Indicator) Calculate(TSeries seriesX, TSeries seriesY, int period = 20)
    {
        var indicator = new Spearman(period);
        TSeries results = Batch(seriesX, seriesY, period, indicator);
        return (results, indicator);
    }
}
