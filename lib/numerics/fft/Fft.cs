// FFT: Fast Fourier Transform — Dominant Cycle Detector
// Estimates the dominant cycle period in bars using a DFT on a windowed price buffer.
// Algorithm: Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013.
// Hanning-windowed DFT across bins [minBin..maxBin], with parabolic interpolation
// for sub-bin period estimation. Output: dominant cycle period in bars (clamped).

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FFT: Fast Fourier Transform Dominant Cycle Detector
/// Computes the dominant cycle period using a Hanning-windowed DFT
/// over a rolling price buffer, with parabolic interpolation refinement.
/// </summary>
/// <remarks>
/// Key properties:
/// - Output: dominant cycle period in bars, clamped to [minPeriod, maxPeriod]
/// - windowSize must be 32, 64, or 128
/// - WarmupPeriod = windowSize bars
/// - No allocation in Update (RingBuffer + precomputed Hanning weights)
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
    private readonly double _twoPiOverN;
    private readonly double[] _hanning;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);
    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _windowSize;

    /// <summary>
    /// Initializes a new Fft indicator.
    /// </summary>
    /// <param name="windowSize">DFT window size in bars. Must be 32, 64, or 128. Default 64.</param>
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
        _twoPiOverN = 2.0 * Math.PI / windowSize;

        // bin k corresponds to period N/k; k=minBin → period=N/minBin=maxPeriod, k=maxBin → period=N/maxBin=minPeriod
        _minBin = Math.Max(1, windowSize / maxPeriod);
        _maxBin = Math.Min(windowSize / 2, windowSize / minPeriod);

        // Precompute Hanning window: w[n] = 0.5 - 0.5*cos(2π*n/N), n=0..N-1
        _hanning = new double[windowSize];
        for (int n = 0; n < windowSize; n++)
        {
            _hanning[n] = 0.5 - 0.5 * Math.Cos(_twoPiOverN * n);
        }

        _buffer = new RingBuffer(windowSize);
        Name = $"Fft({windowSize},{minPeriod},{maxPeriod})";
        WarmupPeriod = windowSize;
        _state = new State((minPeriod + maxPeriod) * 0.5);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Fft indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="windowSize">DFT window size. Must be 32, 64, or 128. Default 64.</param>
    /// <param name="minPeriod">Minimum detectable period. Must be >= 2. Default 4.</param>
    /// <param name="maxPeriod">Maximum detectable period. Must be &lt;= windowSize/2. Default 32.</param>
    public Fft(ITValuePublisher source, int windowSize = 64, int minPeriod = 4, int maxPeriod = 32)
        : this(windowSize, minPeriod, maxPeriod)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeDominantPeriod()
    {
        var span = _buffer.GetSpan();
        int n = _windowSize;
        double maxMag = 0.0;
        int peakBin = _minBin;
        double magBefore = 0.0;
        double magAtPeak = 0.0;
        double magAfter = 0.0;

        for (int k = _minBin; k <= _maxBin; k++)
        {
            double omegaK = _twoPiOverN * k;
            double re = 0.0;
            double im = 0.0;

            for (int idx = 0; idx < n; idx++)
            {
                // span[0]=oldest, span[n-1]=newest
                // n=0 in DFT = current (newest): map DFT-n to span index (n-1-dftN)
                // span[n-1-dftN]: dftN=0 → span[n-1] (newest), dftN=n-1 → span[0] (oldest)
                double val = span[n - 1 - idx];
                double xw = val * _hanning[idx];
                double angle = omegaK * idx;
                double cosA = Math.Cos(angle);
                double sinA = Math.Sin(angle);
                re = Math.FusedMultiplyAdd(xw, cosA, re);
                im = Math.FusedMultiplyAdd(xw, -sinA, im);
            }

            double mag = Math.FusedMultiplyAdd(re, re, im * im);

            if (mag > maxMag)
            {
                magBefore = magAtPeak;
                magAfter = 0.0;
                maxMag = mag;
                magAtPeak = mag;
                peakBin = k;
            }
            else if (peakBin > 0 && magAfter == 0.0)
            {
                magAfter = mag;
            }
        }

        // Parabolic interpolation for sub-bin refinement
        double denom = magBefore + 2.0 * maxMag + magAfter;
        double shift = (denom > 0.0) ? (magBefore - magAfter) / denom : 0.0;
        double dominantPeriod = (double)_windowSize / (peakBin + shift);

        // Clamp to [minPeriod, maxPeriod]
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
    /// <remarks>
    /// Synthetic timestamps are generated by subtracting <c>step × source.Length</c>
    /// from <see cref="DateTime.UtcNow"/>. For deterministic or replay-safe pipelines
    /// use <see cref="Update(TValue, bool)"/> directly with explicit timestamps.
    /// </remarks>
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
    /// Computes dominant cycle period over a span of values using a sliding Hanning-windowed DFT.
    /// Uses stackalloc for Hanning weights when windowSize &lt;= 64, otherwise ArrayPool.
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

        double twoPiOverN = 2.0 * Math.PI / windowSize;
        int minBin = Math.Max(1, windowSize / maxPeriod);
        int maxBin = Math.Min(windowSize / 2, windowSize / minPeriod);
        double defaultPeriod = (minPeriod + maxPeriod) * 0.5;
        double lastValid = defaultPeriod;

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

                double maxMag = 0.0;
                int peakBin = minBin;
                double magBefore = 0.0;
                double magAtPeak = 0.0;
                double magAfter = 0.0;

                for (int k = minBin; k <= maxBin; k++)
                {
                    double omegaK = twoPiOverN * k;
                    double re = 0.0;
                    double im = 0.0;

                    for (int dftN = 0; dftN < windowSize; dftN++)
                    {
                        // dftN=0 → newest (src[i]), dftN=windowSize-1 → oldest (src[start])
                        double v = src[i - dftN];
                        if (!double.IsFinite(v))
                        {
                            v = lastValid;
                        }

                        double xw = v * hanning[dftN];
                        double angle = omegaK * dftN;
                        re = Math.FusedMultiplyAdd(xw, Math.Cos(angle), re);
                        im = Math.FusedMultiplyAdd(xw, -Math.Sin(angle), im);
                    }

                    double mag = Math.FusedMultiplyAdd(re, re, im * im);

                    if (mag > maxMag)
                    {
                        magBefore = magAtPeak;
                        magAfter = 0.0;
                        maxMag = mag;
                        magAtPeak = mag;
                        peakBin = k;
                    }
                    else if (peakBin > 0 && magAfter == 0.0)
                    {
                        magAfter = mag;
                    }
                }

                double denom = magBefore + 2.0 * maxMag + magAfter;
                double shift = (denom > 0.0) ? (magBefore - magAfter) / denom : 0.0;
                double dominant = (double)windowSize / (peakBin + shift);
                double clamped = Math.Clamp(dominant, minPeriod, maxPeriod);
                lastValid = clamped;
                output[i] = clamped;
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
