using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// One Euro Filter (ONEEURO)
/// Speed-adaptive first-order low-pass filter that balances jitter removal
/// against responsiveness. Uses an adaptive cutoff frequency: low cutoff at
/// low signal speed (reduces jitter), high cutoff at high speed (reduces lag).
/// </summary>
/// <remarks>
/// Algorithm (T_e = 1.0 for uniform bar spacing):
///   1. Raw derivative:    dx = x - x̂_prev
///   2. Smooth derivative: d̂x = α_d · dx + (1 - α_d) · d̂x_prev
///   3. Adaptive cutoff:   f_c = minCutoff + β · |d̂x|
///   4. Smoothing factor:  r = 2π · f_c, α = r / (r + 1)
///   5. Filtered output:   x̂ = α · x + (1 - α) · x̂_prev
///
/// Reference: Casiez, Roussel &amp; Vogel (2012), "1€ Filter: A Simple
/// Speed-Based Low-Pass Filter for Noisy Input in Interactive Systems,"
/// CHI '12, pp. 2527-2530. DOI: 10.1145/2207676.2208639
///
/// Complexity: O(1) per bar, O(1) memory
/// </remarks>
[SkipLocalsInit]
public sealed class OneEuro : AbstractBase
{
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private State _s;
    private State _ps;

    [StructLayout(LayoutKind.Sequential)]
    private record struct State
    {
        public double XHat;      // filtered signal
        public double DxHat;     // filtered derivative
        public double LastValid; // last finite input
        public int Count;
    }

    public double MinCutoff { get; }
    public double Beta { get; }
    public double DCutoff { get; }

    // Precomputed derivative smoothing factor (constant since T_e = 1.0)
    private readonly double _alphaD;

    public OneEuro(double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
    {
        if (minCutoff <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minCutoff), "minCutoff must be positive.");
        }
        if (beta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beta), "beta must be non-negative.");
        }
        if (dCutoff <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dCutoff), "dCutoff must be positive.");
        }

        MinCutoff = minCutoff;
        Beta = beta;
        DCutoff = dCutoff;

        // Precompute α_d = r / (r + 1), r = 2π · dCutoff · T_e, T_e = 1.0
        double rD = 2.0 * Math.PI * dCutoff;
        _alphaD = rD / (rD + 1.0);

        Name = $"OneEuro({minCutoff},{beta},{dCutoff})";
        WarmupPeriod = 1;
        Reset();
    }

    public OneEuro(ITValuePublisher source, double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0) : this(minCutoff, beta, dCutoff)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? _, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override bool IsHot => _s.Count >= WarmupPeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew) { _ps = _s; } else { _s = _ps; }
        var s = _s;

        double val = input.Value;

        // NaN/Infinity guard: substitute last valid
        if (!double.IsFinite(val))
        {
            if (s.Count > 0)
            {
                val = s.LastValid;
            }
            else
            {
                Last = new TValue(input.Time, val);
                PubEvent(Last, isNew);
                return Last;
            }
        }
        else
        {
            s.LastValid = val;
        }

        if (s.Count == 0)
        {
            // First bar: passthrough
            s.XHat = val;
            s.DxHat = 0.0;
            s.Count = 1;
        }
        else
        {
            // Step 1: Raw derivative (T_e = 1.0 for bars)
            double dx = val - s.XHat;

            // Step 2: Smooth derivative
            s.DxHat = Math.FusedMultiplyAdd(_alphaD, dx - s.DxHat, s.DxHat);

            // Step 3: Adaptive cutoff
            double fc = MinCutoff + Beta * Math.Abs(s.DxHat);

            // Step 4: Smoothing factor α = r / (r + 1), r = 2π·fc
            double r = 2.0 * Math.PI * fc;
            double alpha = r / (r + 1.0);

            // Step 5: Filter
            s.XHat = Math.FusedMultiplyAdd(alpha, val - s.XHat, s.XHat);

            s.Count++;
        }

        _s = s;
        Last = new TValue(input.Time, s.XHat);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var output = new double[source.Count];

        Batch(source.Values, output, MinCutoff, Beta, DCutoff);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], output[i]));
        }

        // Restore internal state by replaying
        Reset();
        int replayStart = Math.Max(0, source.Count - 1);
        for (int i = replayStart; i < source.Count; i++)
        {
            Update(new TValue(times[i], source.Values[i]), isNew: true);
        }

        Last = new TValue(times[^1], output[^1]);
        return result;
    }

    public static TSeries Batch(TSeries source, double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
    {
        var indicator = new OneEuro(minCutoff, beta, dCutoff);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
    {
        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output span must be at least as long as source.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        // Precompute derivative alpha
        double rD = 2.0 * Math.PI * dCutoff;
        double alphaD = rD / (rD + 1.0);

        double xHat = source[0];
        double dxHat = 0.0;
        output[0] = xHat;

        for (int i = 1; i < source.Length; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                output[i] = xHat; // hold last valid
                continue;
            }

            // Raw derivative
            double dx = val - xHat;

            // Smooth derivative
            dxHat = Math.FusedMultiplyAdd(alphaD, dx - dxHat, dxHat);

            // Adaptive cutoff
            double fc = minCutoff + beta * Math.Abs(dxHat);

            // Smoothing factor
            double r = 2.0 * Math.PI * fc;
            double alpha = r / (r + 1.0);

            // Filter
            xHat = Math.FusedMultiplyAdd(alpha, val - xHat, xHat);

            output[i] = xHat;
        }
    }

    public override void Reset()
    {
        _s = default;
        _s.LastValid = double.NaN;
        _ps = default;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, OneEuro Indicator) Calculate(TSeries source,
        double minCutoff = 1.0, double beta = 0.007, double dCutoff = 1.0)
    {
        var indicator = new OneEuro(minCutoff, beta, dCutoff);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
