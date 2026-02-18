using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LMS: Least Mean Squares Adaptive Filter (Widrow-Hoff)
/// An adaptive FIR filter that adjusts its weight vector to predict the current
/// input from its recent history via the Normalized LMS (NLMS) update rule.
/// Converges to the optimal Wiener solution with O(order) per-bar complexity.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/lms.md
///
/// Key properties:
///   - Adaptive FIR: weight vector w[0..order-1] learns from streaming data
///   - Predicts src[0] from src[1]..src[order] (no look-ahead)
///   - NLMS normalization: mu_eff = mu / (eps + ||x||^2) for input-power-independent convergence
///   - Overlay indicator (price-following)
///   - O(order) per bar for both prediction and weight update
///
/// Complexity: O(order) per bar
/// </remarks>
[SkipLocalsInit]
public sealed class Lms : AbstractBase
{
    private const double Epsilon = 1e-10;

    private readonly int _order;
    private readonly double _mu;
    private readonly RingBuffer _inputBuffer;
    private readonly double[] _weights;
    private readonly double[] _p_weights;
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

    /// <summary>Learning rate (step size) controlling adaptation speed.</summary>
    public double Mu => _mu;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count > _order;

    public Lms(int order = 16, double mu = 0.5)
    {
        if (order < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be >= 2.");
        }

        if (mu <= 0.0 || mu >= 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mu), "Learning rate mu must be in (0, 2).");
        }

        _order = order;
        _mu = mu;
        Name = $"LMS({order},{mu:F2})";
        WarmupPeriod = order + 1;

        // Weight vector + snapshot for bar correction
        _weights = new double[order];
        _p_weights = new double[order];

        // Ring buffer holds order+1 values: current + order past values
        _inputBuffer = new RingBuffer(order + 1);
        _state.LastValid = double.NaN;
    }

    public Lms(ITValuePublisher source, int order = 16, double mu = 0.5)
        : this(order, mu)
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

        Batch(values, results, _order, _mu);

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
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_weights, _p_weights, _order);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_weights, _weights, _order);
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
            // Predict src[0] from src[1]..src[order]
            // buffer[^1] = newest (src[0]), buffer[^2] = src[1], etc.
            double y = 0.0;
            double normSq = 0.0;

            for (int i = 0; i < _order; i++)
            {
                double xi = _inputBuffer[^(i + 2)]; // src[i+1]
                y = Math.FusedMultiplyAdd(_weights[i], xi, y);
                normSq = Math.FusedMultiplyAdd(xi, xi, normSq);
            }

            // NLMS weight update — only learn from confirmed bars
            if (isNew)
            {
                double error = val - y;
                double muEff = _mu / (Epsilon + normSq);

                for (int i = 0; i < _order; i++)
                {
                    double xi = _inputBuffer[^(i + 2)];
                    _weights[i] = Math.FusedMultiplyAdd(muEff * error, xi, _weights[i]);
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

    public static TSeries Batch(TSeries source, int order = 16, double mu = 0.5)
    {
        var indicator = new Lms(order, mu);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int order = 16, double mu = 0.5)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        if (order < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be >= 2.");
        }

        if (mu <= 0.0 || mu >= 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mu), "Learning rate mu must be in (0, 2).");
        }

        // Local weight vector on heap (order can be large)
        double[] w = new double[order];
        var ring = new RingBuffer(order + 1);
        double lastValid = 0;

        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid))
            {
                lastValid = 0;
            }
        }

        for (int i = 0; i < source.Length; i++)
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

            ring.Add(val, true);

            if (ring.Count <= order)
            {
                // Pass through until we have enough history
                output[i] = val;
                continue;
            }

            // Predict current from past order values
            double y = 0.0;
            double normSq = 0.0;

            for (int j = 0; j < order; j++)
            {
                double xj = ring[^(j + 2)];
                y = Math.FusedMultiplyAdd(w[j], xj, y);
                normSq = Math.FusedMultiplyAdd(xj, xj, normSq);
            }

            double error = val - y;
            double muEff = mu / (Epsilon + normSq);

            for (int j = 0; j < order; j++)
            {
                double xj = ring[^(j + 2)];
                w[j] = Math.FusedMultiplyAdd(muEff * error, xj, w[j]);
            }

            output[i] = y;
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
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Lms Indicator) Calculate(TSeries source,
        int order = 16, double mu = 0.5)
    {
        var indicator = new Lms(order, mu);
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
