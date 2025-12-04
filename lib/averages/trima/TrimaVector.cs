using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Multi-Period Triangular Moving Average (TRIMA) - SIMD optimized.
/// Calculates multiple TRIMAs with different periods for the same input series in parallel.
/// Uses last-value substitution for invalid inputs (NaN/Infinity).
/// </summary>
[SkipLocalsInit]
public class TrimaVector
{
    private readonly SmaVector _sma1;
    private readonly int _count;
    private readonly TValue[] _values;

    // Internal state for second stage
    private readonly RingBuffer[] _buffers2;
    private readonly RingBuffer[] _p_buffers2;
    private readonly double[] _lastValidValues2;

    /// <summary>
    /// Current TRIMA values for all periods.
    /// </summary>
    public ReadOnlySpan<TValue> Values => _values;

    /// <summary>
    /// Initializes TrimaVector with specified periods.
    /// </summary>
    /// <param name="periods">Array of periods (each must be > 0)</param>
    public TrimaVector(int[] periods)
    {
        _count = periods.Length;
        _values = new TValue[_count];
        _buffers2 = new RingBuffer[_count];
        _p_buffers2 = new RingBuffer[_count];
        _lastValidValues2 = new double[_count];

        int[] p1 = new int[_count];

        for (int i = 0; i < _count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(periods[i], 0);
            p1[i] = periods[i] / 2 + 1;
            int p2 = (periods[i] + 1) / 2;
            
            _buffers2[i] = new RingBuffer(p2);
            _p_buffers2[i] = new RingBuffer(p2);
        }

        _sma1 = new SmaVector(p1);
    }

    /// <summary>
    /// Resets all TRIMA states.
    /// </summary>
    public void Reset()
    {
        _sma1.Reset();
        for (int i = 0; i < _count; i++)
        {
            _buffers2[i].Clear();
            _p_buffers2[i].Clear();
        }
        Array.Clear(_lastValidValues2);
        Array.Clear(_values);
    }

    /// <summary>
    /// Updates TRIMAs with the given value.
    /// Uses last-value substitution: invalid inputs (NaN/Infinity) are replaced with
    /// the last known good value, providing continuity in the output series.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Array of TRIMA values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue[] Update(TValue input, bool isNew = true)
    {
        // First pass: SMA1
        var sma1Results = _sma1.Update(input, isNew);

        // Second pass: SMA2 (TRIMA)
        // We need to feed each SMA1 result into the corresponding SMA2
        // Since SmaVector.Update takes a single input, we can't use it directly for vector-to-vector
        // However, SmaVector is designed for single input -> multiple periods
        // Here we have multiple inputs (from SMA1) -> multiple periods (for SMA2)
        // This means we need to update each SMA2 individually, but SmaVector doesn't support that directly
        // Wait, SmaVector structure is: one input -> N periods.
        // Here we have N inputs (one for each period from SMA1) -> N periods (one for each period in SMA2).
        // So we can't use a single SmaVector for the second stage if the inputs are different.
        // We need N separate SMAs for the second stage, OR we need to modify SmaVector to support vector input.
        // But wait, TrimaVector is supposed to be optimized.
        // Let's look at how we can implement this efficiently.
        
        // Actually, since each period in TRIMA maps to a specific pair of (p1, p2),
        // and the input to the second SMA depends on the output of the first SMA,
        // the inputs to the second stage are indeed all different.
        // So we can't use SmaVector for the second stage in the same way (single input broadcast to all).
        
        // We have two options:
        // 1. Use an array of Sma objects for the second stage.
        // 2. Implement a custom vector-input SMA logic here.
        
        // Given the goal of high performance and vectorization, option 2 is better but more complex.
        // However, for now, to match the structure and ensure correctness, let's use the fact that
        // we already have SmaVector which is optimized for ring buffers.
        // But SmaVector assumes a single input value for all buffers.
        // Here, _sma1 produces an array of values, one for each period.
        // _sma2 needs to take these DIFFERENT values.
        
        // So, we cannot use SmaVector for the second stage if it only supports single input.
        // Let's check SmaVector again. Yes, Update takes `TValue input`.
        
        // So we need to implement the second stage manually using RingBuffers, similar to SmaVector
        // but accepting a vector of inputs.
        
        // Let's refactor:
        // Instead of using _sma2 as SmaVector, we'll manage the second stage buffers directly here.
        // This duplicates some logic from SmaVector but allows vector-to-vector processing.
        
        // Actually, since we are implementing TrimaVector, maybe we should just use arrays of RingBuffers
        // for both stages directly, to avoid the mismatch.
        // But _sma1 is fine because it takes the single external input.
        // It's only the second stage that is problematic.
        
        // Let's implement the second stage buffers directly.
        
        // Wait, I can't change the class structure mid-method.
        // I will implement the class using _sma1 for the first stage, and manual buffers for the second stage.
        
        // Re-reading my own thought process:
        // _sma1.Update(input) returns TValue[] with results for each period.
        // We need to feed result[i] into buffer2[i].
        
        return UpdateInternal(sma1Results, isNew);
    }

    private TValue[] UpdateInternal(TValue[] inputs, bool isNew)
    {
        if (isNew)
        {
            for (int i = 0; i < _count; i++)
            {
                _p_buffers2[i].CopyFrom(_buffers2[i]);
            }
        }
        else
        {
            for (int i = 0; i < _count; i++)
            {
                _buffers2[i].CopyFrom(_p_buffers2[i]);
            }
        }

        for (int i = 0; i < _count; i++)
        {
            double val = inputs[i].Value;
            
            // Last-value substitution for the second stage
            if (double.IsFinite(val))
            {
                _lastValidValues2[i] = val;
            }
            else
            {
                val = _lastValidValues2[i];
            }

            _buffers2[i].Add(val);
            _values[i] = new TValue(inputs[i].Time, _buffers2[i].Average);
        }

        return _values;
    }

    /// <summary>
    /// Calculates TRIMAs for the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Array of TRIMA series</returns>
    public TSeries[] Calculate(TSeries source)
    {
        // We can use the Update method for simplicity and correctness,
        // or implement a batch calculation for performance.
        // Given the complexity of double smoothing, using Update in a loop is safer and cleaner.
        // SmaVector.Calculate is optimized, but we have the two-stage issue.
        
        // Let's use the Update loop approach for now to ensure correctness.
        // It will be reasonably fast.
        
        int len = source.Count;
        var resultSeries = new TSeries[_count];
        
        // Pre-allocate lists
        var tLists = new List<long>[_count];
        var vLists = new List<double>[_count];

        for (int i = 0; i < _count; i++)
        {
            tLists[i] = new List<long>(len);
            vLists[i] = new List<double>(len);
            CollectionsMarshal.SetCount(tLists[i], len);
            CollectionsMarshal.SetCount(vLists[i], len);
        }

        Reset();

        for (int t = 0; t < len; t++)
        {
            var tVal = new TValue(source.Times[t], source.Values[t]);
            var results = Update(tVal, isNew: true);

            for (int i = 0; i < _count; i++)
            {
                CollectionsMarshal.AsSpan(tLists[i])[t] = results[i].Time;
                CollectionsMarshal.AsSpan(vLists[i])[t] = results[i].Value;
            }
        }

        for (int i = 0; i < _count; i++)
        {
            resultSeries[i] = new TSeries(tLists[i], vLists[i]);
        }

        return resultSeries;
    }

    /// <summary>
    /// Calculates TRIMAs for the entire series using specified periods.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="periods">Array of periods</param>
    /// <returns>Array of TRIMA series</returns>
    public static TSeries[] Calculate(TSeries source, int[] periods)
    {
        var trimaVector = new TrimaVector(periods);
        return trimaVector.Calculate(source);
    }
}
