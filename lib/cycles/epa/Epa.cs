using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EPA: Ehlers Phasor Analysis — extracts cycle phase by computing Pearson correlation
/// of a price window against cosine (Real) and negative-sine (Imaginary) reference waves,
/// converting the resulting phasor to an angle with wraparound compensation and monotonic
/// constraint, then deriving cycle period and trend state from the angle rate-of-change.
/// </summary>
/// <remarks>
/// From John F. Ehlers, "Recurring Phase Of Cycle Analysis"
/// (Stocks &amp; Commodities, November 2022).
///
/// Algorithm:
/// 1. Dual Pearson correlation over sliding window of N bars:
///    Real = corr(price, cos(2πk/N)), Imag = corr(price, -sin(2πk/N))
/// 2. Phasor angle = 90° - atan(Imag/Real) with quadrant fix (if Real &lt; 0: angle -= 180°)
/// 3. Wraparound compensation: detects 360° boundary crossings
/// 4. Monotonic constraint with conditional exceptions: angle generally cannot go backwards
/// 5. DerivedPeriod = 360 / DeltaAngle (clamped to max 60)
/// 6. TrendState: 0 = cycling, +1 = trending long, -1 = trending short
///
/// Properties:
/// - O(period) per bar for dual correlation loops
/// - Precomputed cos/sin tables eliminate per-bar trig calls
/// - Real, Imag bounded [-1, +1] by Pearson construction
/// - Zero allocation in hot path (RingBuffer is pre-allocated)
/// </remarks>
[SkipLocalsInit]
public sealed class Epa : AbstractBase
{
    private const int DefaultPeriod = 28;
    private const double MaxDerivedPeriod = 60.0;
    private const double TrendThreshold = 6.0;
    private const double Rad2Deg = 180.0 / Math.PI;

    private readonly int _period;
    private readonly double[] _cosTable;
    private readonly double[] _negSinTable;
    private readonly RingBuffer _buf;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevAngle,
        double PrevDeltaAngle,
        double PrevDerivedPeriod,
        int Count,
        double LastValid);

    private State _s;
    private State _ps;

    /// <summary>Phasor angle in degrees, with wraparound compensation and monotonic constraint.</summary>
    public double Angle { get; private set; }

    /// <summary>Cycle period derived from angle rate-of-change. Clamped to [0, 60].</summary>
    public double DerivedPeriod { get; private set; }

    /// <summary>Trend state: +1 = trending long, -1 = trending short, 0 = cycling.</summary>
    public int TrendState { get; private set; }

    /// <inheritdoc />
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Epa indicator.
    /// </summary>
    /// <param name="period">Presumed dominant cycle wavelength. Must be &gt; 1. Default 28.</param>
    public Epa(int period = DefaultPeriod)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1.", nameof(period));
        }

        _period = period;

        // Precompute cos/sin lookup tables
        _cosTable = new double[period];
        _negSinTable = new double[period];
        double twoPiOverN = 2.0 * Math.PI / period;

        for (int k = 0; k < period; k++)
        {
            double a = twoPiOverN * k;
            _cosTable[k] = Math.Cos(a);
            _negSinTable[k] = -Math.Sin(a);
        }

        _buf = new(period);
        Name = $"Epa({period})";
        WarmupPeriod = period;
        _s = default;
        _ps = default;
    }

    /// <summary>
    /// Creates a new Epa indicator chained to a publisher source.
    /// </summary>
    public Epa(ITValuePublisher source, int period = DefaultPeriod) : this(period)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // State management: save/restore for bar correction
        if (isNew)
        {
            _ps = _s;
            _buf.Snapshot();
        }
        else
        {
            _s = _ps;
            _buf.Restore();
        }

        var s = _s;
        double price = input.Value;

        // NaN/Infinity guard
        if (!double.IsFinite(price))
        {
            price = s.LastValid;
        }
        else
        {
            s = s with { LastValid = price };
        }

        int count = isNew ? s.Count + 1 : s.Count;
        _buf.Add(price);

        int n = Math.Min(count, _period);
        double angle = 0;
        double derivedPeriod = s.PrevDerivedPeriod;
        int trendState = 0;

        if (n >= 2)
        {
            // Dual Pearson correlations
            double real = ComputeCorrelation(_buf, _cosTable, n);
            double imag = ComputeCorrelation(_buf, _negSinTable, n);

            // Step 3: Angle = 90 - atan(Imag/Real) with quadrant fix
            if (real != 0.0)
            {
                angle = 90.0 - (Math.Atan(imag / real) * Rad2Deg);
            }
            if (real < 0.0)
            {
                angle -= 180.0;
            }

            double prevAngle = s.PrevAngle;

            // Step 4: Wraparound compensation
            if (Math.Abs(angle) - Math.Abs(prevAngle - 360.0) < angle - prevAngle
                && prevAngle > 90.0 && angle < -90.0)
            {
                angle -= 360.0;
            }

            // Step 5: Angle cannot go backwards (with conditional exceptions)
            if (angle < prevAngle
                && ((prevAngle > -135.0 && prevAngle < 135.0)
                    || (angle < -90.0 && prevAngle < -90.0)))
            {
                angle = prevAngle;
            }

            // Step 6: DerivedPeriod from angle rate-of-change
            double deltaAngle = angle - prevAngle;
            if (deltaAngle <= 0.0)
            {
                deltaAngle = s.PrevDeltaAngle;
            }
            if (deltaAngle > 0.0)
            {
                derivedPeriod = 360.0 / deltaAngle;
            }
            if (derivedPeriod > MaxDerivedPeriod)
            {
                derivedPeriod = MaxDerivedPeriod;
            }

            // Step 7: Trend state
            trendState = 0;
            double angleChange = angle - prevAngle;
            if (angleChange <= TrendThreshold)
            {
                if (angle >= 90.0 || angle <= -90.0)
                {
                    trendState = 1;   // trending long
                }
                else if (angle > -90.0 && angle < 90.0)
                {
                    trendState = -1;  // trending short
                }
            }

            s = s with { PrevDeltaAngle = deltaAngle > 0 ? deltaAngle : s.PrevDeltaAngle };
        }

        Angle = angle;
        DerivedPeriod = derivedPeriod;
        TrendState = trendState;

        _s = new State(angle, s.PrevDeltaAngle, derivedPeriod, count, s.LastValid);

        Last = new TValue(input.Time, angle);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Processes a full TSeries, returning the Angle for each bar.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i]);
            vSpan[i] = result.Value;
        }
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <inheritdoc />
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Static batch: creates an Epa, processes source, returns output TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = DefaultPeriod)
    {
        var ind = new Epa(period);
        return ind.Update(source);
    }

    /// <summary>
    /// Static span-based batch: computes phasor angle into output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = DefaultPeriod)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Precompute trig tables
        const int StackallocThreshold = 256;
        double[]? rentedCos = null;
        double[]? rentedSin = null;
        scoped Span<double> cosTab;
        scoped Span<double> sinTab;

        if (period <= StackallocThreshold)
        {
            cosTab = stackalloc double[period];
            sinTab = stackalloc double[period];
        }
        else
        {
            rentedCos = ArrayPool<double>.Shared.Rent(period);
            rentedSin = ArrayPool<double>.Shared.Rent(period);
            cosTab = rentedCos.AsSpan(0, period);
            sinTab = rentedSin.AsSpan(0, period);
        }

        try
        {
            double twoPiOverN = 2.0 * Math.PI / period;
            for (int k = 0; k < period; k++)
            {
                double a = twoPiOverN * k;
                cosTab[k] = Math.Cos(a);
                sinTab[k] = -Math.Sin(a);
            }

            // Price ring buffer (manual circular)
            double[]? rentedBuf = null;
            scoped Span<double> priceBuf;
            if (period <= StackallocThreshold)
            {
                priceBuf = stackalloc double[period];
            }
            else
            {
                rentedBuf = ArrayPool<double>.Shared.Rent(period);
                priceBuf = rentedBuf.AsSpan(0, period);
            }

            try
            {
                priceBuf.Clear();
                int bufIdx = 0;
                int filled = 0;
                double lastValid = 0;
                double prevAngle = 0;
                double prevDeltaAngle = 0;
                double prevDerivedPeriod = 0;

                for (int i = 0; i < len; i++)
                {
                    double val = source[i];
                    if (!double.IsFinite(val))
                    {
                        val = lastValid;
                    }
                    else
                    {
                        lastValid = val;
                    }

                    priceBuf[bufIdx] = val;
                    bufIdx = (bufIdx + 1) % period;
                    if (filled < period)
                    {
                        filled++;
                    }

                    int n = filled;
                    double angle = 0;

                    if (n >= 2)
                    {
                        // Compute Real correlation (cosine)
                        double real = InlineCorrelation(priceBuf, cosTab, bufIdx, n, period);
                        double imag = InlineCorrelation(priceBuf, sinTab, bufIdx, n, period);

                        // Angle calculation
                        if (real != 0.0)
                        {
                            angle = 90.0 - (Math.Atan(imag / real) * Rad2Deg);
                        }
                        if (real < 0.0)
                        {
                            angle -= 180.0;
                        }

                        // Wraparound compensation
                        if (Math.Abs(angle) - Math.Abs(prevAngle - 360.0) < angle - prevAngle
                            && prevAngle > 90.0 && angle < -90.0)
                        {
                            angle -= 360.0;
                        }

                        // Monotonic constraint with exceptions
                        if (angle < prevAngle
                            && ((prevAngle > -135.0 && prevAngle < 135.0)
                                || (angle < -90.0 && prevAngle < -90.0)))
                        {
                            angle = prevAngle;
                        }

                        // DerivedPeriod
                        double deltaAngle = angle - prevAngle;
                        if (deltaAngle <= 0.0)
                        {
                            deltaAngle = prevDeltaAngle;
                        }
                        if (deltaAngle > 0.0)
                        {
                            prevDerivedPeriod = 360.0 / deltaAngle;
                            prevDeltaAngle = deltaAngle;
                        }
                        if (prevDerivedPeriod > MaxDerivedPeriod)
                        {
                            prevDerivedPeriod = MaxDerivedPeriod;
                        }
                    }

                    output[i] = angle;
                    prevAngle = angle;
                }
            }
            finally
            {
                if (rentedBuf != null)
                {
                    ArrayPool<double>.Shared.Return(rentedBuf);
                }
            }
        }
        finally
        {
            if (rentedCos != null)
            {
                ArrayPool<double>.Shared.Return(rentedCos);
            }
            if (rentedSin != null)
            {
                ArrayPool<double>.Shared.Return(rentedSin);
            }
        }
    }

    /// <summary>
    /// Static convenience method: returns (TSeries results, Epa indicator) for inspection.
    /// </summary>
    public static (TSeries Results, Epa Indicator) Calculate(TSeries source, int period = DefaultPeriod)
    {
        var ind = new Epa(period);
        var results = ind.Update(source);
        return (results, ind);
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _s = default;
        _ps = default;
        _buf.Clear();
        Last = default;
        Angle = 0;
        DerivedPeriod = 0;
        TrendState = 0;
    }

    /// <summary>
    /// Computes Pearson correlation between the most recent n values in RingBuffer
    /// and the first n entries of a reference wave table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeCorrelation(RingBuffer buf, double[] refTable, int n)
    {
        double sx = 0, sxx = 0, sxy = 0;
        double sy = 0, syy = 0;
        int newest = buf.Count - 1;

        for (int k = 0; k < n; k++)
        {
            double x = buf[newest - k];
            double y = refTable[k];
            sx += x;
            sxx += x * x;
            sxy += x * y;
            sy += y;
            syy += y * y;
        }

        double nd = n;
        double denomProd = ((nd * sxx) - (sx * sx)) * ((nd * syy) - (sy * sy));
        if (denomProd <= 0.0)
        {
            return 0.0;
        }

        double r = ((nd * sxy) - (sx * sy)) / Math.Sqrt(denomProd);
        return Math.Clamp(r, -1.0, 1.0);
    }

    /// <summary>
    /// Inline Pearson correlation for span-based batch (uses manual circular buffer).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double InlineCorrelation(
        Span<double> priceBuf, Span<double> refTab, int bufIdx, int n, int period)
    {
        double sx = 0, sxx = 0, sxy = 0;
        double sy = 0, syy = 0;

        for (int k = 0; k < n; k++)
        {
            int idx = (((bufIdx - 1 - k) % period) + period) % period;
            double x = priceBuf[idx];
            double y = refTab[k];
            sx += x;
            sxx += x * x;
            sxy += x * y;
            sy += y;
            syy += y * y;
        }

        double nd = n;
        double dp = ((nd * sxx) - (sx * sx)) * ((nd * syy) - (sy * sy));
        return dp > 0.0 ? Math.Clamp(((nd * sxy) - (sx * sy)) / Math.Sqrt(dp), -1.0, 1.0) : 0.0;
    }
}
