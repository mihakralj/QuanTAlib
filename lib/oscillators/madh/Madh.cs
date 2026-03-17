using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MADH: Ehlers Moving Average Difference with Hann
/// </summary>
/// <remarks>
/// A zero-crossing trend oscillator that computes the percentage difference
/// between a short and long Hann-windowed FIR moving average.
///
/// Calculation:
/// <c>LongLength = IntPortion(ShortLength + DominantCycle / 2)</c>
/// <c>Filt1 = HannFIR(Close, ShortLength)</c>
/// <c>Filt2 = HannFIR(Close, LongLength)</c>
/// <c>MADH = 100 × (Filt1 / Filt2 - 1)</c>
///
/// Hann coefficients: <c>w(k) = 1 - cos(2π·k / (N + 1))</c>
/// </remarks>
/// <seealso href="Madh.md">Detailed documentation</seealso>
/// <seealso href="madh.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Madh : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(int Count, double LastValid)
    {
        public static State New() => new() { Count = 0, LastValid = 0 };
    }

    private readonly int _shortLength;
    private readonly int _longLength;
    private readonly double[] _shortCoeffs;
    private readonly double[] _longCoeffs;

    private State _s = State.New();
    private State _ps = State.New();

    // RingBuffer stores close prices — needs longLength+1 slots
    private readonly RingBuffer _closeBuf;

    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates MADH with specified parameters.
    /// </summary>
    /// <param name="shortLength">Short Hann FIR window length (must be ≥ 1)</param>
    /// <param name="dominantCycle">Dominant cycle period (must be ≥ 2)</param>
    public Madh(int shortLength = 8, int dominantCycle = 27)
    {
        if (shortLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(shortLength), shortLength, "ShortLength must be at least 1.");
        }
        if (dominantCycle < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(dominantCycle), dominantCycle, "DominantCycle must be at least 2.");
        }

        _shortLength = shortLength;
        _longLength = shortLength + dominantCycle / 2;

        // Precompute short Hann coefficients: w(k) = 1 - cos(2π·k / (N+1))
        _shortCoeffs = new double[_shortLength];
        double shortAngleStep = 2.0 * Math.PI / (_shortLength + 1);
        for (int k = 1; k <= _shortLength; k++)
        {
            _shortCoeffs[k - 1] = 1.0 - Math.Cos(shortAngleStep * k);
        }

        // Precompute long Hann coefficients
        _longCoeffs = new double[_longLength];
        double longAngleStep = 2.0 * Math.PI / (_longLength + 1);
        for (int k = 1; k <= _longLength; k++)
        {
            _longCoeffs[k - 1] = 1.0 - Math.Cos(longAngleStep * k);
        }

        _closeBuf = new RingBuffer(_longLength);

        Name = $"Madh({shortLength},{dominantCycle})";
        WarmupPeriod = _longLength;
    }

    /// <summary>
    /// Creates MADH with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Madh(ITValuePublisher source, int shortLength = 8, int dominantCycle = 27) : this(shortLength, dominantCycle)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates MADH with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Madh(TSeries source, int shortLength = 8, int dominantCycle = 27) : this(shortLength, dominantCycle)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _longLength;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        _closeBuf.Clear();

        int len = source.Length;
        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
        }

        Last = new TValue(DateTime.MinValue, ComputeResult());
        _ps = _s;
        _closeBuf.Snapshot();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref State s)
    {
        if (double.IsFinite(input))
        {
            s.LastValid = input;
            return input;
        }
        return s.LastValid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _closeBuf.Snapshot();
        }
        else
        {
            _s = _ps;
            _closeBuf.Restore();
        }

        double val = GetValidValue(input.Value, ref _s);
        Step(val);
        double result = ComputeResult();

        Last = new TValue(input.Time, result);
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
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            double val = source.Values[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
            vSpan[i] = ComputeResult();
        }

        _ps = _s;
        _closeBuf.Snapshot();
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: add close price to ring buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Step(double input)
    {
        _s.Count++;
        _closeBuf.Add(input);
    }

    /// <summary>
    /// Computes MADH from the close buffer using dual Hann-weighted FIR averages.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double ComputeResult()
    {
        int available = Math.Min(_s.Count, _longLength);
        if (available < 1)
        {
            return 0.0;
        }

        // Short Hann FIR
        double filt1 = 0.0;
        double coef1 = 0.0;
        int shortAvail = Math.Min(available, _shortLength);
        for (int k = 1; k <= shortAvail; k++)
        {
            double w = _shortCoeffs[k - 1];
            filt1 = Math.FusedMultiplyAdd(w, _closeBuf[available - k], filt1);
            coef1 += w;
        }
        if (coef1 > Epsilon)
        {
            filt1 /= coef1;
        }

        // Long Hann FIR
        double filt2 = 0.0;
        double coef2 = 0.0;
        for (int k = 1; k <= available; k++)
        {
            double w = _longCoeffs[k - 1];
            filt2 = Math.FusedMultiplyAdd(w, _closeBuf[available - k], filt2);
            coef2 += w;
        }
        if (coef2 > Epsilon)
        {
            filt2 /= coef2;
        }

        // MADH = 100 * (Filt1 / Filt2 - 1)
        return Math.Abs(filt2) > Epsilon ? 100.0 * (filt1 / filt2 - 1.0) : 0.0;
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int shortLength = 8, int dominantCycle = 27)
    {
        var indicator = new Madh(shortLength, dominantCycle);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int shortLength = 8, int dominantCycle = 27)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (shortLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(shortLength), shortLength, "ShortLength must be at least 1.");
        }
        if (dominantCycle < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(dominantCycle), dominantCycle, "DominantCycle must be at least 2.");
        }

        if (source.Length == 0)
        {
            return;
        }

        var indicator = new Madh(shortLength, dominantCycle);
        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                indicator._s.LastValid = val;
            }
            else
            {
                val = indicator._s.LastValid;
            }

            indicator.Step(val);
            output[i] = indicator.ComputeResult();
        }
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Madh Indicator) Calculate(TSeries source, int shortLength = 8, int dominantCycle = 27)
    {
        var indicator = new Madh(shortLength, dominantCycle);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _closeBuf.Clear();
        Last = default;
    }
}
