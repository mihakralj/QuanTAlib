// IFFT: Inverse FFT Spectral Low-Pass Filter
// Reconstructs a filtered price signal by summing the DC component and
// the first H harmonics of the Hanning-windowed DFT. Output overlays on price.
// More harmonics → less smoothing; fewer harmonics → smoother output.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// IFFT: Inverse FFT Spectral Low-Pass Filter
/// Reconstructs a filtered price value from the DC component plus
/// the first numHarmonics frequency bins of the Hanning-windowed DFT.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output: reconstructed price (spectral low-pass filtered), overlays on price chart
/// - windowSize must be 32, 64, or 128
/// - numHarmonics clamped to [1, windowSize/2]
/// - WarmupPeriod = windowSize bars
/// - No allocation in Update (RingBuffer + precomputed Hanning weights)
/// - Increasing harmonics increases detail (less smoothing)
/// </remarks>
[SkipLocalsInit]
public sealed class Ifft : AbstractBase
{
    private readonly int _windowSize;
    private readonly int _numHarmonics;
    private readonly double _twoPiOverN;
    private readonly double _invN;
    private readonly double[] _hanning;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _windowSize;

    /// <summary>
    /// Initializes a new Ifft indicator.
    /// </summary>
    /// <param name="windowSize">DFT window size in bars. Must be 32, 64, or 128. Default 64.</param>
    /// <param name="numHarmonics">Number of harmonics to reconstruct. Must be >= 1. Default 5.</param>
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
        _twoPiOverN = 2.0 * Math.PI / windowSize;
        _invN = 1.0 / windowSize;

        // Precompute Hanning window: w[n] = 0.5 - 0.5*cos(2π*n/N), n=0..N-1
        _hanning = new double[windowSize];
        for (int n = 0; n < windowSize; n++)
        {
            _hanning[n] = 0.5 - 0.5 * Math.Cos(_twoPiOverN * n);
        }

        _buffer = new RingBuffer(windowSize);
        Name = $"Ifft({windowSize},{numHarmonics})";
        WarmupPeriod = windowSize;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Ifft indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="windowSize">DFT window size. Must be 32, 64, or 128. Default 64.</param>
    /// <param name="numHarmonics">Number of harmonics to reconstruct. Must be >= 1. Default 5.</param>
    public Ifft(ITValuePublisher source, int windowSize = 64, int numHarmonics = 5)
        : this(windowSize, numHarmonics)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeIfft()
    {
        var span = _buffer.GetSpan();
        int n = _windowSize;

        // DC component (k=0): sum of windowed values / N
        double dcRe = 0.0;
        for (int idx = 0; idx < n; idx++)
        {
            // span[0]=oldest, span[n-1]=newest
            // dftN=0→newest, dftN=n-1→oldest → span index = n-1-dftN
            double val = span[n - 1 - idx];
            dcRe = Math.FusedMultiplyAdd(val, _hanning[idx], dcRe);
        }

        double result = dcRe * _invN;

        // Harmonics k=1..H: add 2*re/N at time n=0 (reconstruction at current bar)
        for (int k = 1; k <= _numHarmonics; k++)
        {
            double omegaK = _twoPiOverN * k;
            double re = 0.0;

            for (int idx = 0; idx < n; idx++)
            {
                double val = span[n - 1 - idx];
                double xw = val * _hanning[idx];
                double angle = omegaK * idx;
                re = Math.FusedMultiplyAdd(xw, Math.Cos(angle), re);
            }

            result = Math.FusedMultiplyAdd(2.0 * _invN, re, result);
        }

        return result;
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
    /// Computes IFFT reconstruction over a span of values using a sliding Hanning-windowed DFT.
    /// Uses stackalloc for Hanning weights when windowSize &lt;= 64, otherwise ArrayPool.
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
        double twoPiOverN = 2.0 * Math.PI / windowSize;
        double invN = 1.0 / windowSize;
        double lastValid = 0.0;

        const int StackallocThreshold = 64;
        double[]? rentedW = null;
        scoped Span<double> hanning;

        if (windowSize <= StackallocThreshold)
        {
            hanning = stackalloc double[windowSize];
        }
        else
        {
            rentedW = ArrayPool<double>.Shared.Rent(windowSize);
            hanning = rentedW.AsSpan(0, windowSize);
        }

        try
        {
            for (int n = 0; n < windowSize; n++)
            {
                hanning[n] = 0.5 - 0.5 * Math.Cos(twoPiOverN * n);
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

                // DC component
                double dcRe = 0.0;
                for (int dftN = 0; dftN < windowSize; dftN++)
                {
                    double v = src[i - dftN];
                    if (!double.IsFinite(v))
                    {
                        v = lastValid;
                    }

                    dcRe = Math.FusedMultiplyAdd(v, hanning[dftN], dcRe);
                }

                double result = dcRe * invN;

                // Harmonics
                for (int k = 1; k <= clampedHarmonics; k++)
                {
                    double omegaK = twoPiOverN * k;
                    double re = 0.0;

                    for (int dftN = 0; dftN < windowSize; dftN++)
                    {
                        double v = src[i - dftN];
                        if (!double.IsFinite(v))
                        {
                            v = lastValid;
                        }

                        double xw = v * hanning[dftN];
                        re = Math.FusedMultiplyAdd(xw, Math.Cos(omegaK * dftN), re);
                    }

                    result = Math.FusedMultiplyAdd(2.0 * invN, re, result);
                }

                lastValid = result;
                output[i] = result;
            }
        }
        finally
        {
            if (rentedW != null)
            {
                ArrayPool<double>.Shared.Return(rentedW);
            }
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
