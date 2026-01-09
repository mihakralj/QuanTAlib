using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Kalman Filter
/// A 1D Kalman filter implementation for smoothing time series data.
/// It estimates the state of the system (price) by minimizing the mean squared error.
/// </summary>
/// <remarks>
/// The Kalman Filter is an optimal estimator that infers parameters of interest from inaccurate and uncertain observations.
/// In this implementation:
/// - State (x): Current price estimate
/// - Process Noise (q): Uncertainty in the process (how much the true price changes)
/// - Measurement Noise (r): Uncertainty in the measurement (how much noise is in the data)
///
/// Algorithm:
/// 1. Prediction:
///    $$ x_{pred} = x_{t-1} $$
///    $$ p_{pred} = p_{t-1} + q $$
///
/// 2. Update (Correction):
///    $$ k = \frac{p_{pred}}{p_{pred} + r} $$
///    $$ x_t = x_{pred} + k \cdot (measurement - x_{pred}) $$
///    $$ p_t = (1 - k) \cdot p_{pred} $$
///
/// Where:
/// - $x$ is the state estimate
/// - $p$ is the error covariance
/// - $k$ is the Kalman gain
///
/// Complexity: O(1)
/// </remarks>
[SkipLocalsInit]
public sealed class Kalman : AbstractBase
{
    private readonly double _q; // process noise variance
    private readonly double _r; // measurement noise variance
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private State _state;
    private State _pState;

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public double X;        // estimate
        public double P;        // error covariance
        public int Samples;     // 0 => uninitialized
    }

    /// <summary>
    /// Process noise covariance. Defaults to 0.01.
    /// Controls the assumption of how fast the system state changes.
    /// Higher values allow the filter to react faster to changes (less smoothing).
    /// </summary>
    public double Q => _q;

    /// <summary>
    /// Measurement noise covariance. Defaults to 0.1.
    /// Controls the assumption of how noisy the measurements are.
    /// Higher values make the filter trust the measurement less (more smoothing).
    /// </summary>
    public double R => _r;

    /// <summary>
    /// Initializes a new instance of the <see cref="Kalman"/> class.
    /// </summary>
    /// <param name="q">Process noise covariance. Default 0.01.</param>
    /// <param name="r">Measurement noise covariance. Default 0.1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when q or r are not positive.</exception>
    public Kalman(double q = 0.01, double r = 0.1)
    {
        if (q <= 0) throw new ArgumentOutOfRangeException(nameof(q), "q must be positive.");
        if (r <= 0) throw new ArgumentOutOfRangeException(nameof(r), "r must be positive.");

        _q = q;
        _r = r;

        Name = $"Kalman(q={q},r={r})";
        WarmupPeriod = 10; // Approximate convergence
        Reset();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Kalman"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="q">Process noise covariance.</param>
    /// <param name="r">Measurement noise covariance.</param>
    public Kalman(ITValuePublisher source, double q = 0.01, double r = 0.1) : this(q, r)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? _, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override bool IsHot => _state.Samples >= WarmupPeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = default;
        _pState = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // "rewind" behavior for isNew=false
        if (isNew) _pState = _state;
        else _state = _pState;

        double z = input.Value;

        // Treat NaN/Inf as missing measurement: do prediction only.
        if (!double.IsFinite(z))
        {
            if (_state.Samples == 0)
            {
                // No estimate yet: just echo the invalid measurement
                Last = new TValue(input.Time, z);
            }
            else
            {
                // Predict only: x unchanged for random walk, p grows
                _state.P += _q;
                Last = new TValue(input.Time, _state.X);
            }

            PubEvent(Last, isNew);
            return Last;
        }

        // First valid measurement initializes the filter.
        if (_state.Samples == 0)
        {
            _state.X = z;
            _state.P = _r;      // scale covariance to measurement noise (more consistent than 1.0)
            _state.Samples = 1;

            Last = new TValue(input.Time, _state.X);
            PubEvent(Last, isNew);
            return Last;
        }

        // Predict
        double pPred = _state.P + _q;

        // Update
        double denom = pPred + _r;
        double k = pPred / denom;

        // x = x + k * (z - x)  (use FMA when available)
        double x = _state.X;
        x = Math.FusedMultiplyAdd(k, z - x, x);
        _state.X = x;

        // p = (1 - k) * pPred  (scalar-stable equivalent form)
        _state.P = (pPred * _r) / denom;

        _state.Samples++;

        Last = new TValue(input.Time, _state.X);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        var output = new double[source.Count];

        // One pass. Also returns the ending state so we don't replay.
        Calculate(source.Values, output, _q, _r,
            out double endX, out double endP, out int endSamples);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
            result.Add(new TValue(times[i], output[i]));

        _state = new State { X = endX, P = endP, Samples = endSamples };
        _pState = _state;

        Last = new TValue(times[^1], output[^1]);
        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double v in source)
            Update(new TValue(DateTime.MinValue, v), true);
    }

    /// <summary>
    /// Batch KF. Returns final state so instance can restore without replay.
    /// NaN/Inf => prediction-only (hold x, p += q) once initialized.
    /// </summary>
    public static void Calculate(
        ReadOnlySpan<double> source,
        Span<double> output,
        double q,
        double r,
        out double endX,
        out double endP,
        out int endSamples)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must be same length.", nameof(output));

        double x = 0.0;
        double p = 0.0;
        int samples = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double z = source[i];

            if (!double.IsFinite(z))
            {
                if (samples == 0)
                {
                    output[i] = z; // still uninitialized
                }
                else
                {
                    p += q;        // predict-only
                    output[i] = x;
                }
                continue;
            }

            if (samples == 0)
            {
                x = z;
                p = r;
                samples = 1;
                output[i] = x;
                continue;
            }

            double pPred = p + q;
            double denom = pPred + r;
            double k = pPred / denom;

            x = Math.FusedMultiplyAdd(k, z - x, x);
            p = (pPred * r) / denom;

            samples++;
            output[i] = x;
        }

        endX = x;
        endP = p;
        endSamples = samples;
    }

    // Overload for Calculate without out params to maintain API compatibility if needed,
    // although the original Calculate signature was different anyway (returned void).
    // The previous implementation had: public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double q, double r)
    // We should keep this signature valid.
    
    /// <summary>
    /// Static calculation of Kalman Filter on a span.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double q, double r)
    {
        Calculate(source, output, q, r, out _, out _, out _);
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}