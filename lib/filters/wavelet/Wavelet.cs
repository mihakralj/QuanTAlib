using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WAVELET: À Trous Wavelet Denoising Filter
/// A non-decimated (stationary) wavelet transform using the Haar basis with soft
/// thresholding. Decomposes the signal into approximation and detail coefficients
/// at multiple scales, applies soft thresholding to remove noise, then reconstructs.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/wavelet.md
///
/// Key properties:
///   - À trous ("with holes") decomposition: no downsampling, output length = input length
///   - Haar wavelet: c_j = (c_{j-1} + c_{j-1}[2^(j-1)]) / 2 at each level j
///   - Detail coefficients: d_j = c_{j-1} - c_j
///   - Noise estimate: MAD of level-1 details / 0.6745 (robust Gaussian sigma)
///   - Universal threshold: T = sigma * sqrt(2 * ln(N)) * threshMult
///   - Soft thresholding: sign(d) * max(0, |d| - T)
///   - Reconstruction: coarsest approximation + sum of thresholded details
///   - Overlay indicator (price-following)
///   - O(levels) decomposition + O(2^levels) MAD per bar
///
/// Complexity: O(2^levels) per bar (dominated by MAD estimation)
/// </remarks>
[SkipLocalsInit]
public sealed class Wavelet : AbstractBase
{
    private const int MaxLevels = 8;
    private const double MadScale = 0.6745; // MAD-to-sigma for Gaussian

    private readonly int _levels;
    private readonly double _threshMult;
    private readonly int _madLen;        // 2^levels
    private readonly double _sqrtLog;    // sqrt(2 * ln(2^levels))
    private readonly RingBuffer _buffer;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double LastValid;
        public int Count;
    }

    private State _state;
    private State _p_state;

    /// <summary>Number of wavelet decomposition levels.</summary>
    public int Levels => _levels;

    /// <summary>Threshold multiplier for soft thresholding.</summary>
    public double ThreshMult => _threshMult;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count >= _madLen;

    public Wavelet(int levels = 4, double threshMult = 1.0)
    {
        if (levels < 1 || levels > MaxLevels)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), "Levels must be between 1 and 8.");
        }

        if (threshMult < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshMult), "Threshold multiplier must be >= 0.");
        }

        _levels = levels;
        _threshMult = threshMult;
        _madLen = 1 << levels;         // 2^levels
        _sqrtLog = Math.Sqrt(2.0 * Math.Log(_madLen));

        Name = $"Wavelet({levels},{threshMult:F1})";
        WarmupPeriod = _madLen;

        _buffer = new RingBuffer(_madLen + 1);
        _state.LastValid = double.NaN;
    }

    public Wavelet(ITValuePublisher source, int levels = 4, double threshMult = 1.0)
        : this(levels, threshMult)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        Batch(values, results, _levels, _threshMult);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Resync internal state by replaying
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;

        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        var s = _state;

        // Handle bad data — last-valid substitution
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = val;
        }

        // Input buffer: Add for new bars, UpdateNewest for corrections
        if (isNew)
        {
            _buffer.Add(val);
        }
        else
        {
            _buffer.UpdateNewest(val);
        }

        double result;

        if (_buffer.Count < 2)
        {
            result = val;
        }
        else
        {
            result = ComputeWavelet();
        }

        if (isNew)
        {
            s.Count++;
        }

        _state = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeWavelet()
    {
        int count = _buffer.Count;

        // --- À trous decomposition ---
        // Level 1: step = 1
        double c0 = _buffer[^1];  // newest
        double c1 = (c0 + GetBufferValue(1)) * 0.5;
        double d1 = c0 - c1;

        double c_prev = c1;
        double coarse = c1;

        // Accumulate details: d2..d_levels
        // skipcq: CS-W1082 - stackalloc safe: MaxLevels is 8, 8 doubles = 64 bytes
        Span<double> details = stackalloc double[MaxLevels];
        details[0] = d1;

        for (int lev = 2; lev <= _levels; lev++)
        {
            int step = 1 << (lev - 1); // 2^(lev-1)
            double delayed = GetBufferValueByStep(c_prev, step, count);
            double c_new = (c_prev + delayed) * 0.5;
            details[lev - 1] = c_prev - c_new;
            c_prev = c_new;
            coarse = c_new;
        }

        // --- MAD-based noise estimate from level-1 details ---
        // Compute mean|d1| over min(madLen, available) bars
        int madCount = Math.Min(_madLen, count);
        double sumAbsD1 = Math.Abs(d1);

        // For level-1 details at previous positions, recompute from buffer
        for (int i = 1; i < madCount; i++)
        {
            double ci = GetBufferValue(i);
            double ci1 = GetBufferValue(i + 1);
            double localC1 = (ci + ci1) * 0.5;
            double localD1 = ci - localC1;
            sumAbsD1 += Math.Abs(localD1);
        }

        double mad = sumAbsD1 / madCount;
        double sigma = mad / MadScale;
        double threshold = sigma * _sqrtLog * _threshMult;

        // --- Soft thresholding ---
        double reconstruction = coarse;
        for (int lev = 0; lev < _levels; lev++)
        {
            reconstruction += SoftThreshold(details[lev], threshold);
        }

        return reconstruction;
    }

    /// <summary>Gets buffer value at offset from newest (0 = newest, 1 = one bar back, etc.).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetBufferValue(int offset)
    {
        if (offset >= _buffer.Count)
        {
            return _buffer[^1]; // replicate newest if not enough history
        }
        return _buffer[^(offset + 1)];
    }

    /// <summary>
    /// Gets the delayed value for à trous decomposition.
    /// At level j, we need c_{j-1}[step] where step = 2^(j-1).
    /// Since we don't store intermediate approximation arrays, we approximate
    /// by reading the buffer at the appropriate offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetBufferValueByStep(double current, int step, int count)
    {
        // For the à trous algorithm, we need the low-pass output at a delayed position.
        // Since we only have the original signal buffer, we approximate by reading
        // the raw buffer at the step offset, which is valid for level-1 input.
        // For higher levels, this is an approximation that works well in practice.
        if (step >= count)
        {
            return current;
        }
        return _buffer[^(step + 1)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SoftThreshold(double d, double thresh)
    {
        double absD = Math.Abs(d);
        return absD > thresh ? Math.CopySign(absD - thresh, d) : 0.0;
    }

    public static TSeries Batch(TSeries source, int levels = 4, double threshMult = 1.0)
    {
        var indicator = new Wavelet(levels, threshMult);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int levels = 4, double threshMult = 1.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        if (levels < 1 || levels > MaxLevels)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), "Levels must be between 1 and 8.");
        }

        if (threshMult < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshMult), "Threshold multiplier must be >= 0.");
        }

        int madLen = 1 << levels;
        double sqrtLog = Math.Sqrt(2.0 * Math.Log(madLen));
        int bufSize = madLen + 1;
        var ring = new RingBuffer(bufSize);
        double lastValid = 0;

        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid))
            {
                lastValid = 0;
            }
        }

        // skipcq: CS-W1082 - stackalloc safe: MaxLevels is 8
        Span<double> details = stackalloc double[MaxLevels];

        for (int n = 0; n < source.Length; n++)
        {
            double val = source[n];
            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            ring.Add(val, true);

            if (ring.Count < 2)
            {
                output[n] = val;
                continue;
            }

            int count = ring.Count;

            // --- À trous decomposition ---
            double c0 = ring[^1];
            double c1Delayed = count > 1 ? ring[^2] : c0;
            double c1 = (c0 + c1Delayed) * 0.5;
            double d1 = c0 - c1;

            double c_prev = c1;
            double coarse = c1;
            details[0] = d1;

            for (int lev = 2; lev <= levels; lev++)
            {
                int step = 1 << (lev - 1);
                double delayed = step < count ? ring[^(step + 1)] : c_prev;
                double c_new = (c_prev + delayed) * 0.5;
                details[lev - 1] = c_prev - c_new;
                c_prev = c_new;
                coarse = c_new;
            }

            // --- MAD noise estimate ---
            int madCount = Math.Min(madLen, count);
            double sumAbsD1 = Math.Abs(d1);

            for (int i = 1; i < madCount; i++)
            {
                double ci = i < count ? ring[^(i + 1)] : ring[^1];
                double ci1 = (i + 1) < count ? ring[^(i + 2)] : ci;
                double localC1 = (ci + ci1) * 0.5;
                double localD1 = ci - localC1;
                sumAbsD1 += Math.Abs(localD1);
            }

            double mad = sumAbsD1 / madCount;
            double sigma = mad / MadScale;
            double threshold = sigma * sqrtLog * threshMult;

            // --- Reconstruction with soft thresholding ---
            double result = coarse;
            for (int lev = 0; lev < levels; lev++)
            {
                result += SoftThreshold(details[lev], threshold);
            }

            output[n] = result;
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
        _p_state = default;
        _buffer.Clear();
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Wavelet Indicator) Calculate(TSeries source,
        int levels = 4, double threshMult = 1.0)
    {
        var indicator = new Wavelet(levels, threshMult);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
            _handler = null;
        }
        base.Dispose(disposing);
    }
}
