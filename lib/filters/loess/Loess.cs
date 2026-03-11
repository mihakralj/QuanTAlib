using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LOESS Filter: Locally Estimated Scatterplot Smoothing.
/// A non-parametric regression method that combines multiple regression models in a k-nearest-neighbor-based meta-model.
/// This implementation performs a locally weighted linear regression on a sliding window to produce a smoothed value.
/// </summary>
/// <remarks>
/// The filter estimates the value at the end of the window (causal LOESS).
/// It uses a tricube weight function w(x) = (1 - |x|^3)^3.
/// Computation is optimized by precalculating the linear regression coefficients into a fixed convolution kernel.
/// Implementation uses SIMD-optimized dot product with a pre-calculated kernel.
/// Period is automatically adjusted to the next odd number to ensure a symmetric window.
/// </remarks>
[SkipLocalsInit]
public sealed class Loess : AbstractBase
{
    private readonly double[] _kernel;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private Snapshot _snap;
    private Snapshot _pSnap;

    [StructLayout(LayoutKind.Sequential)]
    private record struct Snapshot
    {
        public double LastOutput;
        public double LastFiniteInput;
        public bool HasFiniteInput;
    }

    /// <summary>
    /// Gets the period of the filter.
    /// </summary>
    public int Period { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Loess"/> class.
    /// </summary>
    /// <param name="period">The window size for the local regression. Minimum 3. Even numbers are rounded up.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 3.</exception>
    public Loess(int period)
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 3.");
        }

        Period = (period & 1) == 0 ? period + 1 : period;

        WarmupPeriod = Period;
        Name = $"Loess({Period})";

        _buffer = new RingBuffer(Period);
        _kernel = new double[Period];

        GenerateKernelOldestFirst(Period, _kernel);

        Reset();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Loess"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="period">The window size for the local regression.</param>
    public Loess(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _buffer.Clear();
        _snap = new Snapshot();
        _pSnap = _snap;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _pSnap = _snap;
        }
        else
        {
            _snap = _pSnap;
        }

        // Feature: Robust NaN handling
        // If input is invalid, use the last known valid input (finite).
        // This prevents the regression from exploding or propagating NaNs aggressively.
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            if (_snap.HasFiniteInput)
            {
                val = _snap.LastFiniteInput;
            }
            else
            {
                val = 0.0; // Fallback if we have no history
            }
        }
        else
        {
            _snap.LastFiniteInput = val;
            _snap.HasFiniteInput = true;
        }

        // Add to buffer
        _buffer.Add(val, isNew);

        double y;
        if (!_buffer.IsFull)
        {
            // During warmup, pass through the input (or could attempt partial regression, but pass-through is safer/standard)
            y = val;
        }
        else
        {
            // Convolution with precomputed kernel
            // _buffer parts are [Oldest -> Newest]
            // _kernel is [Oldest -> Newest]
            // Result = DotProduct

            _buffer.GetSequencedSpans(out var span1, out var span2);

            y = DotProduct(span1, _kernel.AsSpan(0, span1.Length));
            if (span2.Length > 0)
            {
                y += DotProduct(span2, _kernel.AsSpan(span1.Length));
            }
        }

        _snap.LastOutput = y;
        Last = new TValue(input.Time, y);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        // Use static Calculate for performance on the whole series
        var resultValues = new double[source.Count];
        Batch(source.Values, resultValues, Period);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        int startup = Math.Max(0, source.Count - Period);
        Reset();

        // Restore Snap history if possible
        if (startup > 0)
        {
            double lastFinite = 0;
            bool found = false;
            for (int k = startup - 1; k >= 0; k--)
            {
                if (double.IsFinite(source.Values[k]))
                {
                    lastFinite = source.Values[k];
                    found = true;
                    break;
                }
            }
            if (found) { _snap.LastFiniteInput = lastFinite; _snap.HasFiniteInput = true; _pSnap = _snap; }
        }

        for (int i = startup; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }

        return result;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Loess(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static stateless calculation optimized for SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 3.");
        }

        int adjPeriod = (period & 1) == 0 ? period + 1 : period;

        double[] kernel = new double[adjPeriod];
        GenerateKernelOldestFirst(adjPeriod, kernel);

        ReadOnlySpan<double> kSpan = new ReadOnlySpan<double>(kernel);

        for (int i = 0; i < source.Length; i++)
        {
            if (i < adjPeriod - 1)
            {
                output[i] = source[i];
                continue;
            }

            var window = source.Slice(i - adjPeriod + 1, adjPeriod);
            output[i] = DotProduct(window, kSpan);
        }
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateKernelOldestFirst(int period, double[] kernel)
    {
        int halfWindow = period / 2;
        double weightSum = 0;
        double xSum = 0;
        double x2Sum = 0;

        double bandwidth = Math.Max(1.0, halfWindow + 0.5);

        for (int i = 0; i < period; i++)
        {
            double dist = Math.Abs(i - halfWindow) / bandwidth;
            if (dist >= 1.0)
            {
                dist = 0.9999;
            }

            double t = 1.0 - dist * dist * dist;
            double w = t * t * t;

            double xi = i - halfWindow;

            weightSum += w;
            xSum += xi * w;
            x2Sum += xi * xi * w;
        }

        double delta = weightSum * x2Sum - xSum * xSum;
        if (Math.Abs(delta) < double.Epsilon)
        {
            delta = 1.0;
        }

        double targetX = -halfWindow;

        for (int i = 0; i < period; i++)
        {
            double dist = Math.Abs(i - halfWindow) / bandwidth;
            if (dist >= 1.0)
            {
                dist = 0.9999;
            }

            double t = 1.0 - dist * dist * dist;
            double w = t * t * t;
            double xi = i - halfWindow;

            double term1 = x2Sum - xi * xSum;
            double term2 = targetX * (xi * weightSum - xSum);

            double kValue = (w / delta) * (term1 + term2);

            kernel[period - 1 - i] = kValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProduct(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
        => a.DotProduct(b);

    public static (TSeries Results, Loess Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Loess(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
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
