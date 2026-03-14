// CWT: Continuous Wavelet Transform
// Convolves a signal with a scaled Morlet wavelet to extract spectral energy
// at a specific frequency band (determined by the scale parameter).
// Algorithm: precomputed Morlet kernel × RingBuffer sliding window.
// Half-window K = round(3*scale) — captures 99.7% of Gaussian envelope.
// Output: |W(t,s)| = sqrt(Re² + Im²) / sqrt(scale).

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CWT: Continuous Wavelet Transform
/// Computes the Morlet CWT magnitude at a single scale, providing a
/// time-localized frequency decomposition of the input series.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output is the Morlet wavelet magnitude |W(t,s)| — non-negative
/// - Half-window K = round(3*scale); warmup = 2K+1 samples
/// - Normalization: 1/sqrt(s) preserves energy across scales
/// - Omega0 = 6.0 (default) satisfies the admissibility condition
/// - Scale-to-period: P ≈ 2π·s / ω0 (e.g., scale=10 → period ≈ 10.5 bars)
/// - No allocation in Update (RingBuffer + precomputed kernel)
/// </remarks>
[SkipLocalsInit]
public sealed class Cwt : AbstractBase
{
    private readonly int _windowSize;  // 2K+1 = 2*round(3*scale)+1
    private readonly double[] _kernelReal;
    private readonly double[] _kernelImag;
    private readonly double _normFactor; // 1/sqrt(scale)
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _windowSize;

    /// <summary>
    /// Initializes a new Cwt indicator.
    /// </summary>
    /// <param name="scale">Wavelet scale parameter (default 10.0). Controls the frequency band analyzed.
    /// Scale-to-period: P ≈ 2π·scale/omega0. Must be > 0.</param>
    /// <param name="omega0">Central frequency of the Morlet wavelet (default 6.0).
    /// Must be > 0. Higher values give better frequency resolution (at cost of time resolution).</param>
    public Cwt(double scale = 10.0, double omega0 = 6.0)
    {
        if (scale <= 0.0)
        {
            throw new ArgumentException("Scale must be > 0", nameof(scale));
        }

        if (omega0 <= 0.0)
        {
            throw new ArgumentException("Omega0 must be > 0", nameof(omega0));
        }

        int halfWindow = (int)Math.Round(3.0 * scale);
        _windowSize = (2 * halfWindow) + 1;
        _normFactor = 1.0 / Math.Sqrt(scale);

        // Precompute kernel: ψ(k/s) = exp(-k²/(2s²)) * (cos(ω₀k/s) - i·sin(ω₀k/s))
        // Kernel is centered, k runs from -halfWindow..+halfWindow
        // We store in order [0..windowSize-1] where index j maps to k = j - halfWindow
        _kernelReal = new double[_windowSize];
        _kernelImag = new double[_windowSize];
        PrecomputeKernel(_kernelReal, _kernelImag, halfWindow, scale, omega0);

        _buffer = new RingBuffer(_windowSize);
        Name = $"Cwt({scale:G},{omega0:G})";
        WarmupPeriod = _windowSize;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Cwt indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="scale">Wavelet scale parameter (default 10.0)</param>
    /// <param name="omega0">Central frequency of the Morlet wavelet (default 6.0)</param>
    public Cwt(ITValuePublisher source, double scale = 10.0, double omega0 = 6.0)
        : this(scale, omega0)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Precomputes the Morlet wavelet kernel weights for the given scale.
    /// kernelReal[j] = exp(-t²/2) * cos(ω₀t),  t = (j - halfWindow) / scale
    /// kernelImag[j] = exp(-t²/2) * sin(ω₀t),  t = (j - halfWindow) / scale
    /// The kernel is complex-conjugate: the CWT convolution uses ψ*(k/s),
    /// so both real and imaginary components are needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrecomputeKernel(
        double[] kernelReal, double[] kernelImag,
        int halfWindow, double scale, double omega0)
    {
        int windowSize = (2 * halfWindow) + 1;
        double invScale = 1.0 / scale;
        for (int j = 0; j < windowSize; j++)
        {
            double t = (j - halfWindow) * invScale;
            double gauss = Math.Exp(Math.FusedMultiplyAdd(-0.5, t * t, 0.0));
            double phase = omega0 * t;
            // Complex conjugate of e^{iω₀t}: cos(ω₀t) - i·sin(ω₀t)
            kernelReal[j] = gauss * Math.Cos(phase);
            kernelImag[j] = gauss * Math.Sin(phase);
        }
    }

    /// <summary>
    /// Computes the dot product of the ring buffer contents with the precomputed kernel.
    /// Buffer[0] = oldest, Buffer[windowSize-1] = newest.
    /// kernel[0] corresponds to k = -halfWindow (earliest offset).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeCwt()
    {
        var span = _buffer.GetSpan();
        int n = span.Length;
        double re = 0.0;
        double im = 0.0;

        // span[0] is the oldest sample, aligns with kernel[windowSize-1-?]
        // The CWT formula: W(t,s) = (1/√s) Σ_k x[t-k]·ψ*(k/s)
        // where k = -halfWindow..+halfWindow, and x[t-k] is stored oldest-first.
        // span[j] = x[t - halfWindow + j]  (j=0: oldest = x[t-K], j=windowSize-1: newest = x[t+K])
        // ψ*(k/s) at k = -halfWindow+j corresponds to kernelReal/Imag[j].
        for (int j = 0; j < n; j++)
        {
            double v = span[j];
            re = Math.FusedMultiplyAdd(v, _kernelReal[j], re);
            im = Math.FusedMultiplyAdd(v, _kernelImag[j], im);
        }

        return Math.Sqrt(Math.FusedMultiplyAdd(re, re, im * im)) * _normFactor;
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
                result = ComputeCwt();
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

    public static TSeries Batch(TSeries source, double scale = 10.0, double omega0 = 6.0)
    {
        var indicator = new Cwt(scale, omega0);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates CWT magnitude over a span of values using a sliding Morlet convolution.
    /// Uses stackalloc for kernel when windowSize &lt;= 256, otherwise ArrayPool.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        double scale = 10.0, double omega0 = 6.0)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (scale <= 0.0)
        {
            throw new ArgumentException("Scale must be > 0", nameof(scale));
        }

        if (omega0 <= 0.0)
        {
            throw new ArgumentException("Omega0 must be > 0", nameof(omega0));
        }

        int halfWindow = (int)Math.Round(3.0 * scale);
        int windowSize = (2 * halfWindow) + 1;
        double normFactor = 1.0 / Math.Sqrt(scale);
        double lastValid = 0.0;

        const int StackallocThreshold = 128; // 128 doubles * 2 arrays = 2KB, safe margin

        double[]? rentedReal = null;
        double[]? rentedImag = null;
        scoped Span<double> kReal;
        scoped Span<double> kImag;

        if (windowSize <= StackallocThreshold)
        {
            kReal = stackalloc double[windowSize];
            kImag = stackalloc double[windowSize];
        }
        else
        {
            rentedReal = ArrayPool<double>.Shared.Rent(windowSize);
            rentedImag = ArrayPool<double>.Shared.Rent(windowSize);
            kReal = rentedReal.AsSpan(0, windowSize);
            kImag = rentedImag.AsSpan(0, windowSize);
        }

        try
        {
            // Precompute kernel
            double invScale = 1.0 / scale;
            for (int j = 0; j < windowSize; j++)
            {
                double t = (j - halfWindow) * invScale;
                double gauss = Math.Exp(Math.FusedMultiplyAdd(-0.5, t * t, 0.0));
                double phase = omega0 * t;
                kReal[j] = gauss * Math.Cos(phase);
                kImag[j] = gauss * Math.Sin(phase);
            }

            // Sliding convolution
            for (int i = 0; i < source.Length; i++)
            {
                double val = source[i];
                if (!double.IsFinite(val))
                {
                    output[i] = lastValid;
                    continue;
                }

                // We need windowSize samples ending at i (inclusive).
                // If i < windowSize-1, the buffer is not full yet → return lastValid (0).
                if (i < windowSize - 1)
                {
                    output[i] = lastValid;
                    continue;
                }

                int start = i - windowSize + 1;
                double re = 0.0;
                double im = 0.0;

                for (int j = 0; j < windowSize; j++)
                {
                    double v = source[start + j];
                    if (!double.IsFinite(v))
                    {
                        v = lastValid;
                    }

                    re = Math.FusedMultiplyAdd(v, kReal[j], re);
                    im = Math.FusedMultiplyAdd(v, kImag[j], im);
                }

                double magnitude = Math.Sqrt(Math.FusedMultiplyAdd(re, re, im * im)) * normFactor;
                lastValid = magnitude;
                output[i] = magnitude;
            }
        }
        finally
        {
            if (rentedReal != null)
            {
                ArrayPool<double>.Shared.Return(rentedReal);
            }

            if (rentedImag != null)
            {
                ArrayPool<double>.Shared.Return(rentedImag);
            }
        }
    }

    public static (TSeries Results, Cwt Indicator) Calculate(
        TSeries source, double scale = 10.0, double omega0 = 6.0)
    {
        var indicator = new Cwt(scale, omega0);
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
