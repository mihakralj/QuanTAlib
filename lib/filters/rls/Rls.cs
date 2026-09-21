using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RLS: Recursive Least Squares Adaptive Filter
/// An adaptive FIR filter that maintains an inverse correlation matrix P to achieve
/// faster convergence than LMS. Uses a forgetting factor λ to control memory horizon.
/// Converges in ~order iterations with O(order²) per-bar complexity.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/rls.md
///
/// Key properties:
///   - Adaptive FIR: weight vector w[0..order-1] learns from streaming data
///   - Predicts src[0] from src[1]..src[order] (no look-ahead)
///   - Gain vector: k = P·x / (λ + x^T·P·x)
///   - P update: P = (1/λ)(P - k·(P·x)^T)
///   - Overlay indicator (price-following)
///   - O(order²) per bar for both prediction and weight/matrix update
///
/// Complexity: O(order²) per bar
/// </remarks>
[SkipLocalsInit]
public sealed class Rls : AbstractBase
{
    private const double Epsilon = 1e-30;

    private readonly int _order;
    private readonly double _lambda;
    private readonly double _invLambda;
    private readonly RingBuffer _inputBuffer;
    private readonly double[] _weights;
    private readonly double[] _p_weights;
    private readonly double[] _P;      // inverse correlation matrix (order x order), row-major
    private readonly double[] _p_P;    // snapshot for bar correction
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double LastValid;
        public int Count;
    }

    private State _state;
    private State _p_state;

    /// <summary>Number of FIR taps (adaptive weights).</summary>
    public int Order => _order;

    /// <summary>Forgetting factor controlling memory horizon (0 &lt; λ ≤ 1).</summary>
    public double Lambda => _lambda;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count > _order;

    public Rls(int order = 16, double lambda = 0.99)
    {
        if (order < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be >= 2.");
        }

        if (lambda <= 0.0 || lambda > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), "Forgetting factor lambda must be in (0, 1].");
        }

        _order = order;
        _lambda = lambda;
        _invLambda = 1.0 / lambda;
        Name = $"RLS({order},{lambda:F2})";
        WarmupPeriod = order + 1;

        // Weight vector + snapshot for bar correction
        _weights = new double[order];
        _p_weights = new double[order];

        // Inverse correlation matrix P = delta * I (high initial uncertainty)
        const double delta = 100.0;
        int matSize = order * order;
        _P = new double[matSize];
        _p_P = new double[matSize];
        for (int i = 0; i < order; i++)
        {
            _P[(i * order) + i] = delta;
        }

        // Ring buffer holds order+1 values: current + order past values
        _inputBuffer = new RingBuffer(order + 1);
        _state.LastValid = double.NaN;
    }

    public Rls(ITValuePublisher source, int order = 16, double lambda = 0.99)
        : this(order, lambda)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        Batch(values, results, _order, _lambda);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Resync internal state by replaying
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        int matSize = _order * _order;

        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_weights, _p_weights, _order);
            Array.Copy(_P, _p_P, matSize);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_weights, _weights, _order);
            Array.Copy(_p_P, _P, matSize);
        }

        var s = _state;

        // Handle bad data — last-valid substitution
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = val;
        }

        // Input buffer: Add for new bars, UpdateNewest for corrections
        if (isNew)
        {
            _inputBuffer.Add(val);
        }
        else
        {
            _inputBuffer.UpdateNewest(val);
        }

        double result;

        if (_inputBuffer.Count <= _order)
        {
            // Not enough history to form prediction — pass through
            result = val;
        }
        else
        {
            // --- Step 1: Prediction y = w^T * x ---
            double y = 0.0;
            for (int i = 0; i < _order; i++)
            {
                double xi = _inputBuffer[^(i + 2)]; // src[i+1]
                y = Math.FusedMultiplyAdd(_weights[i], xi, y);
            }

            // --- RLS update — only learn from confirmed bars ---
            if (isNew)
            {
                // --- Step 2: Compute Px = P * x ---
                // skipcq: CS-W1082 - stackalloc safe: order is bounded by constructor validation
                Span<double> px = stackalloc double[_order];
                for (int i = 0; i < _order; i++)
                {
                    double rowSum = 0.0;
                    int rowBase = i * _order;
                    for (int j = 0; j < _order; j++)
                    {
                        double xj = _inputBuffer[^(j + 2)];
                        rowSum = Math.FusedMultiplyAdd(_P[rowBase + j], xj, rowSum);
                    }
                    px[i] = rowSum;
                }

                // --- Step 3: Compute denom = λ + x^T * Px ---
                double denom = _lambda;
                for (int i = 0; i < _order; i++)
                {
                    double xi = _inputBuffer[^(i + 2)];
                    denom = Math.FusedMultiplyAdd(xi, px[i], denom);
                }

                // --- Step 4: Gain vector k = Px / denom ---
                double invDenom = denom > Epsilon ? 1.0 / denom : 0.0;

                // skipcq: CS-W1082 - stackalloc safe: order is bounded
                Span<double> k = stackalloc double[_order];
                for (int i = 0; i < _order; i++)
                {
                    k[i] = px[i] * invDenom;
                }

                // --- Step 5: Weight update w = w + k * error ---
                double error = val - y;
                for (int i = 0; i < _order; i++)
                {
                    _weights[i] = Math.FusedMultiplyAdd(k[i], error, _weights[i]);
                }

                // --- Step 6: P update: P = (1/λ)(P - k * Px^T) ---
                for (int i = 0; i < _order; i++)
                {
                    double ki = k[i];
                    int rowBase = i * _order;
                    for (int j = 0; j < _order; j++)
                    {
                        _P[rowBase + j] = _invLambda * (_P[rowBase + j] - (ki * px[j]));
                    }
                }
            }

            result = y;
        }

        if (isNew)
        {
            s.Count++;
        }

        _state = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public static TSeries Batch(TSeries source, int order = 16, double lambda = 0.99)
    {
        var indicator = new Rls(order, lambda);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int order = 16, double lambda = 0.99)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        if (order < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be >= 2.");
        }

        if (lambda <= 0.0 || lambda > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), "Forgetting factor lambda must be in (0, 1].");
        }

        double invLambda = 1.0 / lambda;

        // Weight vector
        double[] w = new double[order];
        var ring = new RingBuffer(order + 1);

        // Inverse correlation matrix P = delta * I
        const double delta = 100.0;
        int matSize = order * order;
        double[] P = new double[matSize];
        for (int i = 0; i < order; i++)
        {
            P[(i * order) + i] = delta;
        }

        // Temporary buffers for Px and k
        double[] px = new double[order];
        double[] k = new double[order];

        double lastValid = 0;
        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid))
            {
                lastValid = 0;
            }
        }

        for (int n = 0; n < source.Length; n++)
        {
            double val = source[n];
            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            ring.Add(val, true);

            if (ring.Count <= order)
            {
                output[n] = val;
                continue;
            }

            // Step 1: Prediction y = w^T * x
            double y = 0.0;
            for (int i = 0; i < order; i++)
            {
                double xi = ring[^(i + 2)];
                y = Math.FusedMultiplyAdd(w[i], xi, y);
            }

            // Step 2: Px = P * x
            for (int i = 0; i < order; i++)
            {
                double rowSum = 0.0;
                int rowBase = i * order;
                for (int j = 0; j < order; j++)
                {
                    double xj = ring[^(j + 2)];
                    rowSum = Math.FusedMultiplyAdd(P[rowBase + j], xj, rowSum);
                }
                px[i] = rowSum;
            }

            // Step 3: denom = λ + x^T * Px
            double denom = lambda;
            for (int i = 0; i < order; i++)
            {
                double xi = ring[^(i + 2)];
                denom = Math.FusedMultiplyAdd(xi, px[i], denom);
            }

            // Step 4: k = Px / denom
            double invDenom = denom > Epsilon ? 1.0 / denom : 0.0;
            for (int i = 0; i < order; i++)
            {
                k[i] = px[i] * invDenom;
            }

            // Step 5: w = w + k * error
            double error = val - y;
            for (int i = 0; i < order; i++)
            {
                w[i] = Math.FusedMultiplyAdd(k[i], error, w[i]);
            }

            // Step 6: P = (1/λ)(P - k * Px^T)
            for (int i = 0; i < order; i++)
            {
                double ki = k[i];
                int rowBase = i * order;
                for (int j = 0; j < order; j++)
                {
                    P[rowBase + j] = invLambda * (P[rowBase + j] - (ki * px[j]));
                }
            }

            output[n] = y;
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
        _p_state = default;
        _inputBuffer.Clear();
        Array.Clear(_weights);
        Array.Clear(_p_weights);

        // Reset P to delta * I
        const double delta = 100.0;
        Array.Clear(_P);
        Array.Clear(_p_P);
        for (int i = 0; i < _order; i++)
        {
            _P[(i * _order) + i] = delta;
        }

        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Rls Indicator) Calculate(TSeries source,
        int order = 16, double lambda = 0.99)
    {
        var indicator = new Rls(order, lambda);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
            _handler = null;
        }
        base.Dispose(disposing);
    }
}
