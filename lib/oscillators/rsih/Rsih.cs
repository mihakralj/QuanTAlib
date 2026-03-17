using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSIH: Ehlers Hann-Windowed RSI
/// </summary>
/// <remarks>
/// A zero-mean RSI variant that uses Hann window coefficients to weight
/// price differences, producing a bounded [-1, +1] oscillator with inherent
/// smoothing. FIR filter — fixed lookback window, not recursive.
///
/// Calculation:
/// <c>w(k) = 1 - cos(2π·k / (period + 1))  for k = 1..period</c>
/// <c>CU = Σ w(k) · max(Close[k-1] - Close[k], 0)</c>
/// <c>CD = Σ w(k) · max(Close[k] - Close[k-1], 0)</c>
/// <c>RSIH = (CU - CD) / (CU + CD)</c>
/// </remarks>
/// <seealso href="Rsih.md">Detailed documentation</seealso>
/// <seealso href="rsih.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Rsih : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(int Count, double LastValid)
    {
        public static State New() => new() { Count = 0, LastValid = 0 };
    }

    private readonly int _period;
    private readonly double[] _hannCoeffs;

    private State _s = State.New();
    private State _ps = State.New();

    // RingBuffer stores close prices — needs period+1 slots for period differences
    private readonly RingBuffer _closeBuf;

    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates RSIH with specified period.
    /// </summary>
    /// <param name="period">Lookback period for Hann-windowed RSI (must be ≥ 1)</param>
    public Rsih(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 1.");
        }

        _period = period;

        // Precompute Hann window coefficients: w(k) = 1 - cos(2π·k / (period+1))
        _hannCoeffs = new double[period];
        double angleStep = 2.0 * Math.PI / (period + 1);
        for (int k = 1; k <= period; k++)
        {
            _hannCoeffs[k - 1] = 1.0 - Math.Cos(angleStep * k);
        }

        _closeBuf = new RingBuffer(period + 1);

        Name = $"Rsih({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// Creates RSIH with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Rsih(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates RSIH with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Rsih(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _period + 1;

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
    /// Computes RSIH from the close buffer using Hann-weighted CU/CD sums.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double ComputeResult()
    {
        int available = Math.Min(_s.Count, _period + 1);
        int pairs = available - 1;
        if (pairs <= 0)
        {
            return 0.0;
        }

        double cu = 0.0;
        double cd = 0.0;

        // k = 1..pairs: newer = closeBuf[available - k], older = closeBuf[available - k - 1]
        // Hann coeff index = k - 1 (0-based)
        int effectivePairs = Math.Min(pairs, _period);
        for (int k = 1; k <= effectivePairs; k++)
        {
            double newer = _closeBuf[available - k];
            double older = _closeBuf[available - k - 1];
            double diff = newer - older;
            double w = _hannCoeffs[k - 1];

            if (diff > 0.0)
            {
                cu = Math.FusedMultiplyAdd(w, diff, cu);
            }
            else if (diff < 0.0)
            {
                cd = Math.FusedMultiplyAdd(w, -diff, cd);
            }
        }

        double denom = cu + cd;
        return denom > Epsilon ? (cu - cd) / denom : 0.0;
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Rsih(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 1.");
        }

        if (source.Length == 0)
        {
            return;
        }

        var indicator = new Rsih(period);
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
    public static (TSeries Results, Rsih Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Rsih(period);
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
