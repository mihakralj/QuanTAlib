using System;
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

    private record struct State(double SumSq, double LastInput, double LastValidValue);
    private State _state;
    private State _p_state;

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

        _spatialWeights = new double[period];
        PrecalculateSpatialWeights();
    }

    public Bilateral(ITValuePublisher source, int period, double sigmaSRatio = 0.5, double sigmaRMult = 1.0) 
        : this(period, sigmaSRatio, sigmaRMult)
    {
        source.Pub += (item) => Update(item);
    }

    public override bool IsHot => _buffer.IsFull;

    public override void Prime(ReadOnlySpan<double> source)
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
            _state.LastInput = val;
        }

        double result = CalculateBilateral();
        Last = new TValue(DateTime.MinValue, result);
        _p_state = _state;
    }

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
            _state.LastInput = val;
        }
        else
        {
            // Preserve SumSq as it tracks the buffer which is already at T
            double currentSumSq = _state.SumSq;
            
            _state = _p_state;
            _state.SumSq = currentSumSq;
            
            double val = GetValidValue(input.Value);
            double oldNewest = _buffer.Newest; // Get current newest before overwriting
            _buffer.UpdateNewest(val);
            
            _state.SumSq -= (oldNewest * oldNewest);
            _state.SumSq += (val * val);
        }

        double result = CalculateBilateral();
        Last = new TValue(input.Time, result);
        PubEvent(Last);
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

        return sumWeights == 0.0 ? centerVal : sumWeightedSrc / sumWeights;
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
}
