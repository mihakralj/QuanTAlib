using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MADH: Ehlers Moving Average Difference with Hann
/// </summary>
/// <remarks>
/// A zero-centered percentage oscillator that computes the difference between
/// two Hann-windowed FIR averages (short and long). The long length is derived
/// from the short length plus half the dominant cycle. Pure FIR — no recursive state.
///
/// Calculation:
/// <c>LongLength = (int)(ShortLength + DominantCycle / 2.0)</c>
/// <c>Filt = Σ w(k) · Close[k-1] / Σ w(k)   where w(k) = 1 - cos(2π·k / (N+1))</c>
/// <c>MADH = 100 · (Filt1 / Filt2 - 1)</c>
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
    private readonly double[] _hannShort;
    private readonly double[] _hannLong;
    private readonly double _coefSumShort;
    private readonly double _coefSumLong;

    private State _s = State.New();
    private State _ps = State.New();

    // RingBuffer stores close prices — needs longLength + 1 slots
    private readonly RingBuffer _closeBuf;

    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates MADH with specified short length and dominant cycle.
    /// </summary>
    /// <param name="shortLength">Short filter window (must be ≥ 1)</param>
    /// <param name="dominantCycle">Dominant cycle estimate (must be ≥ 2)</param>
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
        _longLength = (int)(shortLength + dominantCycle / 2.0);

        // Precompute short Hann window coefficients: w(k) = 1 - cos(2π·k / (N+1))
        _hannShort = new double[shortLength];
        double angleStepShort = 2.0 * Math.PI / (shortLength + 1);
        double sumShort = 0;
        for (int k = 1; k <= shortLength; k++)
        {
            double w = 1.0 - Math.Cos(angleStepShort * k);
            _hannShort[k - 1] = w;
            sumShort += w;
        }
        _coefSumShort = sumShort;

        // Precompute long Hann window coefficients
        _hannLong = new double[_longLength];
        double angleStepLong = 2.0 * Math.PI / (_longLength + 1);
        double sumLong = 0;
        for (int k = 1; k <= _longLength; k++)
        {
            double w = 1.0 - Math.Cos(angleStepLong * k);
            _hannLong[k - 1] = w;
            sumLong += w;
        }
        _coefSumLong = sumLong;

        _closeBuf = new RingBuffer(_longLength + 1);

        Name = $"Madh({shortLength},{dominantCycle})";
        WarmupPeriod = _longLength + 1;
    }

    /// <summary>
    /// Creates MADH with specified source, short length and dominant cycle.
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

    public override bool IsHot => _s.Count > _longLength;

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
        int available = Math.Min(_s.Count, _longLength + 1);
        if (available <= 0)
        {
            return 0.0;
        }

        // Short Hann FIR: scan most recent shortLength values
        double sumShort = 0.0;
        int effectiveShort = Math.Min(available, _shortLength);
        for (int k = 1; k <= effectiveShort; k++)
        {
            double val = _closeBuf[available - k];
            sumShort = Math.FusedMultiplyAdd(_hannShort[k - 1], val, sumShort);
        }
        double filt1 = _coefSumShort > Epsilon ? sumShort / _coefSumShort : 0.0;

        // Long Hann FIR: scan most recent longLength values
        double sumLong = 0.0;
        int effectiveLong = Math.Min(available, _longLength);
        for (int k = 1; k <= effectiveLong; k++)
        {
            double val = _closeBuf[available - k];
            sumLong = Math.FusedMultiplyAdd(_hannLong[k - 1], val, sumLong);
        }
        double filt2 = _coefSumLong > Epsilon ? sumLong / _coefSumLong : 0.0;

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
