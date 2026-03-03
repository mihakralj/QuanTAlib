using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ALAGUERRE: Adaptive Laguerre Filter (Ehlers)
/// </summary>
/// <remarks>
/// Adaptive variant of the Laguerre Filter where alpha auto-adjusts each bar based on
/// how well the filter tracks price. Computes diff = |price - filter_output|, normalizes
/// to [0,1] using highest/lowest over a lookback window, then takes median of normalized
/// values over a second window for the adaptive alpha.
///
/// When price diverges from filter (trending), alpha increases → faster tracking.
/// When price stays near filter (ranging), alpha decreases → more smoothing.
///
/// Calculation: Standard 4-element Laguerre all-pass cascade with variable alpha per bar.
/// </remarks>
/// <seealso href="ALaguerre.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class ALaguerre : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double L0, double L1, double L2, double L3,
        double PrevL0, double PrevL1, double PrevL2,
        double Alpha, double LastResult,
        int Count, double LastValid, bool IsInitialized)
    {
        public static State New() => new()
        {
            L0 = 0,
            L1 = 0,
            L2 = 0,
            L3 = 0,
            PrevL0 = 0,
            PrevL1 = 0,
            PrevL2 = 0,
            Alpha = 0.5,
            LastResult = 0,
            Count = 0,
            LastValid = 0,
            IsInitialized = false
        };
    }

    private readonly int _length;
    private readonly int _medianLength;
    private readonly double[] _diffBuffer;
    private readonly double[] _coeffBuffer;
    private int _diffHead;
    private int _diffCount;
    private int _coeffHead;
    private int _coeffCount;
    private State _s = State.New();
    private State _ps = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    // Saved state for bar correction
    private int _p_diffHead;
    private int _p_diffCount;
    private int _p_coeffHead;
    private int _p_coeffCount;
    private double[]? _p_diffBuffer;
    private double[]? _p_coeffBuffer;

    private const int WarmupBars = 4;

    /// <summary>
    /// Creates an Adaptive Laguerre Filter with the specified lookback parameters.
    /// </summary>
    /// <param name="length">Lookback period for HH/LL normalization of tracking error (default 20). Must be > 0.</param>
    /// <param name="medianLength">Lookback period for median smoothing of alpha (default 5). Must be > 0.</param>
    public ALaguerre(int length = 20, int medianLength = 5)
    {
        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than 0", nameof(length));
        }
        if (medianLength <= 0)
        {
            throw new ArgumentException("Median length must be greater than 0", nameof(medianLength));
        }

        _length = length;
        _medianLength = medianLength;
        _diffBuffer = new double[length];
        _coeffBuffer = new double[medianLength];
        _diffHead = 0;
        _diffCount = 0;
        _coeffHead = 0;
        _coeffCount = 0;
        Name = $"ALaguerre({length},{medianLength})";
        WarmupPeriod = Math.Max(WarmupBars, length);
    }

    /// <summary>
    /// Creates an Adaptive Laguerre Filter with event-driven source subscription.
    /// </summary>
    public ALaguerre(ITValuePublisher source, int length = 20, int medianLength = 5) : this(length, medianLength)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates an Adaptive Laguerre Filter from TSeries source with auto-priming.
    /// </summary>
    public ALaguerre(TSeries source, int length = 20, int medianLength = 5) : this(length, medianLength)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }
    public override bool IsHot => _s.Count >= WarmupPeriod;

    private const int StackAllocThreshold = 512;
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _diffHead = 0;
        _diffCount = 0;
        _coeffHead = 0;
        _coeffCount = 0;
        Array.Clear(_diffBuffer);
        Array.Clear(_coeffBuffer);

        int len = source.Length;

        bool foundValid = false;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _lastValidValue = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            Last = new TValue(DateTime.MinValue, double.NaN);
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
            SaveBufferState();
            return;
        }

        double[]? rented = len > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> tempOutput = rented != null
            ? rented.AsSpan(0, len)
            : stackalloc double[len];

        try
        {
            CalculateCore(source, tempOutput, _length, _medianLength,
                _diffBuffer, ref _diffHead, ref _diffCount,
                _coeffBuffer, ref _coeffHead, ref _coeffCount,
                ref _s, ref _lastValidValue);
            double result = tempOutput[len - 1];
            Last = new TValue(DateTime.MinValue, result);
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
            SaveBufferState();
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
            SaveBufferState();
        }
        else
        {
            _s = _ps;
            _lastValidValue = _p_lastValidValue;
            RestoreBufferState();
        }

        double val = GetValidValue(input.Value);
        val = Compute(val, _length, _medianLength,
            _diffBuffer, ref _diffHead, ref _diffCount,
            _coeffBuffer, ref _coeffHead, ref _coeffCount,
            ref _s);
        Last = new TValue(input.Time, val);
        PubEvent(Last, isNew);
        return Last;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        // Use fresh buffers for batch calculation
        double[] batchDiffBuffer = new double[_length];
        double[] batchCoeffBuffer = new double[_medianLength];
        int batchDiffHead = 0, batchDiffCount = 0;
        int batchCoeffHead = 0, batchCoeffCount = 0;
        State state = State.New();
        double lastValidValue = 0;

        // Find first valid value
        for (int k = 0; k < sourceValues.Length; k++)
        {
            if (double.IsFinite(sourceValues[k]))
            {
                lastValidValue = sourceValues[k];
                break;
            }
        }

        CalculateCore(sourceValues, vSpan, _length, _medianLength,
            batchDiffBuffer, ref batchDiffHead, ref batchDiffCount,
            batchCoeffBuffer, ref batchCoeffHead, ref batchCoeffCount,
            ref state, ref lastValidValue);

        _s = state;
        _lastValidValue = lastValidValue;
        Array.Copy(batchDiffBuffer, _diffBuffer, _length);
        _diffHead = batchDiffHead;
        _diffCount = batchDiffCount;
        Array.Copy(batchCoeffBuffer, _coeffBuffer, _medianLength);
        _coeffHead = batchCoeffHead;
        _coeffCount = batchCoeffCount;

        sourceTimes.CopyTo(tSpan);

        _ps = _s;
        _p_lastValidValue = _lastValidValue;
        SaveBufferState();
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core adaptive Laguerre computation: variable alpha from tracking-error normalization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, int length, int medianLength,
        double[] diffBuffer, ref int diffHead, ref int diffCount,
        double[] coeffBuffer, ref int coeffHead, ref int coeffCount,
        ref State s)
    {
        if (!s.IsInitialized)
        {
            s.L0 = input;
            s.L1 = input;
            s.L2 = input;
            s.L3 = input;
            s.PrevL0 = input;
            s.PrevL1 = input;
            s.PrevL2 = input;
            s.Alpha = 0.5;
            s.LastResult = input;
            s.IsInitialized = true;
            s.Count = 1;
            s.LastValid = input;

            // First bar: diff is 0, coeff is 0
            diffBuffer[diffHead] = 0;
            diffHead = (diffHead + 1) % length;
            if (diffCount < length)
            {
                diffCount++;
            }

            coeffBuffer[coeffHead] = 0;
            coeffHead = (coeffHead + 1) % medianLength;
            if (coeffCount < medianLength)
            {
                coeffCount++;
            }

            return input;
        }

        // Compute tracking error: |price - last_filter_output|
        double diff = Math.Abs(input - s.LastResult);

        // Store diff in circular buffer
        diffBuffer[diffHead] = diff;
        diffHead = (diffHead + 1) % length;
        if (diffCount < length)
        {
            diffCount++;
        }

        // Compute HH and LL of diff over lookback
        double hh = double.MinValue;
        double ll = double.MaxValue;
        int start = (diffHead - diffCount + length) % length;
        for (int i = 0; i < diffCount; i++)
        {
            double d = diffBuffer[(start + i) % length];
            if (d > hh)
            {
                hh = d;
            }
            if (d < ll)
            {
                ll = d;
            }
        }

        // Normalize diff to [0,1]
        double coeff = (hh - ll > 1e-15) ? (diff - ll) / (hh - ll) : 0.5;

        // Store coeff in median buffer
        coeffBuffer[coeffHead] = coeff;
        coeffHead = (coeffHead + 1) % medianLength;
        if (coeffCount < medianLength)
        {
            coeffCount++;
        }

        // Compute median of coeff buffer as adaptive alpha
        double alpha = MedianOfBuffer(coeffBuffer, coeffCount, coeffHead, medianLength);

        // Save previous L values
        double prevL0 = s.L0;
        double prevL1 = s.L1;
        double prevL2 = s.L2;

        // Compute Laguerre elements with adaptive alpha
        // alpha = (1 - gamma), so L0 = alpha*input + (1-alpha)*L0[1]
        double oneMinusAlpha = 1.0 - alpha;

        // L0 = alpha * input + (1-alpha) * L0[1]
        // skipcq: CS-R1140 - FMA provides better precision for IIR accumulation
        s.L0 = Math.FusedMultiplyAdd(oneMinusAlpha, prevL0, alpha * input);

        // L1 = -(1-alpha) * L0 + L0[1] + (1-alpha) * L1[1]
        s.L1 = Math.FusedMultiplyAdd(oneMinusAlpha, prevL1, Math.FusedMultiplyAdd(-oneMinusAlpha, s.L0, prevL0));

        // L2 = -(1-alpha) * L1 + L1[1] + (1-alpha) * L2[1]
        s.L2 = Math.FusedMultiplyAdd(oneMinusAlpha, prevL2, Math.FusedMultiplyAdd(-oneMinusAlpha, s.L1, prevL1));

        // L3 = -(1-alpha) * L2 + L2[1] + (1-alpha) * L3[1]
        s.L3 = Math.FusedMultiplyAdd(oneMinusAlpha, s.L3, Math.FusedMultiplyAdd(-oneMinusAlpha, s.L2, prevL2));

        s.PrevL0 = prevL0;
        s.PrevL1 = prevL1;
        s.PrevL2 = prevL2;
        s.Alpha = alpha;

        s.Count++;
        s.LastValid = input;

        // Filt = (L0 + 2*L1 + 2*L2 + L3) / 6
        double result = (s.L0 + 2.0 * s.L1 + 2.0 * s.L2 + s.L3) / 6.0;
        s.LastResult = result;
        return result;
    }

    /// <summary>
    /// Computes median from circular buffer without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double MedianOfBuffer(double[] buffer, int count, int head, int capacity)
    {
        if (count == 0)
        {
            return 0.5;
        }
        if (count == 1)
        {
            return buffer[(head - 1 + capacity) % capacity];
        }

        // Copy active elements to stackalloc for sorting
        const int maxStackAlloc = 64;
        Span<double> temp = count <= maxStackAlloc
            ? stackalloc double[count]
            : new double[count];

        int start = (head - count + capacity) % capacity;
        for (int i = 0; i < count; i++)
        {
            temp[i] = buffer[(start + i) % capacity];
        }

        // Insertion sort (count is typically small: 5-20)
        for (int i = 1; i < count; i++)
        {
            double key = temp[i];
            int j = i - 1;
            while (j >= 0 && temp[j] > key)
            {
                temp[j + 1] = temp[j];
                j--;
            }
            temp[j + 1] = key;
        }

        // Median
        if ((count & 1) == 1)
        {
            return temp[count >> 1];
        }
        int mid = count >> 1;
        return (temp[mid - 1] + temp[mid]) * 0.5;
    }

    /// <summary>
    /// Core calculation for batch processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        int length, int medianLength,
        double[] diffBuffer, ref int diffHead, ref int diffCount,
        double[] coeffBuffer, ref int coeffHead, ref int coeffCount,
        ref State state, ref double lastValidValue)
    {
        int len = source.Length;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        for (int i = 0; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            if (!double.IsFinite(val))
            {
                val = lastValidValue;
            }
            else
            {
                lastValidValue = val;
            }

            double result = Compute(val, length, medianLength,
                diffBuffer, ref diffHead, ref diffCount,
                coeffBuffer, ref coeffHead, ref coeffCount,
                ref state);
            Unsafe.Add(ref outRef, i) = result;
        }
    }

    /// <summary>
    /// Calculates Adaptive Laguerre Filter for a TSeries, returning results and a hot indicator instance.
    /// </summary>
    public static (TSeries Results, ALaguerre Indicator) Calculate(TSeries source, int length = 20, int medianLength = 5)
    {
        var alaguerre = new ALaguerre(length, medianLength);
        TSeries results = alaguerre.Update(source);
        return (results, alaguerre);
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int length = 20, int medianLength = 5)
    {
        var alaguerre = new ALaguerre(length, medianLength);
        return alaguerre.Update(source);
    }

    /// <summary>
    /// Zero-allocation span-based batch calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int length = 20, int medianLength = 5)
    {
        if (length <= 0)
        {
            throw new ArgumentException("Length must be greater than 0", nameof(length));
        }
        if (medianLength <= 0)
        {
            throw new ArgumentException("Median length must be greater than 0", nameof(medianLength));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double[] diffBuf = new double[length];
        double[] coeffBuf = new double[medianLength];
        int dHead = 0, dCount = 0;
        int cHead = 0, cCount = 0;

        var state = State.New();
        double lastValid = 0;
        bool foundValid = false;

        for (int k = 0; k < source.Length; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            output.Fill(double.NaN);
            return;
        }

        CalculateCore(source, output, length, medianLength,
            diffBuf, ref dHead, ref dCount,
            coeffBuf, ref cHead, ref cCount,
            ref state, ref lastValid);
    }
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _diffHead = 0;
        _diffCount = 0;
        _coeffHead = 0;
        _coeffCount = 0;
        Array.Clear(_diffBuffer);
        Array.Clear(_coeffBuffer);
        _p_diffBuffer = null;
        _p_coeffBuffer = null;
        _p_diffHead = 0;
        _p_diffCount = 0;
        _p_coeffHead = 0;
        _p_coeffCount = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveBufferState()
    {
        _p_diffHead = _diffHead;
        _p_diffCount = _diffCount;
        _p_coeffHead = _coeffHead;
        _p_coeffCount = _coeffCount;
        _p_diffBuffer ??= new double[_length];
        _p_coeffBuffer ??= new double[_medianLength];
        Array.Copy(_diffBuffer, _p_diffBuffer, _length);
        Array.Copy(_coeffBuffer, _p_coeffBuffer, _medianLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreBufferState()
    {
        _diffHead = _p_diffHead;
        _diffCount = _p_diffCount;
        _coeffHead = _p_coeffHead;
        _coeffCount = _p_coeffCount;
        if (_p_diffBuffer != null)
        {
            Array.Copy(_p_diffBuffer, _diffBuffer, _length);
        }
        if (_p_coeffBuffer != null)
        {
            Array.Copy(_p_coeffBuffer, _coeffBuffer, _medianLength);
        }
    }
}
