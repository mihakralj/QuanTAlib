// IFFT: Inverse Fast Fourier Transform — Spectral Low-Pass Filter
// Reconstructs a filtered price signal by performing a forward radix-2 FFT,
// zeroing frequency bins above numHarmonics, then applying an inverse FFT.
// Output overlays on price. More harmonics → less smoothing; fewer → smoother.
//
// Algorithm: Cooley, J.W. & Tukey, J.W. (1965). Forward FFT → spectral
// truncation → inverse FFT reconstruction.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// IFFT: Inverse FFT Spectral Low-Pass Filter
/// Reconstructs a filtered price value by performing a forward radix-2 FFT,
/// zeroing bins above numHarmonics (preserving conjugate symmetry),
/// then applying an inverse FFT to reconstruct the time-domain signal.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output: reconstructed price (spectral low-pass filtered), overlays on price chart
/// - windowSize must be 32, 64, or 128 (power of 2 for radix-2)
/// - numHarmonics clamped to [1, windowSize/2]
/// - WarmupPeriod = windowSize bars
/// - True O(N log N) radix-2 FFT/IFFT with bit-reversal permutation
/// - Pre-allocated work arrays for zero-allocation streaming
/// - Increasing harmonics increases detail (less smoothing)
/// </remarks>
[SkipLocalsInit]
public sealed class Ifft : AbstractBase
{
    private readonly int _windowSize;
    private readonly int _numHarmonics;
    private readonly double _invN;
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
    /// Initializes a new Ifft indicator.
    /// </summary>
    /// <param name="windowSize">FFT window size in bars. Must be 32, 64, or 128. Default 64.</param>
    /// <param name="numHarmonics">Number of harmonics to preserve. Must be >= 1. Default 5.</param>
    public Ifft(int windowSize = 64, int numHarmonics = 5)
    {
        if (windowSize != 32 && windowSize != 64 && windowSize != 128)
        {
            throw new ArgumentException("windowSize must be 32, 64, or 128", nameof(windowSize));
        }

        if (numHarmonics < 1)
        {
            throw new ArgumentException("numHarmonics must be >= 1", nameof(numHarmonics));
        }

        _windowSize = windowSize;
        _numHarmonics = Math.Min(numHarmonics, windowSize / 2);
        int log2N = Log2(windowSize);
        _invN = 1.0 / windowSize;

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
        Name = $"Ifft({windowSize},{numHarmonics})";
        WarmupPeriod = windowSize;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Ifft indicator with source for event-based chaining.
    /// </summary>
    public Ifft(ITValuePublisher source, int windowSize = 64, int numHarmonics = 5)
        : this(windowSize, numHarmonics)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

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
    /// Applies spectral truncation: zeroes frequency bins outside the
    /// preserved range [0..numHarmonics] and their conjugate mirrors
    /// [N-numHarmonics..N-1], ensuring real-valued IFFT output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SpectralTruncate(double[] re, double[] im, int n, int numHarmonics)
    {
        // Keep bins 0..numHarmonics and N-numHarmonics..N-1 (conjugate symmetry)
        // Zero everything in between: bins numHarmonics+1..N-numHarmonics-1
        int startZero = numHarmonics + 1;
        int endZero = n - numHarmonics; // exclusive

        for (int k = startZero; k < endZero; k++)
        {
            re[k] = 0.0;
            im[k] = 0.0;
        }
    }

    /// <summary>
    /// Computes inverse FFT in-place using the conjugate method:
    /// IFFT(X) = (1/N) * conj(FFT(conj(X)))
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IfftInPlace(double[] re, double[] im, int n, int[] bitRev, double invN)
    {
        // Conjugate input
        for (int i = 0; i < n; i++)
        {
            im[i] = -im[i];
        }

        // Forward FFT
        Fft.FftInPlace(re, im, n, bitRev);

        // Conjugate output and scale by 1/N
        for (int i = 0; i < n; i++)
        {
            re[i] *= invN;
            im[i] = -im[i] * invN;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeIfft()
    {
        var span = _buffer.GetSpan();
        int n = _windowSize;

        // Fill work arrays: windowed data (oldest→newest), imag=0
        for (int i = 0; i < n; i++)
        {
            _workRe[i] = span[i] * _hanning[i];
            _workIm[i] = 0.0;
        }

        // Forward FFT
        Fft.FftInPlace(_workRe, _workIm, n, _bitRev);

        // Spectral truncation: zero bins above numHarmonics
        SpectralTruncate(_workRe, _workIm, n, _numHarmonics);

        // Inverse FFT to reconstruct filtered time-domain signal
        IfftInPlace(_workRe, _workIm, n, _bitRev, _invN);

        // Return the newest sample (last position in the array)
        return _workRe[n - 1];
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
                result = ComputeIfft();
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

    public static TSeries Batch(TSeries source, int windowSize = 64, int numHarmonics = 5)
    {
        var indicator = new Ifft(windowSize, numHarmonics);
        return indicator.Update(source);
    }

    /// <summary>
    /// Computes IFFT reconstruction over a span using sliding Hanning-windowed
    /// radix-2 FFT → spectral truncation → inverse FFT.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> src, Span<double> output,
        int windowSize = 64, int numHarmonics = 5)
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

        if (numHarmonics < 1)
        {
            throw new ArgumentException("numHarmonics must be >= 1", nameof(numHarmonics));
        }

        int clampedHarmonics = Math.Min(numHarmonics, windowSize / 2);
        int log2N = Log2(windowSize);
        double twoPiOverN = 2.0 * Math.PI / windowSize;
        double invN = 1.0 / windowSize;
        double lastValid = 0.0;

        // Precompute Hanning window
        const int StackallocThreshold = 64;
        double[]? rentedH = null;
        scoped Span<double> hanning;

        if (windowSize <= StackallocThreshold)
        {
            hanning = stackalloc double[windowSize];
        }
        else
        {
            rentedH = ArrayPool<double>.Shared.Rent(windowSize);
            hanning = rentedH.AsSpan(0, windowSize);
        }

        // FFT work arrays and bit-reversal table
        double[] workRe = ArrayPool<double>.Shared.Rent(windowSize);
        double[] workIm = ArrayPool<double>.Shared.Rent(windowSize);
        int[] bitRev = ArrayPool<int>.Shared.Rent(windowSize);

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

                // Fill work arrays with windowed data (oldest→newest)
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

                // Forward FFT
                Fft.FftInPlace(workRe, workIm, windowSize, bitRev);

                // Spectral truncation
                SpectralTruncate(workRe, workIm, windowSize, clampedHarmonics);

                // Inverse FFT
                IfftInPlace(workRe, workIm, windowSize, bitRev, invN);

                // Extract newest sample
                double result = workRe[windowSize - 1];
                lastValid = result;
                output[i] = result;
            }
        }
        finally
        {
            if (rentedH != null)
            {
                ArrayPool<double>.Shared.Return(rentedH);
            }
            ArrayPool<double>.Shared.Return(workRe);
            ArrayPool<double>.Shared.Return(workIm);
            ArrayPool<int>.Shared.Return(bitRev);
        }
    }

    public static (TSeries Results, Ifft Indicator) Calculate(
        TSeries source, int windowSize = 64, int numHarmonics = 5)
    {
        var indicator = new Ifft(windowSize, numHarmonics);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.0);
        _p_state = _state;
        Last = default;
    }
}
