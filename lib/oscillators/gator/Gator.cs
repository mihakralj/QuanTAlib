using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// GATOR: Williams Gator Oscillator
/// Dual-histogram oscillator derived from three SMMA (Wilder's RMA) lines of the
/// Williams Alligator. Upper histogram = |Jaw_shifted − Teeth_shifted| (always ≥ 0).
/// Lower histogram = −|Teeth_shifted − Lips_shifted| (always ≤ 0).
/// Primary output (Val) = Upper histogram.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>SMMA_jaw = RMA(input, jawPeriod); shifted forward jawShift bars</item>
/// <item>SMMA_teeth = RMA(input, teethPeriod); shifted forward teethShift bars</item>
/// <item>SMMA_lips = RMA(input, lipsPeriod); shifted forward lipsShift bars</item>
/// <item>Upper = |SMMA_jaw[jawShift] − SMMA_teeth[teethShift]|</item>
/// <item>Lower = −|SMMA_teeth[teethShift] − SMMA_lips[lipsShift]|</item>
/// </list>
///
/// <b>Sources:</b>
/// Bill Williams, "New Trading Dimensions", Wiley, 1998
/// </remarks>
/// <seealso href="Gator.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Gator : AbstractBase
{
    private readonly int _jawPeriod;
    private readonly int _jawShift;
    private readonly int _teethPeriod;
    private readonly int _teethShift;
    private readonly int _lipsPeriod;
    private readonly int _lipsShift;

    private readonly double _jawAlpha;
    private readonly double _jawDecay;
    private readonly double _teethAlpha;
    private readonly double _teethDecay;
    private readonly double _lipsAlpha;
    private readonly double _lipsDecay;

    // Ring buffers to store shifted SMMA values (size = shift + 1)
    private readonly RingBuffer _jawHistory;
    private readonly RingBuffer _teethHistory;
    private readonly RingBuffer _lipsHistory;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double JawSmma,
        double TeethSmma,
        double LipsSmma,
        double LastValidValue,
        double LowerValue,
        int Count
    );

    private State _s;
    private State _ps;

    /// <summary>
    /// Lower histogram value from the most recent calculation.
    /// Always ≤ 0 (negated absolute difference between Teeth and Lips).
    /// </summary>
    public double Lower => _s.LowerValue;

    /// <summary>
    /// Creates GATOR with specified Alligator parameters.
    /// </summary>
    /// <param name="jawPeriod">Jaw SMMA period (must be ≥ 1, default 13)</param>
    /// <param name="jawShift">Jaw forward shift (must be ≥ 0, default 8)</param>
    /// <param name="teethPeriod">Teeth SMMA period (must be ≥ 1, default 8)</param>
    /// <param name="teethShift">Teeth forward shift (must be ≥ 0, default 5)</param>
    /// <param name="lipsPeriod">Lips SMMA period (must be ≥ 1, default 5)</param>
    /// <param name="lipsShift">Lips forward shift (must be ≥ 0, default 3)</param>
    public Gator(int jawPeriod = 13, int jawShift = 8, int teethPeriod = 8, int teethShift = 5, int lipsPeriod = 5, int lipsShift = 3)
    {
        if (jawPeriod < 1)
        {
            throw new ArgumentException("Jaw period must be greater than or equal to 1", nameof(jawPeriod));
        }
        if (teethPeriod < 1)
        {
            throw new ArgumentException("Teeth period must be greater than or equal to 1", nameof(teethPeriod));
        }
        if (lipsPeriod < 1)
        {
            throw new ArgumentException("Lips period must be greater than or equal to 1", nameof(lipsPeriod));
        }
        if (jawShift < 0)
        {
            throw new ArgumentException("Jaw shift must be non-negative", nameof(jawShift));
        }
        if (teethShift < 0)
        {
            throw new ArgumentException("Teeth shift must be non-negative", nameof(teethShift));
        }
        if (lipsShift < 0)
        {
            throw new ArgumentException("Lips shift must be non-negative", nameof(lipsShift));
        }

        _jawPeriod = jawPeriod;
        _jawShift = jawShift;
        _teethPeriod = teethPeriod;
        _teethShift = teethShift;
        _lipsPeriod = lipsPeriod;
        _lipsShift = lipsShift;

        _jawAlpha = 1.0 / jawPeriod;
        _jawDecay = 1.0 - _jawAlpha;
        _teethAlpha = 1.0 / teethPeriod;
        _teethDecay = 1.0 - _teethAlpha;
        _lipsAlpha = 1.0 / lipsPeriod;
        _lipsDecay = 1.0 - _lipsAlpha;

        // Ring buffers: need shift+1 slots to store current + shifted history
        _jawHistory = new RingBuffer(jawShift + 1);
        _teethHistory = new RingBuffer(teethShift + 1);
        _lipsHistory = new RingBuffer(lipsShift + 1);

        Name = $"Gator({jawPeriod},{teethPeriod},{lipsPeriod})";

        // Warmup = max(jawPeriod + jawShift, teethPeriod + teethShift, lipsPeriod + lipsShift)
        WarmupPeriod = Math.Max(jawPeriod + jawShift, Math.Max(teethPeriod + teethShift, lipsPeriod + lipsShift));

        _s = new State(0, 0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates GATOR with specified source and parameters.
    /// </summary>
    public Gator(ITValuePublisher source, int jawPeriod = 13, int jawShift = 8, int teethPeriod = 8, int teethShift = 5, int lipsPeriod = 5, int lipsShift = 3)
        : this(jawPeriod, jawShift, teethPeriod, teethShift, lipsPeriod, lipsShift)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True when all three shift buffers are full (enough shifted history).
    /// </summary>
    public override bool IsHot => _jawHistory.IsFull && _teethHistory.IsFull && _lipsHistory.IsFull;

    /// <summary>
    /// Updates the indicator with a single TValue input.
    /// Uses DPO-proven Snapshot/Restore pattern for bar correction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // NaN/Infinity handling: last-valid substitution (before branching)
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = _s.LastValidValue;
        }

        if (isNew)
        {
            // skipcq:CS-R1140 - DPO pattern: save state, snapshot buffers, compute, add
            _ps = _s;
            _jawHistory.Snapshot();
            _teethHistory.Snapshot();
            _lipsHistory.Snapshot();

            var s = _s;
            if (double.IsFinite(input.Value))
            {
                s.LastValidValue = val;
            }
            s.Count++;

            ComputeSmma(ref s, val);
            _jawHistory.Add(s.JawSmma <= 0 && s.Count <= 1 ? val : s.JawSmma);
            _teethHistory.Add(s.TeethSmma <= 0 && s.Count <= 1 ? val : s.TeethSmma);
            _lipsHistory.Add(s.LipsSmma <= 0 && s.Count <= 1 ? val : s.LipsSmma);

            _s = s;
        }
        else
        {
            // skipcq:CS-R1140 - Mirror isNew=true: restore state, restore buffers, recompute, re-add
            _s = _ps;
            _jawHistory.Restore();
            _teethHistory.Restore();
            _lipsHistory.Restore();

            var s = _s;
            if (double.IsFinite(input.Value))
            {
                s.LastValidValue = val;
            }
            s.Count++;

            ComputeSmma(ref s, val);
            _jawHistory.Add(s.JawSmma <= 0 && s.Count <= 1 ? val : s.JawSmma);
            _teethHistory.Add(s.TeethSmma <= 0 && s.Count <= 1 ? val : s.TeethSmma);
            _lipsHistory.Add(s.LipsSmma <= 0 && s.Count <= 1 ? val : s.LipsSmma);

            _s = s;
        }

        // Calculate histogram values using shifted (oldest) values from buffers
        double upper;
        double lower;

        if (_jawHistory.IsFull && _teethHistory.IsFull && _lipsHistory.IsFull)
        {
            double jawShifted = _jawHistory.Oldest;
            double teethShifted = _teethHistory.Oldest;
            double lipsShifted = _lipsHistory.Oldest;

            upper = Math.Abs(jawShifted - teethShifted);
            lower = -Math.Abs(teethShifted - lipsShifted);
        }
        else
        {
            upper = 0.0;
            lower = 0.0;
        }

        _s.LowerValue = lower;

        Last = new TValue(input.Time, upper);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _jawPeriod, _jawShift, _teethPeriod, _teethShift, _lipsPeriod, _lipsShift);
        source.Times.CopyTo(tSpan);

        // Prime internal state by replaying
        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _jawHistory.Clear();
        _teethHistory.Clear();
        _lipsHistory.Clear();
        _s = default;
        _ps = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod + 10);
        int startIndex = source.Length - warmupLength;

        // Find a valid seed value for last-valid tracking
        _s.LastValidValue = 0;

        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _s.LastValidValue = source[i];
                break;
            }
        }

        if (_s.LastValidValue == 0)
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _s.LastValidValue = source[i];
                    break;
                }
            }
        }

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]), isNew: true);
        }

        // After priming, sync saved state so first isNew=false works
        _ps = _s;
        _jawHistory.Snapshot();
        _teethHistory.Snapshot();
        _lipsHistory.Snapshot();
    }

    /// <summary>
    /// Calculates GATOR for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int jawPeriod = 13, int jawShift = 8, int teethPeriod = 8, int teethShift = 5, int lipsPeriod = 5, int lipsShift = 3)
    {
        var gator = new Gator(jawPeriod, jawShift, teethPeriod, teethShift, lipsPeriod, lipsShift);
        return gator.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation. Outputs upper histogram values.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int jawPeriod = 13, int jawShift = 8, int teethPeriod = 8, int teethShift = 5,
        int lipsPeriod = 5, int lipsShift = 3)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (jawPeriod < 1)
        {
            throw new ArgumentException("Jaw period must be greater than or equal to 1", nameof(jawPeriod));
        }
        if (teethPeriod < 1)
        {
            throw new ArgumentException("Teeth period must be greater than or equal to 1", nameof(teethPeriod));
        }
        if (lipsPeriod < 1)
        {
            throw new ArgumentException("Lips period must be greater than or equal to 1", nameof(lipsPeriod));
        }
        if (jawShift < 0)
        {
            throw new ArgumentException("Jaw shift must be non-negative", nameof(jawShift));
        }
        if (teethShift < 0)
        {
            throw new ArgumentException("Teeth shift must be non-negative", nameof(teethShift));
        }
        if (lipsShift < 0)
        {
            throw new ArgumentException("Lips shift must be non-negative", nameof(lipsShift));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, jawPeriod, jawShift, teethPeriod, teethShift, lipsPeriod, lipsShift);
    }

    /// <summary>
    /// Calculates GATOR and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Gator Indicator) Calculate(TSeries source,
        int jawPeriod = 13, int jawShift = 8, int teethPeriod = 8, int teethShift = 5,
        int lipsPeriod = 5, int lipsShift = 3)
    {
        var indicator = new Gator(jawPeriod, jawShift, teethPeriod, teethShift, lipsPeriod, lipsShift);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeSmma(ref State s, double val)
    {
        if (s.Count <= 1)
        {
            // Seed: first value initializes all SMMAs
            s.JawSmma = val;
            s.TeethSmma = val;
            s.LipsSmma = val;
        }
        else
        {
            // SMMA: alpha * input + (1-alpha) * prevSmma = FMA(prevSmma, decay, alpha * input)
            s.JawSmma = Math.FusedMultiplyAdd(s.JawSmma, _jawDecay, _jawAlpha * val);
            s.TeethSmma = Math.FusedMultiplyAdd(s.TeethSmma, _teethDecay, _teethAlpha * val);
            s.LipsSmma = Math.FusedMultiplyAdd(s.LipsSmma, _lipsDecay, _lipsAlpha * val);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output,
        int jawPeriod, int jawShift, int teethPeriod, int teethShift, int lipsPeriod, int lipsShift)
    {
        int len = source.Length;
        double jawAlpha = 1.0 / jawPeriod;
        double jawDecay = 1.0 - jawAlpha;
        double teethAlpha = 1.0 / teethPeriod;
        double teethDecay = 1.0 - teethAlpha;
        double lipsAlpha = 1.0 / lipsPeriod;
        double lipsDecay = 1.0 - lipsAlpha;

        int jawBufSize = jawShift + 1;
        int teethBufSize = teethShift + 1;
        int lipsBufSize = lipsShift + 1;

        const int StackAllocThreshold = 256;

        double[]? rentedJaw = jawBufSize > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(jawBufSize) : null;
        Span<double> jawBuf = rentedJaw != null
            ? rentedJaw.AsSpan(0, jawBufSize)
            : stackalloc double[jawBufSize];

        double[]? rentedTeeth = teethBufSize > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(teethBufSize) : null;
        Span<double> teethBuf = rentedTeeth != null
            ? rentedTeeth.AsSpan(0, teethBufSize)
            : stackalloc double[teethBufSize];

        double[]? rentedLips = lipsBufSize > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(lipsBufSize) : null;
        Span<double> lipsBuf = rentedLips != null
            ? rentedLips.AsSpan(0, lipsBufSize)
            : stackalloc double[lipsBufSize];

        try
        {
            double lastValid = 0;
            double jawSmma = 0;
            double teethSmma = 0;
            double lipsSmma = 0;
            int jawIdx = 0;
            int teethIdx = 0;
            int lipsIdx = 0;
            int jawFilled = 0;
            int teethFilled = 0;
            int lipsFilled = 0;
            bool seeded = false;

            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                double jawVal;
                double teethVal;
                double lipsVal;

                if (!seeded)
                {
                    jawSmma = val;
                    teethSmma = val;
                    lipsSmma = val;
                    seeded = true;
                    jawVal = val;
                    teethVal = val;
                    lipsVal = val;
                }
                else
                {
                    jawSmma = Math.FusedMultiplyAdd(jawSmma, jawDecay, jawAlpha * val);
                    teethSmma = Math.FusedMultiplyAdd(teethSmma, teethDecay, teethAlpha * val);
                    lipsSmma = Math.FusedMultiplyAdd(lipsSmma, lipsDecay, lipsAlpha * val);

                    jawVal = jawSmma;
                    teethVal = teethSmma;
                    lipsVal = lipsSmma;
                }

                jawBuf[jawIdx] = jawVal;
                if (jawFilled < jawBufSize)
                {
                    jawFilled++;
                }
                jawIdx++;
                if (jawIdx >= jawBufSize)
                {
                    jawIdx = 0;
                }

                teethBuf[teethIdx] = teethVal;
                if (teethFilled < teethBufSize)
                {
                    teethFilled++;
                }
                teethIdx++;
                if (teethIdx >= teethBufSize)
                {
                    teethIdx = 0;
                }

                lipsBuf[lipsIdx] = lipsVal;
                if (lipsFilled < lipsBufSize)
                {
                    lipsFilled++;
                }
                lipsIdx++;
                if (lipsIdx >= lipsBufSize)
                {
                    lipsIdx = 0;
                }

                if (jawFilled >= jawBufSize && teethFilled >= teethBufSize && lipsFilled >= lipsBufSize)
                {
                    double jawShifted = jawBuf[jawIdx % jawBufSize];
                    double teethShifted = teethBuf[teethIdx % teethBufSize];

                    output[i] = Math.Abs(jawShifted - teethShifted);
                }
                else
                {
                    output[i] = 0.0;
                }
            }
        }
        finally
        {
            if (rentedJaw != null)
            {
                ArrayPool<double>.Shared.Return(rentedJaw);
            }
            if (rentedTeeth != null)
            {
                ArrayPool<double>.Shared.Return(rentedTeeth);
            }
            if (rentedLips != null)
            {
                ArrayPool<double>.Shared.Return(rentedLips);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _jawHistory.Clear();
        _teethHistory.Clear();
        _lipsHistory.Clear();
        _s = new State(0, 0, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }
}
