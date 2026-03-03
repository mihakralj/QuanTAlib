using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Kendall Tau-a Rank Correlation Coefficient, which measures the ordinal
/// association between two series by counting concordant and discordant pairs.
/// </summary>
/// <remarks>
/// Kendall Tau-a Formula:
/// <c>τ = (C - D) / (n × (n - 1) / 2)</c>,
/// where <c>C</c> = concordant pairs, <c>D</c> = discordant pairs, <c>n</c> = window size.
///
/// A concordant pair (i,j) has both x_i &gt; x_j and y_i &gt; y_j (or both less).
/// A discordant pair has opposite ordering. Tied pairs contribute zero.
/// Output ranges from -1 (perfect disagreement) to +1 (perfect agreement).
///
/// This implementation recalculates pairwise comparisons from circular buffers each update.
/// The algorithm is O(n²) per update; no running-sum shortcut exists for rank statistics.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Kendall.md">Detailed documentation</seealso>
/// <seealso href="kendall.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Kendall : AbstractBase
{
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    private double _lastValidX, _lastValidY;

    private const double Epsilon = 1e-10;

    public override bool IsHot => _bufferX.Count >= 2;

    /// <summary>
    /// Creates a new Kendall Tau-a indicator.
    /// </summary>
    /// <param name="period">Lookback period for calculation (must be &gt; 1)</param>
    public Kendall(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);

        Name = $"Kendall({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Updates the Kendall indicator with new values from both series.
    /// </summary>
    /// <param name="seriesX">First series value</param>
    /// <param name="seriesY">Second series value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>Kendall Tau-a coefficient (-1 to +1)</returns>
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

        double tau = CalculateTau();

        Last = new TValue(seriesX.Time, tau);
        PubEvent(Last);
        return Last;
    }

    /// <summary>
    /// Updates with raw double values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double seriesX, double seriesY, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, seriesX), new TValue(DateTime.UtcNow, seriesY), isNew);
    }
    /// <remarks>Not supported for dual-input indicator. Use Update(seriesX, seriesY) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Kendall requires two inputs (seriesX and seriesY). Use Update(seriesX, seriesY).");
    }
    /// <remarks>Not supported for dual-input indicator. Use Batch(seriesX, seriesY, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Kendall requires two inputs. Use Batch(seriesX, seriesY, period).");
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
    private double CalculateTau()
    {
        int n = _bufferX.Count;
        if (n < 2)
        {
            return double.NaN;
        }

        int concordant = 0;
        int discordant = 0;

        for (int i = 0; i < n - 1; i++)
        {
            double xi = _bufferX[i];
            double yi = _bufferY[i];

            for (int j = i + 1; j < n; j++)
            {
                double diffX = xi - _bufferX[j];
                double diffY = yi - _bufferY[j];
                double product = diffX * diffY;

                if (product > 0)
                {
                    concordant++;
                }
                else if (product < 0)
                {
                    discordant++;
                }
                // product == 0 means tie — contributes nothing to Tau-a
            }
        }

        double denominator = (double)n * (n - 1) * 0.5;
        if (denominator < Epsilon)
        {
            return double.NaN;
        }

        return (concordant - discordant) / denominator;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Kendall requires two inputs.");
    }

    public override void Reset()
    {
        _bufferX.Clear();
        _bufferY.Clear();

        _lastValidX = 0;
        _lastValidY = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates Kendall Tau-a for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesX, TSeries seriesY, int period = 20, Kendall? indicator = null)
    {
        if (seriesX.Count != seriesY.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        indicator ??= new Kendall(period);
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

        var indicator = new Kendall(period);
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

    public static (TSeries Results, Kendall Indicator) Calculate(TSeries seriesX, TSeries seriesY, int period = 20)
    {
        var indicator = new Kendall(period);
        TSeries results = Batch(seriesX, seriesY, period, indicator);
        return (results, indicator);
    }
}
