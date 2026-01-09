using System.Runtime.CompilerServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Sgf : AbstractBase
{
    private readonly int _period;
    private readonly int _polyOrder;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    public Sgf(int period, int polyOrder = 2)
    {
        _period = (period % 2 == 0) ? period - 1 : period;
        _period = Math.Max(1, _period);

        if (polyOrder >= _period)
        {
            throw new ArgumentException("Polynomial order must be less than window size", nameof(polyOrder));
        }

        _polyOrder = polyOrder;
        _weights = new double[_period];
        _buffer = new RingBuffer(_period);
        Name = $"Sgf({_period},{_polyOrder})";
        CalculateWeights();
        WarmupPeriod = _period;

        Init();
    }

    public Sgf(TSeries source, int period, int polyOrder = 2) : this(period, polyOrder)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _buffer.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateWeights()
    {
        int halfWindow = _period / 2;
        double sumDenom = 0.0;

        for (int i = 0; i < _period; i++)
        {
            int k = i - halfWindow;
            double weight = 0;
            if (_polyOrder == 2)
            {
                weight = 3.0 * (3.0 * _period * _period - 7.0 - 20.0 * k * k);
            }
            else if (_polyOrder == 4)
            {
                double k2 = k * k;
                weight = 15.0 + k2 * (-20.0 + k2 * 6.0);
            }
            else
            {
                weight = 1.0 - Math.Abs((double)k) / (double)halfWindow;
            }

            _weights[i] = weight;
            sumDenom += weight;
        }

        if (Math.Abs(sumDenom) > double.Epsilon)
        {
            for (int i = 0; i < _period; i++)
            {
                _weights[i] /= sumDenom;
            }
        }
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _buffer.Add(input.Value, isNew: true);
        }
        else
        {
            _buffer.UpdateNewest(input.Value);
        }

        double result = 0;
        if (!_buffer.IsFull)
        {
            // Partial convolution logic to match Gauss/Pine behavior for insufficient data
            double wSum = 0;
            int count = _buffer.Count;
            // Map buffer to end of weights: _buffer[0] (oldest available) -> _weights[_period - count]

            for (int i = 0; i < count; i++)
            {
                double val = _buffer[i];
                if (!double.IsNaN(val))
                {
                    int weightIdx = _period - count + i;
                    double w = _weights[weightIdx];
                    result += val * w;
                    wSum += w;
                }
            }

            if (wSum > double.Epsilon)
                result /= wSum;
            else
                result = input.Value;
        }
        else
        {
            // Full kernel convolution with NaN handling
            double wSum = 0;
            for (int i = 0; i < _period; i++)
            {
                double val = _buffer[i];
                if (!double.IsNaN(val))
                {
                    double w = _weights[i];
                    result += val * w;
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

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);

        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        var resultValues = new double[source.Count];
        Calculate(source.Values, resultValues, _period, _polyOrder);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Restore state
        int startup = Math.Max(0, source.Count - _period);
        Reset();
        for (int i = startup; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }

        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int polyOrder = 2)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        int adjPeriod = (period % 2 == 0) ? period - 1 : period;
        adjPeriod = Math.Max(1, adjPeriod);

        if (polyOrder >= adjPeriod)
        {
            throw new ArgumentException("Polynomial order must be less than window size", nameof(polyOrder));
        }

        // Precompute weights
        Span<double> weights = stackalloc double[adjPeriod];
        CalculateWeightsSpan(weights, adjPeriod, polyOrder);

        // Apply filter
        // Optimized loop
        for (int i = 0; i < source.Length; i++)
        {
            double result = 0;
            double wSum = 0;

            int count = Math.Min(i + 1, adjPeriod);

            // Loop over available data for this point
            for (int j = 0; j < count; j++)
            {
                // Align to end of weights buffer
                int srcIdx = i - (count - 1) + j;
                double val = source[srcIdx];

                if (!double.IsNaN(val))
                {
                    int weightIdx = adjPeriod - count + j;
                    double w = weights[weightIdx];
                    result += val * w;
                    wSum += w;
                }
            }

            if (wSum > double.Epsilon)
            {
                output[i] = result / wSum;
            }
            else
            {
                // If wSum is zero/negative/small
                if (count < adjPeriod)
                    output[i] = source[i]; // Pass through for partial window
                else
                    output[i] = double.NaN;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateWeightsSpan(Span<double> weights, int period, int polyOrder)
    {
        int halfWindow = period / 2;
        double sumDenom = 0.0;

        for (int i = 0; i < period; i++)
        {
            int k = i - halfWindow;
            double weight = 0;
            if (polyOrder == 2)
            {
                weight = 3.0 * (3.0 * period * period - 7.0 - 20.0 * k * k);
            }
            else if (polyOrder == 4)
            {
                double k2 = k * k;
                weight = 15.0 + k2 * (-20.0 + k2 * 6.0);
            }
            else
            {
                weight = 1.0 - Math.Abs((double)k) / (double)halfWindow;
            }

            weights[i] = weight;
            sumDenom += weight;
        }

        if (Math.Abs(sumDenom) > double.Epsilon)
        {
            for (int i = 0; i < period; i++)
            {
                weights[i] /= sumDenom;
            }
        }
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
