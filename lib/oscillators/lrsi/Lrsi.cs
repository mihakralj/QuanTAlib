// LRSI: Laguerre RSI
// John Ehlers, "Cybernetic Analysis for Stocks and Futures" (2004), Chapter 14.
// A modified RSI that uses a 4-element Laguerre filter as its core moving average.
// The gamma (damping) parameter trades responsiveness against smoothness.
// Output is dimensionless [0, 1]; no period parameter required.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LRSI: Laguerre RSI
/// </summary>
/// <remarks>
/// Ehlers' Laguerre RSI replaces the standard RSI's gain/loss smoothing with
/// a 4-stage cascaded Laguerre filter. The four outputs (L0–L3) represent
/// successively delayed and damped versions of the input; the RSI-style
/// numerator/denominator is computed over the stage-to-stage differences.
///
/// Filter stages (γ = gamma):
/// <code>
///   L0 = (1−γ)·price + γ·L0[1]
///   L1 = −γ·L0 + L0[1] + γ·L1[1]
///   L2 = −γ·L1 + L1[1] + γ·L2[1]
///   L3 = −γ·L2 + L2[1] + γ·L3[1]
/// </code>
///
/// RSI computation:
/// <code>
///   cu = Σ max(L(k)−L(k+1), 0)   for k = 0..2
///   cd = Σ max(L(k+1)−L(k), 0)   for k = 0..2
///   LRSI = cu / (cu + cd)   [or 0.5 when cu + cd == 0]
/// </code>
///
/// Properties:
/// <list type="bullet">
///   <item>Output is always in [0, 1]</item>
///   <item>WarmupPeriod = 4 (four filter stages)</item>
///   <item>Lower γ = faster response; higher γ = smoother output</item>
///   <item>Recursive filter — no SIMD possible in streaming path</item>
/// </list>
///
/// References:
///   Ehlers, J.F. (2004). Cybernetic Analysis for Stocks and Futures. Wiley. Ch. 14.
///   PineScript reference: lrsi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Lrsi : ITValuePublisher
{
    private readonly double _gamma;
    private readonly double _oneMinusGamma;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double L0,
        double L1,
        double L2,
        double L3,
        double LastValid);

    private State _s;
    private State _ps;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required before output is considered reliable.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True after 4 bars have been processed through all filter stages.</summary>
    public bool IsHot => _s.L0 != 0.0 || _s.L1 != 0.0 || _s.L2 != 0.0 || _s.L3 != 0.0;

    /// <summary>Current LRSI value in [0, 1].</summary>
    public TValue Last { get; private set; }

    public event TValuePublishedHandler? Pub;

    private int _count;
    private int _pcount;

    /// <summary>
    /// Creates LRSI with the specified gamma damping factor.
    /// </summary>
    /// <param name="gamma">Laguerre damping factor in [0.0, 1.0] (default 0.5).
    /// Lower values produce faster response; higher values produce smoother output.</param>
    public Lrsi(double gamma = 0.5)
    {
        if (gamma < 0.0 || gamma > 1.0)
        {
            throw new ArgumentException("gamma must be in [0.0, 1.0]", nameof(gamma));
        }

        _gamma = gamma;
        _oneMinusGamma = 1.0 - gamma;

        _s = new State(0.0, 0.0, 0.0, 0.0, 0.5);
        _ps = _s;

        WarmupPeriod = 4;
        Name = $"Lrsi({gamma:F2})";
    }

    /// <summary>
    /// Creates LRSI chained to an ITValuePublisher source.
    /// </summary>
    public Lrsi(ITValuePublisher source, double gamma = 0.5) : this(gamma)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>Resets all state to initial conditions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(0.0, 0.0, 0.0, 0.0, 0.5);
        _ps = _s;
        _count = 0;
        _pcount = 0;
        Last = default;
    }

    /// <summary>
    /// Updates LRSI with a new price value.
    /// </summary>
    /// <param name="input">Price input (typically close)</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar (bar correction)</param>
    /// <returns>Current LRSI value as TValue in [0, 1]</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize input — substitute last-valid on NaN/Infinity
        if (!double.IsFinite(value))
        {
            value = _s.LastValid;
        }

        if (isNew)
        {
            _ps = _s;
            _pcount = _count;
            _count++;
        }
        else
        {
            _s = _ps;
            _count = _pcount;
        }

        // State local copy — enables JIT struct promotion to registers
        var s = _s;

        // Update LastValid after rollback so we capture the sanitised value
        if (double.IsFinite(input.Value))
        {
            s.LastValid = value;
        }

        double g = _gamma;
        double omg = _oneMinusGamma;

        // Stage 0: first-order IIR lowpass
        // L0 = (1−γ)·price + γ·L0[1]   ≡ FMA(g, prevL0, omg·price)
        double prevL0 = s.L0;
        double prevL1 = s.L1;
        double prevL2 = s.L2;
        double prevL3 = s.L3;

        s.L0 = Math.FusedMultiplyAdd(g, prevL0, omg * value);

        // Stage 1: −γ·L0 + L0[1] + γ·L1[1]   ≡ FMA(g, prevL1, prevL0 − g·s.L0)
        s.L1 = Math.FusedMultiplyAdd(g, prevL1, Math.FusedMultiplyAdd(-g, s.L0, prevL0));

        // Stage 2: −γ·L1 + L1[1] + γ·L2[1]
        s.L2 = Math.FusedMultiplyAdd(g, prevL2, Math.FusedMultiplyAdd(-g, s.L1, prevL1));

        // Stage 3: −γ·L2 + L2[1] + γ·L3[1]
        s.L3 = Math.FusedMultiplyAdd(g, prevL3, Math.FusedMultiplyAdd(-g, s.L2, prevL2));

        // RSI-style: sum up/down stage differences
        double l0 = s.L0;
        double l1 = s.L1;
        double l2 = s.L2;
        double l3 = s.L3;

        double cu = (l0 > l1 ? l0 - l1 : 0.0)
                  + (l1 > l2 ? l1 - l2 : 0.0)
                  + (l2 > l3 ? l2 - l3 : 0.0);

        double cd = (l0 < l1 ? l1 - l0 : 0.0)
                  + (l1 < l2 ? l2 - l1 : 0.0)
                  + (l2 < l3 ? l3 - l2 : 0.0);

        double total = cu + cd;
        double lrsi = total != 0.0 ? cu / total : 0.5;

        _s = s;

        Last = new TValue(input.Time, lrsi);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Batch-computes LRSI over a TSeries source.
    /// </summary>
    public static TSeries Calculate(TSeries source, double gamma = 0.5)
    {
        var lrsi = new Lrsi(gamma);
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            vSpan[i] = lrsi.Update(source[i], isNew: true).Value;
            tSpan[i] = source.Times[i];
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch static: span → span. Uses StackallocThreshold pattern (§2.6).
    /// No internal buffers needed beyond scalar state — no heap allocation for any input size.
    /// </summary>
    /// <param name="source">Input price span</param>
    /// <param name="output">Output LRSI span (must match source length)</param>
    /// <param name="gamma">Laguerre damping factor in [0.0, 1.0]</param>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double gamma = 0.5)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (gamma < 0.0 || gamma > 1.0)
        {
            throw new ArgumentException("gamma must be in [0.0, 1.0]", nameof(gamma));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double g = gamma;
        double omg = 1.0 - gamma;
        double l0 = 0.0, l1 = 0.0, l2 = 0.0, l3 = 0.0;
        double lastValid = 0.5;

        for (int i = 0; i < len; i++)
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

            double pL0 = l0;
            double pL1 = l1;
            double pL2 = l2;
            double pL3 = l3;

            l0 = Math.FusedMultiplyAdd(g, pL0, omg * val);
            l1 = Math.FusedMultiplyAdd(g, pL1, Math.FusedMultiplyAdd(-g, l0, pL0));
            l2 = Math.FusedMultiplyAdd(g, pL2, Math.FusedMultiplyAdd(-g, l1, pL1));
            l3 = Math.FusedMultiplyAdd(g, pL3, Math.FusedMultiplyAdd(-g, l2, pL2));

            double cu = (l0 > l1 ? l0 - l1 : 0.0)
                      + (l1 > l2 ? l1 - l2 : 0.0)
                      + (l2 > l3 ? l2 - l3 : 0.0);

            double cd = (l0 < l1 ? l1 - l0 : 0.0)
                      + (l1 < l2 ? l2 - l1 : 0.0)
                      + (l2 < l3 ? l3 - l2 : 0.0);

            double total = cu + cd;
            output[i] = total != 0.0 ? cu / total : 0.5;
        }
    }

    /// <summary>Gamma damping factor used by this instance.</summary>
    public double Gamma => _gamma;
}
