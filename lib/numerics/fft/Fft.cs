// FFT: Fast Fourier Transform — Dominant Cycle Detector
// Estimates the dominant cycle period in bars using a radix-2 Cooley-Tukey FFT
// on a Hanning-windowed price buffer, with parabolic interpolation for sub-bin
// period estimation. Output: dominant cycle period in bars (clamped).
//
// Algorithm: Cooley, J.W. & Tukey, J.W. (1965). "An Algorithm for the Machine
// Calculation of Complex Fourier Series." Mathematics of Computation, 19(90).
// Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013 (application context).

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FFT: Fast Fourier Transform Dominant Cycle Detector
/// Computes the dominant cycle period using a Hanning-windowed radix-2
/// Cooley-Tukey FFT over a rolling price buffer, with parabolic interpolation.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output: dominant cycle period in bars, clamped to [minPeriod, maxPeriod]
/// - windowSize must be 32, 64, or 128 (power of 2 for radix-2)
/// - WarmupPeriod = windowSize bars
/// - True O(N log N) radix-2 FFT with bit-reversal permutation
/// - Pre-allocated work arrays for zero-allocation streaming
/// - Parabolic interpolation on peak bin for sub-bin accuracy
/// </remarks>
[SkipLocalsInit]
public sealed class Fft : AbstractBase
{
    private readonly int _windowSize;
    private readonly int _minPeriod;
    private readonly int _maxPeriod;
    private readonly int _minBin;
    private readonly int _maxBin;
    private readonly double[] _hanning;
    private readonly int[] _bitRev;
    private readonly double[] _workRe;
    private readonly double[] _workIm;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _windowSize;

    /// <summary>
    /// Initializes a new Fft indicator.
    /// </summary>
    /// <param name="windowSize">FFT window size in bars. Must be 32, 64, or 128. Default 64.</param>
    /// <param name="minPeriod">Minimum detectable cycle period. Must be >= 2. Default 4.</param>
    /// <param name="maxPeriod">Maximum detectable cycle period. Must be &lt;= windowSize/2. Default 32.</param>
    public Fft(int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
    {
        if (windowSize != 32 && windowSize != 64 && windowSize != 128)
        {
            throw new ArgumentException("windowSize must be 32, 64, or 128", nameof(windowSize));
        }

        if (minPeriod < 2)
        {
            throw new ArgumentException("minPeriod must be >= 2", nameof(minPeriod));
        }

        if (maxPeriod > windowSize / 2)
        {
            throw new ArgumentException($"maxPeriod must be <= windowSize/2 ({windowSize / 2})", nameof(maxPeriod));
        }

        _windowSize = windowSize;
        _minPeriod = minPeriod;
        _maxPeriod = maxPeriod;
        int log2N = Log2(windowSize);

        // bin k corresponds to period N/k
        _minBin = Math.Max(1, windowSize / maxPeriod);
        _maxBin = Math.Min(windowSize / 2, windowSize / minPeriod);

        // Precompute Hanning window: w[n] = 0.5 - 0.5*cos(2π*n/N)
        double twoPiOverN = 2.0 * Math.PI / windowSize;
        _hanning = new double[windowSize];
        for (int n = 0; n < windowSize; n++)
        {
            _hanning[n] = 0.5 - 0.5 * Math.Cos(twoPiOverN * n);
        }

        // Precompute bit-reversal permutation table
        _bitRev = new int[windowSize];
        for (int i = 0; i < windowSize; i++)
        {
            _bitRev[i] = BitReverse(i, log2N);
        }

        // Pre-allocate work arrays (zero allocation in hot path)
        _workRe = new double[windowSize];
        _workIm = new double[windowSize];

        _buffer = new RingBuffer(windowSize);
        Name = $"Fft({windowSize},{minPeriod},{maxPeriod})";
        WarmupPeriod = windowSize;
        _state = new State((minPeriod + maxPeriod) * 0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Fft indicator with source for event-based chaining.
    /// </summary>
    public Fft(ITValuePublisher source, int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
        : this(windowSize, minPeriod, maxPeriod)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Computes floor(log2(n)) for powers of 2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Log2(int n)
    {
        int p = 0;
        int x = n;
        while (x > 1)
        {
            x >>= 1;
            p++;
        }
        return p;
    }

    /// <summary>
    /// Reverses the bits of x using 'bits' bit-width.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitReverse(int x, int bits)
    {
        int r = 0;
        for (int i = 0; i < bits; i++)
        {
            r = (r << 1) | (x & 1);
            x >>= 1;
        }
        return r;
    }

    /// <summary>
    /// In-place iterative radix-2 Cooley-Tukey FFT.
    /// </summary>
    /// <param name="re">Real part array (modified in-place)</param>
    /// <param name="im">Imaginary part array (modified in-place)</param>
    /// <param name="n">Array length (must be power of 2)</param>
    /// <param name="bitRev">Pre-computed bit-reversal table</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FftInPlace(double[] re, double[] im, int n, int[] bitRev)
    {
        // Bit-reversal permutation
        for (int i = 0; i < n; i++)
        {
            int j = bitRev[i];
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        // Cooley-Tukey butterfly stages
        int len = 2;
        while (len <= n)
        {
            int half = len >> 1;
            double angStep = -2.0 * Math.PI / len;

            for (int start = 0; start < n; start += len)
            {
                for (int k = 0; k < half; k++)
                {
                    double angle = angStep * k;
                    double wr = Math.Cos(angle);
                    double wi = Math.Sin(angle);

                    int i0 = start + k;
                    int i1 = i0 + half;

                    double ur = re[i0];
                    double ui = im[i0];
                    double vr = re[i1];
                    double vi = im[i1];

                    // Twiddle: t = w * v
                    double tr = Math.FusedMultiplyAdd(vr, wr, -(vi * wi));
                    double ti = Math.FusedMultiplyAdd(vr, wi, vi * wr);

                    re[i0] = ur + tr;
                    im[i0] = ui + ti;
                    re[i1] = ur - tr;
                    im[i1] = ui - ti;
                }
            }

            len <<= 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeDominantPeriod()
    {
        var span = _buffer.GetSpan();
        int n = _windowSize;

        // Fill work arrays: windowed data (oldest→newest), imag=0
        for (int i = 0; i < n; i++)
        {
            _workRe[i] = span[i] * _hanning[i];
            _workIm[i] = 0.0;
        }

        // Radix-2 FFT in-place
        FftInPlace(_workRe, _workIm, n, _bitRev);

        // Find peak magnitude in [minBin..maxBin]
        double bestMag = -1.0;
        int bestK = _minBin;

        for (int k = _minBin; k <= _maxBin; k++)
        {
            double mag = Math.FusedMultiplyAdd(_workRe[k], _workRe[k], _workIm[k] * _workIm[k]);
            if (mag > bestMag)
            {
                bestMag = mag;
                bestK = k;
            }
        }

        // Neighbor magnitudes for parabolic interpolation
        double a, b, c;
        b = bestMag;

        if (bestK > _minBin)
        {
            a = Math.FusedMultiplyAdd(_workRe[bestK - 1], _workRe[bestK - 1],
                _workIm[bestK - 1] * _workIm[bestK - 1]);
        }
        else
        {
            a = b;
        }

        if (bestK < _maxBin)
        {
            c = Math.FusedMultiplyAdd(_workRe[bestK + 1], _workRe[bestK + 1],
                _workIm[bestK + 1] * _workIm[bestK + 1]);
        }
        else
        {
            c = b;
        }

        // Parabolic interpolation: shift = 0.5*(a-c)/(a - 2b + c)
        double denom = a - 2.0 * b + c;
        double shift = Math.Abs(denom) > 0.0 ? 0.5 * (a - c) / denom : 0.0;
        double dominantPeriod = (double)_windowSize / (bestK + shift);

        return Math.Clamp(dominantPeriod, _minPeriod, _maxPeriod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double value = input.Value;
        double result;

        if (double.IsFinite(value))
        {
            _buffer.Add(value, isNew);
            if (IsHot)
            {
                result = ComputeDominantPeriod();
                _state = new State(result);
            }
            else
            {
                result = _state.LastValid;
            }
        }
        else
        {
            result = _state.LastValid;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }

        return result;
    }

    /// <summary>
    /// Primes the indicator with historical values.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
    {
        var indicator = new Fft(windowSize, minPeriod, maxPeriod);
        return indicator.Update(source);
    }

    /// <summary>
    /// Computes dominant cycle period over a span using sliding Hanning-windowed radix-2 FFT.
    /// Uses stackalloc for work arrays when windowSize &lt;= 64, otherwise ArrayPool.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> src, Span<double> output,
        int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
    {
        if (src.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(src));
        }

        if (output.Length < src.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (windowSize != 32 && windowSize != 64 && windowSize != 128)
        {
            throw new ArgumentException("windowSize must be 32, 64, or 128", nameof(windowSize));
        }

        if (minPeriod < 2)
        {
            throw new ArgumentException("minPeriod must be >= 2", nameof(minPeriod));
        }

        if (maxPeriod > windowSize / 2)
        {
            throw new ArgumentException($"maxPeriod must be <= windowSize/2", nameof(maxPeriod));
        }

        int log2N = Log2(windowSize);
        double twoPiOverN = 2.0 * Math.PI / windowSize;
        int minBin = Math.Max(1, windowSize / maxPeriod);
        int maxBin = Math.Min(windowSize / 2, windowSize / minPeriod);
        double defaultPeriod = (minPeriod + maxPeriod) * 0.5;
        double lastValid = defaultPeriod;

        // Precompute Hanning window and bit-reversal table
        const int StackallocThreshold = 64;
        double[]? rentedH = null;
        double[]? rentedRe = null;
        double[]? rentedIm = null;
        int[]? rentedBr = null;

        scoped Span<double> hanning;
        double[] workRe;
        double[] workIm;
        int[] bitRev;

        if (windowSize <= StackallocThreshold)
        {
            hanning = stackalloc double[windowSize];
        }
        else
        {
            rentedH = ArrayPool<double>.Shared.Rent(windowSize);
            hanning = rentedH.AsSpan(0, windowSize);
        }

        // FFT work arrays (must be double[] for FftInPlace)
        rentedRe = ArrayPool<double>.Shared.Rent(windowSize);
        rentedIm = ArrayPool<double>.Shared.Rent(windowSize);
        rentedBr = ArrayPool<int>.Shared.Rent(windowSize);
        workRe = rentedRe;
        workIm = rentedIm;
        bitRev = rentedBr;

        try
        {
            for (int n = 0; n < windowSize; n++)
            {
                hanning[n] = 0.5 - 0.5 * Math.Cos(twoPiOverN * n);
                bitRev[n] = BitReverse(n, log2N);
            }

            for (int i = 0; i < src.Length; i++)
            {
                double val = src[i];
                if (!double.IsFinite(val))
                {
                    output[i] = lastValid;
                    continue;
                }

                if (i < windowSize - 1)
                {
                    output[i] = lastValid;
                    continue;
                }

                // Fill work arrays with windowed data
                for (int n = 0; n < windowSize; n++)
                {
                    double v = src[i - windowSize + 1 + n];
                    if (!double.IsFinite(v))
                    {
                        v = lastValid;
                    }
                    workRe[n] = v * hanning[n];
                    workIm[n] = 0.0;
                }

                // Radix-2 FFT
                FftInPlace(workRe, workIm, windowSize, bitRev);

                // Find peak magnitude
                double bestMag = -1.0;
                int bestK = minBin;

                for (int k = minBin; k <= maxBin; k++)
                {
                    double mag = Math.FusedMultiplyAdd(workRe[k], workRe[k], workIm[k] * workIm[k]);
                    if (mag > bestMag)
                    {
                        bestMag = mag;
                        bestK = k;
                    }
                }

                // Neighbor magnitudes for parabolic interpolation
                double a = bestK > minBin
                    ? Math.FusedMultiplyAdd(workRe[bestK - 1], workRe[bestK - 1],
                        workIm[bestK - 1] * workIm[bestK - 1])
                    : bestMag;

                double c = bestK < maxBin
                    ? Math.FusedMultiplyAdd(workRe[bestK + 1], workRe[bestK + 1],
                        workIm[bestK + 1] * workIm[bestK + 1])
                    : bestMag;

                double denom = a - 2.0 * bestMag + c;
                double shift = Math.Abs(denom) > 0.0 ? 0.5 * (a - c) / denom : 0.0;
                double dominant = (double)windowSize / (bestK + shift);
                double clamped = Math.Clamp(dominant, minPeriod, maxPeriod);
                lastValid = clamped;
                output[i] = clamped;
            }
        }
        finally
        {
            if (rentedH != null)
            {
                ArrayPool<double>.Shared.Return(rentedH);
            }
            ArrayPool<double>.Shared.Return(rentedRe);
            ArrayPool<double>.Shared.Return(rentedIm);
            ArrayPool<int>.Shared.Return(rentedBr);
        }
    }

    public static (TSeries Results, Fft Indicator) Calculate(
        TSeries source, int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
    {
        var indicator = new Fft(windowSize, minPeriod, maxPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State((_minPeriod + _maxPeriod) * 0.5);
        _p_state = _state;
        Last = default;
    }
}
