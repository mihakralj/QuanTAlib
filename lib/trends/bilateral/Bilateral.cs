using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Bilateral Filter
/// </summary>
/// <remarks>
/// A non-linear, edge-preserving, and noise-reducing smoothing filter for images, adapted for time series.
/// It replaces the intensity of each pixel with a weighted average of intensity values from nearby pixels.
/// The weights depend not only on Euclidean distance of pixels, but also on the radiometric differences (e.g., range differences, such as color intensity, depth distance, etc.).
///
/// Calculation:
/// sigma_s = max(length * sigma_s_ratio, 1e-10)
/// sigma_r = max(stdev(src, length) * sigma_r_mult, 1e-10)
/// weight_spatial = exp(-(i^2) / (2 * sigma_s^2))
/// weight_range = exp(-(diff^2) / (2 * sigma_r^2))
/// weight = weight_spatial * weight_range
/// </remarks>
[SkipLocalsInit]
public sealed class Bilateral : AbstractBase
{
    private readonly int _period;
    private readonly double _sigmaSRatio;
    private readonly double _sigmaRMult;
    private readonly RingBuffer _buffer;
    private readonly double[] _spatialWeights;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumSq, double LastValidValue);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    /// <summary>
    /// Creates a Bilateral Filter with specified parameters.
    /// </summary>
    /// <param name="period">The length of the filter window (spatial domain).</param>
    /// <param name="sigmaSRatio">Ratio to determine spatial standard deviation (default 0.5).</param>
    /// <param name="sigmaRMult">Multiplier for range standard deviation (default 1.0).</param>
    public Bilateral(int period, double sigmaSRatio = 0.5, double sigmaRMult = 1.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _sigmaSRatio = sigmaSRatio;
        _sigmaRMult = sigmaRMult;
        _buffer = new RingBuffer(period);
        Name = $"Bilateral({period}, {sigmaSRatio:F2}, {sigmaRMult:F2})";
        WarmupPeriod = period;
        _handler = Handle;

        _spatialWeights = new double[period];
        PrecalculateSpatialWeights();
    }

    public Bilateral(ITValuePublisher source, int period, double sigmaSRatio = 0.5, double sigmaRMult = 1.0)
        : this(period, sigmaSRatio, sigmaRMult)
    {
        source.Pub += _handler;
    }

    public override bool IsHot => _buffer.IsFull;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        _buffer.Clear();
        _state = default;
        _p_state = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }
        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            double removed = _buffer.Add(val);
            _state.SumSq += (val * val);
            if (_buffer.IsFull)
            {
                _state.SumSq -= (removed * removed);
            }
        }

        double result = CalculateBilateral();
        // Use DateTime.UtcNow as Prime(ReadOnlySpan<double>) does not provide timestamps.
        // This represents an initial/primed reading rather than a real source timestamp.
        Last = new TValue(DateTime.UtcNow, result);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
            vSpan[i] = Last.Value;
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;

            double val = GetValidValue(input.Value);
            double removed = _buffer.Add(val);

            _state.SumSq += (val * val);
            if (_buffer.IsFull)
            {
                _state.SumSq -= (removed * removed);
            }
        }
        else
        {
            // Preserve SumSq as it tracks the buffer which is already at T
            double currentSumSq = _state.SumSq;

            _state = _p_state;
            _state.SumSq = currentSumSq;

            double val = GetValidValue(input.Value);

            // Defensive check: if buffer is empty, treat as first value
            if (_buffer.Count == 0)
            {
                _buffer.Add(val);
                _state.SumSq += (val * val);
            }
            else
            {
                double oldNewest = _buffer.Newest; // Get current newest before overwriting
                _buffer.UpdateNewest(val);

                _state.SumSq -= (oldNewest * oldNewest);
                _state.SumSq += (val * val);
            }
        }

        double result = CalculateBilateral();
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateBilateral()
    {
        if (_buffer.Count == 0) return double.NaN;

        // Calculate StDev
        double count = _buffer.Count;
        double sum = _buffer.Sum;

        // Variance = (SumSq - (Sum*Sum)/N) / N
        // Use Math.Max(0, ...) to handle potential floating point negative zero
        double variance = Math.Max(0, (_state.SumSq - (sum * sum) / count) / count);
        double stdev = Math.Sqrt(variance);

        double sigmaR = Math.Max(stdev * _sigmaRMult, 1e-10);
        double twoSigmaRSq = 2.0 * sigmaR * sigmaR;

        double sumWeights = 0.0;
        double sumWeightedSrc = 0.0;
        double centerVal = _buffer.Newest; // src[0]

        // Iterate from 0 to Count-1
        // i=0 corresponds to Newest (src[0])
        // i corresponds to buffer[Count - 1 - i]

        // Use InternalBuffer to avoid allocations from GetSpan() when wrapped
        ReadOnlySpan<double> buffer = _buffer.InternalBuffer;
        int capacity = _buffer.Capacity;
        int startIndex = _buffer.StartIndex;

        // Newest element index
        int newestIndex = (startIndex + (int)count - 1) % capacity;

        for (int i = 0; i < count; i++)
        {
            // Calculate index of element i steps back from newest
            // (newestIndex - i) handling wrap-around
            int idx = newestIndex - i;
            if (idx < 0) idx += capacity;

            double val = buffer[idx];
            double diffRange = centerVal - val;

            // weight_spatial = _spatialWeights[i]
            // weight_range = exp(-(diff^2) / (2 * sigma_r^2))

            double weightRange = Math.Exp(-(diffRange * diffRange) / twoSigmaRSq);
            double weight = _spatialWeights[i] * weightRange;

            sumWeights += weight;
            sumWeightedSrc += weight * val;
        }

        return sumWeights < 1e-10 ? centerVal : sumWeightedSrc / sumWeights;
    }

    private void PrecalculateSpatialWeights()
    {
        double sigmaS = Math.Max(_period * _sigmaSRatio, 1e-10);
        double twoSigmaSSq = 2.0 * sigmaS * sigmaS;
        for (int i = 0; i < _period; i++)
        {
            double diffSpatial = i;
            _spatialWeights[i] = Math.Exp(-(diffSpatial * diffSpatial) / twoSigmaSSq);
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int period, double sigmaSRatio = 0.5, double sigmaRMult = 1.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        if (destination.Length < source.Length)
            throw new ArgumentException("Destination must have length >= source length", nameof(destination));

        // Precalculate spatial weights
        double sigmaS = Math.Max(period * sigmaSRatio, 1e-10);
        double twoSigmaSSq = 2.0 * sigmaS * sigmaS;
        Span<double> spatialWeights = period <= 256 ? stackalloc double[period] : new double[period];
        for (int i = 0; i < period; i++)
        {
            double diffSpatial = i;
            spatialWeights[i] = Math.Exp(-(diffSpatial * diffSpatial) / twoSigmaSSq);
        }

        // Handle NaNs by tracking last valid value
        double lastValid = double.NaN;
        // Find initial valid value
        for (int i = 0; i < source.Length; i++)
        {
            if (double.IsFinite(source[i]))
            {
                lastValid = source[i];
                break;
            }
        }

        // If all NaNs, fill with NaN
        if (double.IsNaN(lastValid))
        {
            destination.Fill(double.NaN);
            return;
        }

        Span<double> window = period <= 256 ? stackalloc double[period] : new double[period];
        int windowIdx = 0;
        int count = 0;
        double sum = 0;
        double sumSq = 0;

        for (int i = 0; i < source.Length; i++)
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

            // Add to window
            double removed = 0;
            if (count >= period)
            {
                removed = window[windowIdx];
                sum -= removed;
                sumSq -= removed * removed;
            }

            window[windowIdx] = val;
            sum += val;
            sumSq += val * val;

            int currentNewestIdx = windowIdx;
            windowIdx = (windowIdx + 1) % period;
            if (count < period) count++;

            // Calculate StDev
            double variance = Math.Max(0, (sumSq - (sum * sum) / count) / count);
            double stdev = Math.Sqrt(variance);

            double sigmaR = Math.Max(stdev * sigmaRMult, 1e-10);
            double twoSigmaRSq = 2.0 * sigmaR * sigmaR;

            double sumWeights = 0.0;
            double sumWeightedSrc = 0.0;
            double centerVal = val; // Newest value

            // Iterate backwards through the window
            for (int k = 0; k < count; k++)
            {
                // k=0 is newest (currentNewestIdx)
                // k=1 is previous...
                int idx = currentNewestIdx - k;
                if (idx < 0) idx += period;

                double wVal = window[idx];
                double diffRange = centerVal - wVal;

                double weightRange = Math.Exp(-(diffRange * diffRange) / twoSigmaRSq);
                double weight = spatialWeights[k] * weightRange;

                sumWeights += weight;
                sumWeightedSrc += weight * wVal;
            }

            destination[i] = sumWeights < 1e-10 ? centerVal : sumWeightedSrc / sumWeights;
        }
    }
}
