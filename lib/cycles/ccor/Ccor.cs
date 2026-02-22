using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CCOR: Ehlers Correlation Cycle — extracts cycle phase by computing Pearson correlation
/// of a price window against cosine (Real) and negative-sine (Imaginary) reference waves,
/// converting the resulting phasor to an angle with monotonic constraint and classifying
/// the market state as trending or cycling.
/// </summary>
/// <remarks>
/// From John F. Ehlers, "Correlation As A Cycle Indicator" (Stocks &amp; Commodities, June 2020).
///
/// Algorithm:
/// 1. Dual Pearson correlation over sliding window of N bars:
///    Real = corr(price, cos(2πk/N)), Imag = corr(price, -sin(2πk/N))
/// 2. Phasor angle = 90° + atan(Real/Imag) with quadrant fix (if Imag &gt; 0: angle -= 180°)
/// 3. Monotonic constraint: angle = max(angle, prev_angle) — prevents backward spin
/// 4. State detection: |Δangle| &lt; threshold → trending (+1 uptrend / -1 downtrend), else cycling (0)
///
/// Properties:
/// - O(period) per bar for dual correlation loops
/// - Precomputed cos/sin tables eliminate per-bar trig calls
/// - Real, Imag bounded [-1, +1] by Pearson construction
/// - Zero allocation in hot path (RingBuffer is pre-allocated)
/// </remarks>
[SkipLocalsInit]
public sealed class Ccor : AbstractBase
{
    private readonly int _period;
    private readonly double _threshold;
    private readonly double[] _cosTable;
    private readonly double[] _negSinTable;
    private readonly RingBuffer _buf;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double PrevAngle, int Count, double LastValid);

    private State _s;
    private State _ps;

    /// <summary>Pearson correlation of price with cosine reference wave. Range [-1, +1].</summary>
    public double Real { get; private set; }

    /// <summary>Pearson correlation of price with negative-sine reference wave. Range [-1, +1].</summary>
    public double Imag { get; private set; }

    /// <summary>Phasor angle (degrees), monotonically increasing.</summary>
    public double Angle { get; private set; }

    /// <summary>Market state: +1 = uptrend, -1 = downtrend, 0 = cycling.</summary>
    public int MarketState { get; private set; }

    /// <inheritdoc />
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ccor indicator.
    /// </summary>
    /// <param name="period">Presumed dominant cycle wavelength. Must be &gt; 0. Default 20.</param>
    /// <param name="threshold">Angle rate threshold (degrees) for state detection. Must be &gt; 0. Default 9.0.</param>
    public Ccor(int period = 20, double threshold = 9.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0.", nameof(period));
        }
        if (threshold <= 0.0)
        {
            throw new ArgumentException("Threshold must be greater than 0.", nameof(threshold));
        }

        _period = period;
        _threshold = threshold;

        // Precompute cos/sin lookup tables
        _cosTable = new double[period];
        _negSinTable = new double[period];
        double twoPiOverN = 2.0 * Math.PI / period;

        for (int k = 0; k < period; k++)
        {
            double angle = twoPiOverN * k;
            _cosTable[k] = Math.Cos(angle);
            _negSinTable[k] = -Math.Sin(angle);
        }

        _buf = new(period);
        Name = $"Ccor({period},{threshold:F1})";
        WarmupPeriod = period;
        _s = default;
        _ps = default;
    }

    /// <summary>
    /// Creates a new Ccor indicator chained to a publisher source.
    /// </summary>
    public Ccor(ITValuePublisher source, int period = 20, double threshold = 9.0) : this(period, threshold)
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

        // NaN/Infinity guard: substitute last valid value
        if (!double.IsFinite(price))
        {
            price = s.LastValid;
        }
        else
        {
            s = s with { LastValid = price };
        }

        // Increment bar count
        int count = isNew ? s.Count + 1 : s.Count;

        // Add price to ring buffer
        _buf.Add(price);

        // Compute dual Pearson correlations
        int n = Math.Min(count, _period);
        double realVal = 0, imagVal = 0;
        double angleVal = 0;
        int stateVal = 0;

        if (n >= 2)
        {
            realVal = ComputeCorrelation(_buf, _cosTable, n);
            imagVal = ComputeCorrelation(_buf, _negSinTable, n);

            // Phasor angle (degrees) with quadrant resolution
            if (imagVal != 0.0)
            {
                angleVal = 90.0 + Math.Atan(realVal / imagVal) * (180.0 / Math.PI);
            }
            if (imagVal > 0.0)
            {
                angleVal -= 180.0;
            }

            // Monotonic constraint: angle cannot decrease
            double savedPrev = s.PrevAngle;
            if (angleVal < savedPrev)
            {
                angleVal = savedPrev;
            }

            // Market state detection
            double angleChange = Math.Abs(angleVal - savedPrev);
            if (angleChange < _threshold && angleVal >= 0.0)
            {
                stateVal = 1;  // uptrend
            }
            else if (angleChange < _threshold && angleVal <= 0.0)
            {
                stateVal = -1; // downtrend
            }
            // else stateVal = 0 (cycling)
        }

        Real = realVal;
        Imag = imagVal;
        Angle = angleVal;
        MarketState = stateVal;

        _s = new State(angleVal, count, s.LastValid);

        Last = new TValue(input.Time, realVal);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Processes a full TSeries, returning the Real correlation for each bar.
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
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Static batch: creates a Ccor, processes source, returns output TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, double threshold = 9.0)
    {
        var ind = new Ccor(period, threshold);
        return ind.Update(source);
    }

    /// <summary>
    /// Static span-based batch: computes correlation cycle Real component into output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20, double threshold = 9.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0.", nameof(period));
        }
        if (threshold <= 0.0)
        {
            throw new ArgumentException("Threshold must be greater than 0.", nameof(threshold));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Precompute trig tables
        const int StackallocThreshold = 256;
        double[]? rentedCos = null;
        scoped Span<double> cosTab;

        if (period <= StackallocThreshold)
        {
            cosTab = stackalloc double[period];
        }
        else
        {
            rentedCos = ArrayPool<double>.Shared.Rent(period);
            cosTab = rentedCos.AsSpan(0, period);
        }

        try
        {
            double twoPiOverN = 2.0 * Math.PI / period;
            for (int k = 0; k < period; k++)
            {
                cosTab[k] = Math.Cos(twoPiOverN * k);
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
                    double realVal = 0;

                    if (n >= 2)
                    {
                        // Compute Real correlation (cosine)
                        double sx = 0, sxx = 0, sxy = 0;
                        double sy = 0, syy = 0;

                        for (int k = 0; k < n; k++)
                        {
                            int idx = ((bufIdx - 1 - k) % period + period) % period;
                            double x = priceBuf[idx];
                            double y = cosTab[k];
                            sx += x;
                            sxx += x * x;
                            sxy += x * y;
                            sy += y;
                            syy += y * y;
                        }

                        double nd = n;
                        double dp = (nd * sxx - sx * sx) * (nd * syy - sy * sy);
                        realVal = dp > 0.0 ? Math.Clamp((nd * sxy - sx * sy) / Math.Sqrt(dp), -1.0, 1.0) : 0.0;
                    }

                    output[i] = realVal;
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
        }
    }

    /// <summary>
    /// Static convenience method: returns (TSeries results, Ccor indicator) for inspection.
    /// </summary>
    public static (TSeries Results, Ccor Indicator) Calculate(TSeries source, int period = 20, double threshold = 9.0)
    {
        var ind = new Ccor(period, threshold);
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
        Real = 0;
        Imag = 0;
        Angle = 0;
        MarketState = 0;
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
        double denomProd = (nd * sxx - sx * sx) * (nd * syy - sy * sy);
        if (denomProd <= 0.0)
        {
            return 0.0;
        }

        double r = (nd * sxy - sx * sy) / Math.Sqrt(denomProd);
        return Math.Clamp(r, -1.0, 1.0);
    }
}
