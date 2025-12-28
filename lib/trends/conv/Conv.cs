using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Convolution Indicator
/// </summary>
/// <remarks>
/// Applies a custom kernel (weights) to the data window.
/// The kernel is applied such that kernel[0] multiplies the oldest data point in the window,
/// and kernel[n-1] multiplies the newest data point.
///
/// Calculation:
/// Result = Sum(kernel[i] * data[i]) for i = 0 to n-1
///
/// Complexity:
/// Update: O(K) where K is kernel length.
///
/// IMPORTANT: This class implements IDisposable. When using the constructor with ITValuePublisher,
/// you MUST dispose the instance to unsubscribe from the source event and prevent memory leaks.
/// </remarks>
[SkipLocalsInit]
public sealed class Conv : AbstractBase, IDisposable
{
    private readonly int _period;
    private readonly double[] _kernel;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _subHandler;

    private record struct State(double LastValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _buffer.IsFull;

    public Conv(double[] kernel)
    {
        if (kernel == null || kernel.Length == 0)
            throw new ArgumentException("Kernel must not be empty", nameof(kernel));

        _period = kernel.Length;
        _kernel = new double[_period];
        Array.Copy(kernel, _kernel, _period);
        _buffer = new RingBuffer(_period);
        Name = $"Conv({_period})";
        WarmupPeriod = _period;
        _state.LastValidValue = double.NaN;
        _p_state.LastValidValue = double.NaN;
    }

    public Conv(ITValuePublisher source, double[] kernel) : this(kernel)
    {
        _source = source;
        _subHandler = Handle;
        _source.Pub += _subHandler;
    }

    public void Dispose()
    {
        if (_source != null && _subHandler != null)
        {
            _source.Pub -= _subHandler;
        }
    }

    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);

        if (isNew)
        {
            _buffer.Add(val);
        }
        else
        {
            _buffer.UpdateNewest(val);
        }

        double result = 0;
        if (_buffer.Count > 0)
        {
            int count = _buffer.Count;
            int kernelOffset = _period - count;
            ReadOnlySpan<double> kernelSpan = _kernel.AsSpan()[kernelOffset..];
            ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;

            if (count < _period)
            {
                result = internalBuf[..count].DotProduct(kernelSpan);
            }
            else
            {
                // Full: data is split at StartIndex (which points to oldest)
                int head = _buffer.StartIndex;
                int part1Len = _period - head;
                result = internalBuf.Slice(head, part1Len).DotProduct(kernelSpan[..part1Len])
                       + internalBuf[..head].DotProduct(kernelSpan[part1Len..]);
            }
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);
        var sourceValues = source.Values;

        Batch(sourceValues, vSpan, _kernel);

        // Restore state
        // We need to replay the last few updates to restore _buffer and _lastValidValue
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Find last valid value before the window if possible
        if (startIndex > 0)
        {
            _state.LastValidValue = double.NaN;
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(sourceValues[i]))
                {
                    _state.LastValidValue = sourceValues[i];
                    break;
                }
            }
        }
        else
        {
            _state.LastValidValue = double.NaN;
        }

        _buffer.Clear();

        // Replay
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(sourceValues[i]);
            _buffer.Add(val);
        }

        // Set Last
        Last = new TValue(source.Times[len - 1], vSpan[len - 1]);

        // Save state for isNew=false
        _p_state = _state;

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, double[] kernel)
    {
        var conv = new Conv(kernel);
        return conv.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double[] kernel)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (kernel == null || kernel.Length == 0)
            throw new ArgumentException("Kernel must not be empty", nameof(kernel));

        int len = source.Length;
        int period = kernel.Length;
        if (len == 0) return;

        // Use stackalloc for small kernels to avoid heap allocation
        Span<double> window = period <= 256 ? stackalloc double[period] : new double[period];

        double lastValid = double.NaN;
        int windowIdx = 0; // Points to where the NEXT value goes (circular)
        int count = 0;

        ReadOnlySpan<double> kernelSpan = kernel.AsSpan();

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

            window[windowIdx] = val;
            windowIdx = (windowIdx + 1);
            if (windowIdx >= period) windowIdx = 0;

            if (count < period) count++;

            double sum = 0;

            if (count < period)
            {
                int kernelOffset = period - count;
                // Window is [0..count-1]
                sum = window[..count].DotProduct(kernelSpan[kernelOffset..]);
            }
            else
            {
                // Full buffer - branchless version
                int part1Len = period - windowIdx;
                sum = window.Slice(windowIdx, part1Len).DotProduct(kernelSpan[..part1Len])
                    + window[..windowIdx].DotProduct(kernelSpan[part1Len..]);
            }

            output[i] = sum;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state.LastValidValue = double.NaN;
        _p_state.LastValidValue = double.NaN;
        Last = default;
    }
}
