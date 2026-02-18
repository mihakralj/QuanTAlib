using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LAGUERRE: Laguerre Filter (Ehlers)
/// </summary>
/// <remarks>
/// Four-element IIR filter using cascaded all-pass sections with a damping factor gamma.
/// Produces extremely smooth output from only 4 data elements. When gamma=0, degenerates
/// to a 4-tap FIR (triangular weighted average). As gamma approaches 1, smoothing increases.
///
/// Calculation: <c>Filt = (L0 + 2*L1 + 2*L2 + L3) / 6</c> where L0..L3 are cascaded all-pass outputs.
/// </remarks>
/// <seealso href="Laguerre.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Laguerre : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double L0, double L1, double L2, double L3,
        double PrevL0, double PrevL1, double PrevL2,
        int Count, double LastValid, bool IsInitialized)
    {
        public static State New() => new()
        {
            L0 = 0, L1 = 0, L2 = 0, L3 = 0,
            PrevL0 = 0, PrevL1 = 0, PrevL2 = 0,
            Count = 0, LastValid = 0, IsInitialized = false
        };
    }

    private readonly double _gamma;
    private readonly double _oneMinusGamma;
    private State _s = State.New();
    private State _ps = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;

    private const int WarmupBars = 4;

    /// <summary>
    /// Creates a Laguerre Filter with the specified damping factor.
    /// </summary>
    /// <param name="gamma">Damping factor [0, 1). 0 = FIR (no feedback), higher = more smoothing. Default 0.8.</param>
    public Laguerre(double gamma = 0.8)
    {
        if (gamma < 0.0 || gamma >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be >= 0 and < 1");
        }

        _gamma = gamma;
        _oneMinusGamma = 1.0 - gamma;
        Name = $"Laguerre({gamma:F2})";
        WarmupPeriod = WarmupBars;
    }

    /// <summary>
    /// Creates a Laguerre Filter with event-driven source subscription.
    /// </summary>
    public Laguerre(ITValuePublisher source, double gamma = 0.8) : this(gamma)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates a Laguerre Filter from TSeries source with auto-priming.
    /// </summary>
    public Laguerre(TSeries source, double gamma = 0.8) : this(gamma)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <inheritdoc/>
    public override bool IsHot => _s.Count >= WarmupBars;

    private const int StackAllocThreshold = 512;

    /// <inheritdoc/>
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
            return;
        }

        double[]? rented = len > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> tempOutput = rented != null
            ? rented.AsSpan(0, len)
            : stackalloc double[len];

        try
        {
            CalculateCore(source, tempOutput, _gamma, _oneMinusGamma, ref _s, ref _lastValidValue);
            double result = tempOutput[len - 1];
            Last = new TValue(DateTime.MinValue, result);
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _s = _ps;
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);
        val = Compute(val, _gamma, _oneMinusGamma, ref _s);
        Last = new TValue(input.Time, val);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
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

        State state = _s;
        double lastValidValue = _lastValidValue;

        CalculateCore(sourceValues, vSpan, _gamma, _oneMinusGamma, ref state, ref lastValidValue);

        _s = state;
        _lastValidValue = lastValidValue;

        sourceTimes.CopyTo(tSpan);

        _ps = _s;
        _p_lastValidValue = _lastValidValue;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core Laguerre filter: 4 cascaded all-pass elements with binomial-weighted output.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double gamma, double oneMinusGamma, ref State s)
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
            s.IsInitialized = true;
            s.Count = 1;
            s.LastValid = input;
            return input;
        }

        // Save previous L values for next iteration
        double prevL0 = s.L0;
        double prevL1 = s.L1;
        double prevL2 = s.L2;

        // L0 = (1 - gamma) * input + gamma * L0[1]
        // skipcq: CS-R1140 - FMA provides better precision for IIR accumulation
        s.L0 = Math.FusedMultiplyAdd(gamma, prevL0, oneMinusGamma * input);

        // L1 = -gamma * L0 + L0[1] + gamma * L1[1]
        s.L1 = Math.FusedMultiplyAdd(gamma, prevL1, Math.FusedMultiplyAdd(-gamma, s.L0, prevL0));

        // L2 = -gamma * L1 + L1[1] + gamma * L2[1]
        s.L2 = Math.FusedMultiplyAdd(gamma, prevL2, Math.FusedMultiplyAdd(-gamma, s.L1, prevL1));

        // L3 = -gamma * L2 + L2[1] + gamma * L3[1]
        s.L3 = Math.FusedMultiplyAdd(gamma, s.L3, Math.FusedMultiplyAdd(-gamma, s.L2, prevL2));

        s.PrevL0 = prevL0;
        s.PrevL1 = prevL1;
        s.PrevL2 = prevL2;

        s.Count++;
        s.LastValid = input;

        // Filt = (L0 + 2*L1 + 2*L2 + L3) / 6
        return (s.L0 + 2.0 * s.L1 + 2.0 * s.L2 + s.L3) / 6.0;
    }

    /// <summary>
    /// Core calculation for batch processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        double gamma, double oneMinusGamma, ref State state, ref double lastValidValue)
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

            double result = Compute(val, gamma, oneMinusGamma, ref state);
            Unsafe.Add(ref outRef, i) = result;
        }
    }

    /// <summary>
    /// Calculates Laguerre Filter for a TSeries, returning results and a hot indicator instance.
    /// </summary>
    public static (TSeries Results, Laguerre Indicator) Calculate(TSeries source, double gamma = 0.8)
    {
        var laguerre = new Laguerre(gamma);
        TSeries results = laguerre.Update(source);
        return (results, laguerre);
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, double gamma = 0.8)
    {
        var laguerre = new Laguerre(gamma);
        return laguerre.Update(source);
    }

    /// <summary>
    /// Zero-allocation span-based batch calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double gamma = 0.8)
    {
        if (gamma < 0.0 || gamma >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be >= 0 and < 1");
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double oneMinusGamma = 1.0 - gamma;

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

        CalculateCore(source, output, gamma, oneMinusGamma, ref state, ref lastValid);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
