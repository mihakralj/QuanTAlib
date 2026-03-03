using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Gaussian Filter: A noise reduction filter that uses a Gaussian kernel for smoothing.
/// Compared to SMA or EMA, it offers better smoothness with theoretically infinite support,
/// truncated here to +/- 3 standard deviations.
/// </summary>
/// <remarks>
/// The kernel size is determined by 2 * ceil(3 * sigma) + 1.
/// Weights are precomputed and normalized.
/// </remarks>
[SkipLocalsInit]
public sealed class Gauss : AbstractBase
{
    private readonly double _sigma;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double LastValue;
        public bool IsHot;
    }

    /// <summary>
    /// Gets the kernel size used for the filter.
    /// </summary>
    public int KernelSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Gauss"/> class.
    /// </summary>
    /// <param name="sigma">Standard deviation of the Gaussian kernel. Controls smoothness. Default is 1.0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when sigma is less than or equal to 0.</exception>
    public Gauss(double sigma = 1.0)
    {
        if (sigma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma), "Sigma must be greater than 0.");
        }

        _sigma = sigma;
        KernelSize = (int)(2 * Math.Ceiling(3.0 * sigma) + 1);
        WarmupPeriod = KernelSize;
        Name = $"Gauss({sigma:F2})";
        _buffer = new RingBuffer(KernelSize);
        _weights = new double[KernelSize];

        GenerateKernel();
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Gauss"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="sigma">Standard deviation of the Gaussian kernel.</param>
    public Gauss(ITValuePublisher source, double sigma = 1.0) : this(sigma)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State();
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GenerateKernel()
    {
        double sum = 0;
        int center = KernelSize / 2;
        double twoSigmaSq = 2.0 * _sigma * _sigma;

        for (int i = 0; i < KernelSize; i++)
        {
            double x = i - center;
            double weight = Math.Exp(-(x * x) / twoSigmaSq);
            _weights[i] = weight;
            sum += weight;
        }

        double invSum = 1.0 / sum;
        for (int i = 0; i < KernelSize; i++)
        {
            _weights[i] *= invSum;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
        _buffer.Clear();
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _buffer.Add(input.Value);
        }
        else
        {
            _state = _p_state;
            _buffer.UpdateNewest(input.Value);
        }

        double result = 0;
        if (!_buffer.IsFull)
        {
            double wSum = 0;
            int count = _buffer.Count;

            for (int i = 0; i < count; i++)
            {
                double val = _buffer[i];
                if (!double.IsNaN(val))
                {
                    int weightIdx = KernelSize - count + i;
                    double w = _weights[weightIdx];
                    result += val * w;
                    wSum += w;
                }
            }

            if (wSum > 0)
            {
                result /= wSum;
            }
            else
            {
                result = input.Value;
            }
        }
        else
        {
            double wSum = 0;
            for (int i = 0; i < KernelSize; i++)
            {
                double val = _buffer[i];
                if (!double.IsNaN(val))
                {
                    double w = _weights[i];
                    result = Math.FusedMultiplyAdd(val, w, result);
                    wSum += w;
                }
            }

            if (wSum > double.Epsilon && wSum < 0.999999)
            {
                result /= wSum;
            }
            else if (wSum <= double.Epsilon)
            {
                result = double.NaN;
            }
        }

        _state.IsHot = IsHot;
        _state.LastValue = result;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);

        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        // Calculate using static method for performance
        var resultValues = new double[source.Count];
        Batch(source.Values, resultValues, _sigma);

        // Convert to TSeries
        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        int startup = Math.Max(0, source.Count - KernelSize);
        Reset();
        for (int i = startup; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }

        return result;
    }

    public static TSeries Batch(TSeries source, double sigma = 1.0)
    {
        var indicator = new Gauss(sigma);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static calculation of Gaussian Filter on a span.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="sigma">Standard deviation</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double sigma)
    {
        const int StackallocThreshold = 256;

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        int kernelSize = (int)(2 * Math.Ceiling(3.0 * sigma) + 1);

        // Use stackalloc for small kernels, ArrayPool for large ones to avoid stack overflow
        double[]? rented = null;
        scoped Span<double> weights;
        scoped Span<double> stackBuffer = stackalloc double[Math.Min(kernelSize, StackallocThreshold)];

        if (kernelSize <= StackallocThreshold)
        {
            weights = stackBuffer.Slice(0, kernelSize);
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(kernelSize);
            weights = rented.AsSpan(0, kernelSize);
        }

        try
        {
            double sum = 0;
            int center = kernelSize / 2;
            double twoSigmaSq = 2.0 * sigma * sigma;

            for (int i = 0; i < kernelSize; i++)
            {
                double x = i - center;
                double weight = Math.Exp(-(x * x) / twoSigmaSq);
                weights[i] = weight;
                sum += weight;
            }

            double invSum = 1.0 / sum;
            for (int i = 0; i < kernelSize; i++)
            {
                weights[i] *= invSum;
            }

           // Apply filter
           for (int i = 0; i < source.Length; i++)
           {
               double result = 0;
               double wSum = 0;

               // This loop logic matches the RingBuffer partial fill logic.
               // If i < kernelSize, we don't have enough history.
               // The available history is source[0]...source[i].
               // This history maps to the END of the kernel weights.
               // E.g. if we have only 1 item (index i=0), it corresponds to weights[kernelSize-1].

               int count = Math.Min(i + 1, kernelSize);

               for (int j = 0; j < count; j++)
               {
                   // Source index: i - (count - 1) + j
                   // if j=0, source index is i - count + 1.
                   // if count=kernelSize, init index is i - kernelSize + 1.
                   // if count=1 (i=0), init index is 0.

                   int srcIdx = i - (count - 1) + j;
                   double val = source[srcIdx];

                   if (!double.IsNaN(val))
                   {
                       // Weight index:
                       // If full buffer, we use full weights 0..kernelSize-1.
                       // If partial, we align to end of weights.
                       // j=0 (oldest available) -> weights[kernelSize - count]
                       int weightIdx = kernelSize - count + j;

                       double w = weights[weightIdx];
                       result += val * w;
                       wSum += w;
                   }
               }

               if (wSum > 0)
               {
                   output[i] = result / wSum;
               }
               else
               {
                   output[i] = double.NaN;
               }
           }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented, clearArray: false);
            }
        }
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    public static (TSeries Results, Gauss Indicator) Calculate(TSeries source, double sigma = 1.0)
    {
        var indicator = new Gauss(sigma);
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
