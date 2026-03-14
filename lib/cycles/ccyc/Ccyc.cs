using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CCYC: Ehlers Cyber Cycle — isolates the dominant cycle component from price data
/// using a 4-tap FIR pre-smoother and a 2-pole high-pass IIR filter.
/// </summary>
/// <remarks>
/// From John F. Ehlers, "Cybernetic Analysis for Stocks and Futures" (Wiley, 2004), Chapter 4.
///
/// Algorithm:
/// 1. 4-bar FIR smoother: smooth = (x + 2x[1] + 2x[2] + x[3]) / 6
///    Zeros at periods 2 and 3 eliminate aliased noise.
/// 2. 2-pole high-pass IIR:
///    cycle = c_hp * (smooth - 2*smooth[1] + smooth[2]) + c_fb1*cycle[1] + c_fb2*cycle[2]
///    where c_hp = (1-0.5*alpha)^2, c_fb1 = 2(1-alpha), c_fb2 = -(1-alpha)^2
/// 3. Bootstrap (bars &lt; 7): cycle = (x - 2x[1] + x[2]) / 4
/// 4. Trigger = cycle[1] (one-bar delay for crossover signals)
///
/// Properties:
/// - O(1) per bar: 6 multiplications, 5 additions, 2 state variables
/// - Zero allocation in hot path
/// - Alpha controls high-pass cutoff: lower = smoother/more lag
/// - Trigger property provides the one-bar-delayed crossover line
/// </remarks>
[SkipLocalsInit]
public sealed class Ccyc : AbstractBase
{
    private readonly double _chp;   // (1 - 0.5*alpha)^2
    private readonly double _cfb1;  // 2*(1 - alpha)
    private readonly double _cfb2;  // -(1 - alpha)^2

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Price0, double Price1, double Price2, double Price3,
        double Smooth0, double Smooth1, double Smooth2,
        double Cycle0, double Cycle1, double Cycle2,
        int Count, double LastValid);

    private State _s;
    private State _ps;

    /// <summary>One-bar-delayed cycle value for crossover detection.</summary>
    public double Trigger { get; private set; }

    /// <inheritdoc />
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ccyc indicator with the specified alpha (damping factor).
    /// </summary>
    /// <param name="alpha">Damping factor controlling high-pass cutoff. Must be in (0, 1) exclusive. Default 0.07.</param>
    public Ccyc(double alpha = 0.07)
    {
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ArgumentException("Alpha must be between 0 and 1 (exclusive).", nameof(alpha));
        }

        double halfAlpha = 1.0 - (0.5 * alpha);
        _chp = halfAlpha * halfAlpha;
        double oneMinusAlpha = 1.0 - alpha;
        _cfb1 = 2.0 * oneMinusAlpha;
        _cfb2 = -(oneMinusAlpha * oneMinusAlpha);

        Name = $"Ccyc({alpha:F2})";
        WarmupPeriod = 7;
        _s = default;
        _ps = default;
    }

    /// <summary>
    /// Creates a new Ccyc indicator chained to a publisher source.
    /// </summary>
    /// <param name="source">Source indicator to subscribe to.</param>
    /// <param name="alpha">Damping factor controlling high-pass cutoff. Default 0.07.</param>
    public Ccyc(ITValuePublisher source, double alpha = 0.07) : this(alpha)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // State management: save/restore for bar correction
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double price = input.Value;

        // NaN/Infinity guard: substitute last valid value
        if (!double.IsFinite(price))
        {
            price = s.LastValid;
        }
        else
        {
            s = s with { LastValid = price };
        }

        // Increment bar count
        int count = isNew ? s.Count + 1 : s.Count;

        // Shift price history
        double price3 = s.Price2;
        double price2 = s.Price1;
        double price1 = s.Price0;
        double price0 = price;

        // 4-tap FIR smoother: smooth = (x + 2*x1 + 2*x2 + x3) / 6
        double smooth = (price0 + (2.0 * price1) + (2.0 * price2) + price3) / 6.0;

        // Shift smooth history
        double smooth2 = s.Smooth1;
        double smooth1 = s.Smooth0;
        double smooth0 = smooth;

        double cycle;
        if (count < 7)
        {
            // Bootstrap: second-difference of raw price
            cycle = (price0 - (2.0 * price1) + price2) * 0.25;
        }
        else
        {
            // Steady-state: 2-pole high-pass IIR on smoothed input
            // cycle = c_hp * (smooth - 2*smooth1 + smooth2) + c_fb1*cycle1 + c_fb2*cycle2
            double diff = smooth0 - (2.0 * smooth1) + smooth2;
            cycle = Math.FusedMultiplyAdd(_chp, diff,
                        Math.FusedMultiplyAdd(_cfb1, s.Cycle1, _cfb2 * s.Cycle2));
        }

        // Guard: if IIR diverges to non-finite, substitute zero
        if (!double.IsFinite(cycle))
        {
            cycle = 0.0;
        }

        // Shift cycle history
        double cycle2 = s.Cycle1;
        double cycle1 = s.Cycle0;
        double cycle0 = cycle;

        // Trigger = previous cycle value
        Trigger = cycle1;

        _s = new State(
            price0, price1, price2, price3,
            smooth0, smooth1, smooth2,
            cycle0, cycle1, cycle2,
            count, s.LastValid);

        Last = new TValue(input.Time, cycle);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Processes a full TSeries, returning the cycle component for each bar.
    /// </summary>
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

        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i]);
            vSpan[i] = result.Value;
        }
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <inheritdoc />
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Static batch: creates a Ccyc, processes source, returns output TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, double alpha = 0.07)
    {
        var ind = new Ccyc(alpha);
        return ind.Update(source);
    }

    /// <summary>
    /// Static span-based batch: computes Cyber Cycle into output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha = 0.07)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ArgumentException("Alpha must be between 0 and 1 (exclusive).", nameof(alpha));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double halfAlpha = 1.0 - (0.5 * alpha);
        double chp = halfAlpha * halfAlpha;
        double oneMinusAlpha = 1.0 - alpha;
        double cfb1 = 2.0 * oneMinusAlpha;
        double cfb2 = -(oneMinusAlpha * oneMinusAlpha);

        double price0 = 0, price1 = 0, price2 = 0, price3 = 0;
        double smooth0 = 0, smooth1 = 0, smooth2 = 0;
        double cycle0 = 0, cycle1 = 0, cycle2 = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = price0; // last valid
            }

            price3 = price2;
            price2 = price1;
            price1 = price0;
            price0 = val;

            double smooth = (price0 + (2.0 * price1) + (2.0 * price2) + price3) / 6.0;
            smooth2 = smooth1;
            smooth1 = smooth0;
            smooth0 = smooth;

            double cycle;
            int barNum = i + 1;
            if (barNum < 7)
            {
                cycle = (price0 - (2.0 * price1) + price2) * 0.25;
            }
            else
            {
                double diff = smooth0 - (2.0 * smooth1) + smooth2;
                cycle = Math.FusedMultiplyAdd(chp, diff,
                            Math.FusedMultiplyAdd(cfb1, cycle1, cfb2 * cycle2));
            }

            // Guard: if IIR diverges to non-finite, substitute zero
            if (!double.IsFinite(cycle))
            {
                cycle = 0.0;
            }

            cycle2 = cycle1;
            cycle1 = cycle0;
            cycle0 = cycle;
            output[i] = cycle;
        }
    }

    /// <summary>
    /// Static convenience method: returns (TSeries results, Ccyc indicator) for inspection.
    /// </summary>
    public static (TSeries Results, Ccyc Indicator) Calculate(TSeries source, double alpha = 0.07)
    {
        var ind = new Ccyc(alpha);
        var results = ind.Update(source);
        return (results, ind);
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _s = default;
        _ps = default;
        Last = default;
        Trigger = 0;
    }
}
