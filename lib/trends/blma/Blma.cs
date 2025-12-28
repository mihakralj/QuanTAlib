using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace QuanTAlib;

public sealed class Blma : AbstractBase, IDisposable
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _publisher;
    private bool _hasLast;

    public override bool IsHot => _buffer.Count >= _period;

    public Blma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        }

        _period = period;
        Name = $"Blma({period})";
        WarmupPeriod = period;
        _buffer = new RingBuffer(period);
        _weights = new double[period];
        
        // Pre-calculate weights for the full period
        _weightSum = CalculateWeights(period, _weights);
        _handler = Handle;
    }

    public Blma(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    public void Dispose()
    {
        if (_publisher != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
        }
    }

    private void Handle(object? sender, TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _hasLast = false;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        DateTime time = DateTime.UtcNow;
        foreach (var value in source)
        {
            Update(new TValue(time, value));
            time = time.AddMilliseconds(1);
        }
    }

    public void Prime(ReadOnlySpan<TValue> source)
    {
        foreach (var value in source)
        {
            Update(value);
        }
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            return _hasLast ? Last : default;
        }

        _buffer.Add(input.Value, isNew);

        double result;
        if (_buffer.Count < _period)
        {
            // During warmup, calculate weights dynamically for the current count
            int count = _buffer.Count;
            if (count == 1)
            {
                result = input.Value;
            }
            else
            {
                Span<double> currentWeights = stackalloc double[count];
                double currentWeightSum = CalculateWeights(count, currentWeights);
                
                // Fallback for cases where weights sum to zero (e.g. N=2)
                result = Math.Abs(currentWeightSum) < double.Epsilon
                    ? _buffer.Average()
                    : CalculateWeightedSum(_buffer, currentWeights) / currentWeightSum;
            }
        }
        else
        {
            // Full period, use pre-calculated weights
            // Fallback for cases where weights sum to zero (e.g. N=2)
            result = Math.Abs(_weightSum) < double.Epsilon
                ? _buffer.Average()
                : CalculateWeightedSum(_buffer, _weights) / _weightSum;
        }

        var tValue = new TValue(input.Time, result);
        Last = tValue;
        _hasLast = true;
        PubEvent(tValue, isNew);
        return tValue;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        Span<double> output = new double[source.Count];
        Calculate(source.Values, output, _period);
        
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(source[i].Time, output[i]));
        }

        // Restore state by replaying last Period bars
        // This ensures the indicator is ready for subsequent streaming updates
        Reset();
        int start = Math.Max(0, source.Count - _period);
        for (int i = start; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return result;
    }

    private static double CalculateWeights(int n, Span<double> weights)
    {
        if (n == 1)
        {
            weights[0] = 1.0;
            return 1.0;
        }

        double totalWeight = 0;
        double invNMinus1 = 1.0 / (n - 1);
        double pi2 = 2.0 * Math.PI;
        double pi4 = 4.0 * Math.PI;

        // Blackman window coefficients
        const double a0 = 0.42;
        const double a1 = 0.5;
        const double a2 = 0.08;

        for (int i = 0; i < n; i++)
        {
            double ratio = i * invNMinus1;
            double w = a0 - (a1 * Math.Cos(pi2 * ratio)) + (a2 * Math.Cos(pi4 * ratio));
            weights[i] = w;
            totalWeight += w;
        }

        return totalWeight;
    }

    private static double CalculateWeightedSum(RingBuffer buffer, ReadOnlySpan<double> weights)
    {
        int start = buffer.StartIndex;
        int count = buffer.Count;
        int capacity = buffer.Capacity;
        
        if (start + count <= capacity)
        {
            return buffer.InternalBuffer.Slice(start, count).DotProduct(weights);
        }

        int firstPartLength = capacity - start;
        int secondPartLength = count - firstPartLength;

        double sum1 = buffer.InternalBuffer.Slice(start, firstPartLength).DotProduct(weights[..firstPartLength]);
        double sum2 = buffer.InternalBuffer.Slice(0, secondPartLength).DotProduct(weights[firstPartLength..]);

        return sum1 + sum2;
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), $"Destination length must be at least {source.Length}.");
        }

        // Pre-calculate weights for full period
        Span<double> weights = period <= 256 ? stackalloc double[period] : new double[period];
        double weightSum = CalculateWeights(period, weights);

        // Buffer for warmup weights to avoid stackalloc in loop
        Span<double> warmupWeightsBuffer = period <= 256 ? stackalloc double[period] : new double[period];

        for (int i = 0; i < source.Length; i++)
        {
            int count = Math.Min(i + 1, period);
            
            if (count < period)
            {
                // Warmup: dynamic weights
                if (count == 1)
                {
                    destination[i] = source[i];
                }
                else
                {
                    Span<double> currentWeights = warmupWeightsBuffer.Slice(0, count);
                    double currentWeightSum = CalculateWeights(count, currentWeights);
                    
                    if (Math.Abs(currentWeightSum) < double.Epsilon)
                    {
                        // Fallback for zero sum weights (e.g. N=2)
                        double sum = 0;
                        for (int j = 0; j < count; j++)
                        {
                            sum += source[i - count + 1 + j];
                        }
                        destination[i] = sum / count;
                    }
                    else
                    {
                        double sum = source.Slice(i - count + 1, count).DotProduct(currentWeights);
                        destination[i] = sum / currentWeightSum;
                    }
                }
            }
            else
            {
                // Full period
                if (Math.Abs(weightSum) < double.Epsilon)
                {
                    // Fallback for zero sum weights (e.g. N=2)
                    double sum = 0;
                    for (int j = 0; j < period; j++)
                    {
                        sum += source[i - period + 1 + j];
                    }
                    destination[i] = sum / period;
                }
                else
                {
                    double sum = source.Slice(i - period + 1, period).DotProduct(weights);
                    destination[i] = sum / weightSum;
                }
            }
        }
    }
}
