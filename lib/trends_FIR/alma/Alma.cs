using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// ALMA: Arnaud Legoux Moving Average
/// </summary>
/// <remarks>
/// Gaussian-weighted FIR filter with configurable offset and sigma parameters.
/// The offset controls the peak of the Gaussian (0 = leftmost, 1 = rightmost).
/// The sigma controls the width of the Gaussian curve.
///
/// Calculation: <c>ALMA = Σ(w_i × P_i) / Σ(w_i)</c> where <c>w_i = exp(-((i - m)²) / (2s²))</c>,
/// <c>m = offset × (period - 1)</c>, <c>s = period / sigma</c>.
/// </remarks>
/// <seealso href="Alma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Alma : AbstractBase
{
    private readonly int _period;
    private readonly double _offset;
    private readonly double _sigma;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastInput, double LastValidValue, bool HasSeenValidData);
    private State _state;
    private State _pState;

    /// <summary>
    /// Default value to use for LastValidValue when no valid data has been seen yet.
    /// Defaults to double.NaN to avoid silently introducing zeros.
    /// </summary>
    public double DefaultLastValidValue { get; set; } = double.NaN;

    /// <summary>
    /// Initializes a new instance of the <see cref="Alma"/> class.
    /// </summary>
    /// <param name="period">The lookback window size. Must be greater than 0.</param>
    /// <param name="offset">The Gaussian peak offset (0.0 to 1.0). Default: 0.85.</param>
    /// <param name="sigma">The Gaussian width divisor. Default: 6.0.</param>
    public Alma(int period, double offset = 0.85, double sigma = 6.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (offset < 0.0 || offset > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be between 0.0 and 1.0");
        }
        if (sigma <= 0.0)
        {
            throw new ArgumentException("Sigma must be greater than 0", nameof(sigma));
        }

        _period = period;
        _offset = offset;
        _sigma = sigma;
        _buffer = new RingBuffer(period);
        _weights = ComputeNormalizedWeights(period, offset, sigma);
        Name = $"Alma({period},{offset:F2},{sigma:F1})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Alma"/> class with a source publisher.
    /// </summary>
    public Alma(ITValuePublisher source, int period, double offset = 0.85, double sigma = 6.0)
        : this(period, offset, sigma)
    {
        _source = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _handler != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    public override bool IsHot => _buffer.IsFull;
    public bool IsNew { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            _state.HasSeenValidData = true;
            return input;
        }
        return _state.HasSeenValidData ? _state.LastValidValue : DefaultLastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeightedSum()
    {
        double result = 0;
        int count = _buffer.Count;
        int weightOffset = _period - count;
        int idx = 0;

        foreach (double item in _buffer)
        {
            result = Math.FusedMultiplyAdd(_weights[weightOffset + idx], item, result);
            idx++;
        }

        // Normalize for partial windows
        if (count < _period)
        {
            double wSum = 0;
            for (int i = weightOffset; i < _period; i++)
            {
                wSum += _weights[i];
            }
            return wSum > 0 ? result / wSum : result;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        IsNew = isNew;
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            _buffer.Add(val);
            _state.LastInput = val;

            _pState = _state;
        }
        else
        {
            if (_buffer.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot call Update with isNew=false when buffer is empty. " +
                    "The first update must have isNew=true to initialize state.");
            }

            _state = _pState;
            double val = GetValidValue(input.Value);
            _buffer.UpdateNewest(val);
        }

        double result = CalculateWeightedSum();
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

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _offset, _sigma);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        int len = source.Length;
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Seed LastValidValue
        _state.LastValidValue = DefaultLastValidValue;
        _state.HasSeenValidData = false;
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    _state.HasSeenValidData = true;
                    break;
                }
            }
        }

        // Reset buffer and process window
        _buffer.Clear();
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
            _state.LastInput = val;
        }

        // Calculate Last
        double result = CalculateWeightedSum();
        Last = new TValue(DateTime.MinValue, result);

        _pState = _state;
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _pState = default;
        Last = default;
    }

    /// <summary>
    /// Computes ALMA for a TSeries using batch processing.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double offset = 0.85, double sigma = 6.0)
    {
        var alma = new Alma(period, offset, sigma);
        return alma.Update(source);
    }

    /// <summary>
    /// Computes ALMA for raw spans using batch processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period,
        double offset = 0.85, double sigma = 6.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (sigma <= 0.0)
        {
            throw new ArgumentException("Sigma must be greater than 0", nameof(sigma));
        }
        if (offset < 0.0 || offset > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be between 0.0 and 1.0");
        }
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period, offset, sigma);
    }

    /// <summary>
    /// Computes ALMA and returns both the result series and a warmed-up indicator instance.
    /// </summary>
    public static (TSeries Results, Alma Indicator) Calculate(TSeries source, int period,
        double offset = 0.85, double sigma = 6.0)
    {
        var indicator = new Alma(period, offset, sigma);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output,
        int period, double offset, double sigma)
    {
        int len = source.Length;
        double[] weights = ComputeNormalizedWeights(period, offset, sigma);
        double lastValid = double.NaN;

        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferCount = 0;
        int bufferIdx = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            // Add to circular buffer
            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
            {
                bufferIdx = 0;
            }
            if (bufferCount < period)
            {
                bufferCount++;
            }

            // Compute weighted sum
            double result = 0;
            int weightOffset = period - bufferCount;

            if (bufferCount == period)
            {
                // Full window — iterate from oldest to newest
                int readIdx = bufferIdx; // bufferIdx now points to oldest
                for (int k = 0; k < period; k++)
                {
                    result = Math.FusedMultiplyAdd(weights[k], buffer[readIdx], result);
                    readIdx++;
                    if (readIdx >= period)
                    {
                        readIdx = 0;
                    }
                }
            }
            else
            {
                // Partial window — use tail weights
                double wSum = 0;
                for (int k = 0; k < bufferCount; k++)
                {
                    int wi = weightOffset + k;
                    result = Math.FusedMultiplyAdd(weights[wi], buffer[k], result);
                    wSum += weights[wi];
                }
                result = wSum > 0 ? result / wSum : result;
            }

            output[i] = result;
        }
    }

    /// <summary>
    /// Pre-computes normalized Gaussian weights for the ALMA filter.
    /// Weights are normalized so that their sum equals 1.0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double[] ComputeNormalizedWeights(int period, double offset, double sigma)
    {
        double[] w = new double[period];
        double m = offset * (period - 1);
        double s = period / sigma;
        double s2 = 2.0 * s * s;
        double wSum = 0;

        for (int i = 0; i < period; i++)
        {
            double d = i - m;
            w[i] = Math.Exp(-(d * d) / s2);
            wSum += w[i];
        }

        // Normalize weights to sum to 1.0
        double invSum = 1.0 / wSum;
        for (int i = 0; i < period; i++)
        {
            w[i] *= invSum;
        }

        return w;
    }
}
