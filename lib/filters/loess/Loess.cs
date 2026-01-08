using System.Numerics;
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
    private readonly int _period;
    private readonly double[] _kernel; // Stored Oldest-First to match RingBuffer layout
    private readonly RingBuffer _buffer;

    private Snapshot _snap;
    private Snapshot _pSnap;

    [StructLayout(LayoutKind.Sequential)]
    private struct Snapshot
    {
        public double LastOutput;
        public double LastFiniteInput;
        public bool HasFiniteInput;
    }

    /// <summary>
    /// Gets the period of the filter.
    /// </summary>
    public int Period => _period;

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

        // Round UP to odd number to ensure symmetric window + center
        _period = (period & 1) == 0 ? period + 1 : period;
        
        WarmupPeriod = _period;
        Name = $"Loess({_period})";
        
        _buffer = new RingBuffer(_period);
        _kernel = new double[_period];
        
        // Generate kernel (weights for linear regression projection)
        GenerateKernelOldestFirst(_period, _kernel);
        
        Reset();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Loess"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="period">The window size for the local regression.</param>
    public Loess(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
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
        if (source.Count == 0) return new TSeries();

        // Use static Calculate for performance on the whole series
        var resultValues = new double[source.Count];
        Calculate(source.Values, resultValues, _period);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Restore state based on last window to ensure continuity
        // Re-run the last _period samples through the instance to sync state
        // (This is less efficient than manual state setting but safer for correctness)
        int startup = Math.Max(0, source.Count - _period);
        Reset();
        
        // Restore Snap history if possible
        if (startup > 0)
        {
             // Try to find a finite value before startup to init LastFiniteInput
             // Just a heuristic:
             double lastFinite = 0; 
             bool found = false;
             for(int k=startup-1; k>=0; k--)
             {
                 if (double.IsFinite(source.Values[k])) { lastFinite = source.Values[k]; found=true; break; }
             }
             if(found) { _snap.LastFiniteInput = lastFinite; _snap.HasFiniteInput = true; _pSnap = _snap; }
        }

        for (int i = startup; i < source.Count; i++)
        {
            Update(source[i], true);
        }

        return result;
    }
    
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateKernelOldestFirst(int period, double[] kernel)
    {
        // Kernel Generation Logic:
        // We want to estimate y at x_target.
        // x coordinates are relative to the window.
        // Let i be the index in the "Newest-First" concept (i=0 is Newest, i=period-1 is Oldest).
        // Then we map to the kernel array: kernel[period - 1 - i] = calculated_weight(i)
        // This ensures the stored kernel is Oldest-First.
        
        int halfWindow = period / 2;
        double weightSum = 0;
        double xSum = 0;
        double x2Sum = 0;
        
        // Use arrays to store temporaries to calculate Delta first
        // Or loop twice. Loop twice is fine for init.
        
        // Ensure bandwidth covers the full window without zeroing edges
        double bandwidth = Math.Max(1.0, halfWindow + 0.5);

        // Pass 1: Compute sums for Cramer's rule
        for (int i = 0; i < period; i++)
        {
            // i=0 is Newest. i=Period-1 is Oldest.
            // Center is at i = halfWindow?
            // "Newest at -halfWindow, Oldest at +halfWindow" -> implies center is middle of window in time
            
            double dist = Math.Abs(i - halfWindow) / bandwidth;
            if (dist >= 1.0) dist = 0.9999;

            // Tricube weight
            double t = 1.0 - dist * dist * dist;
            double w = t * t * t;

            // x coordinate: Newest (i=0) is -halfWindow
            double xi = i - halfWindow;

            weightSum += w;
            xSum += xi * w;
            x2Sum += xi * xi * w;
        }

        double delta = weightSum * x2Sum - xSum * xSum;
        if (Math.Abs(delta) < double.Epsilon) delta = 1.0;
        
        double targetX = -halfWindow; // We estimate at Newest

        // Pass 2: Calculate Kernel Weights
        for (int i = 0; i < period; i++)
        {
            double dist = Math.Abs(i - halfWindow) / bandwidth;
            if (dist >= 1.0) dist = 0.9999;
            double t = 1.0 - dist * dist * dist;
            double w = t * t * t;
            double xi = i - halfWindow;
            
            double term1 = x2Sum - xi * xSum;
            double term2 = targetX * (xi * weightSum - xSum);
            
            double kValue = (w / delta) * (term1 + term2);
            
            // Store in Oldest-First order
            // i=0 (Newest) -> goes to end of array (Index Length-1)
            // i=Period-1 (Oldest) -> goes to start of array (Index 0)
            kernel[period - 1 - i] = kValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProduct(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        // a and b expected to be same length (slice called in Update ensures this)
        int length = a.Length;
        if (length == 0) return 0;
        
        int i = 0;
        double sum = 0;

        if (Vector.IsHardwareAccelerated && length >= Vector<double>.Count)
        {
            var vSum = Vector<double>.Zero;
            ref double rA = ref MemoryMarshal.GetReference(a);
            ref double rB = ref MemoryMarshal.GetReference(b);
            
            int vectorCount = Vector<double>.Count;
            int limit = length - vectorCount;

            for (; i <= limit; i += vectorCount)
            {
                var vA = Vector.LoadUnsafe(ref rA, (nuint)i);
                var vB = Vector.LoadUnsafe(ref rB, (nuint)i);
                vSum += vA * vB;
            }

            // Reduce vector sum
            for (int j = 0; j < vectorCount; j++)
            {
                sum += vSum[j];
            }
        }

        // Remainder
        for (; i < length; i++)
        {
            sum += a[i] * b[i];
        }
        
        return sum;
    }

    /// <summary>
    /// Static stateless calculation optimized for SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));

        if (period < 3) 
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 3.");

        int adjPeriod = (period & 1) == 0 ? period + 1 : period;
        
        // Generate kernel (using same logic as instance)
        // Since we slide over contiguous source (Oldest->Newest naturally in array), we need Kernel Oldest-First.
        // But in convolution: Result[t] = Sum(Source[t-k] * Kernel[?])
        // Source[t] is Newest. Source[t - (period-1)] is Oldest.
        // Instance Kernel is [Oldest -> Newest].
        // If we use DotProduct(Source[t-(P-1) .. t+1], Kernel), we are aligning Oldest with Oldest.
        // Correct.

        double[] kernel = new double[adjPeriod];
        GenerateKernelOldestFirst(adjPeriod, kernel);
        
        ReadOnlySpan<double> kSpan = new ReadOnlySpan<double>(kernel);

        for (int i = 0; i < source.Length; i++)
        {
            if (i < adjPeriod - 1)
            {
                // Not enough history
                output[i] = source[i];
                continue;
            }

            // Window: [i - (Period-1), ..., i]
            // This is length Period.
            // i-(Period-1) is Oldest. i is Newest.
            // Matches Kernel [Oldest ... Newest].
            
            var window = source.Slice(i - adjPeriod + 1, adjPeriod);
            output[i] = DotProduct(window, kSpan);
        }
    }
}