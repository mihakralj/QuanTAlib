using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Notch : AbstractBase
{
    private readonly double _b0, _b1, _b2, _a1, _a2;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private int _index;

    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    #pragma warning disable CA1066 // Implement IEquatable<T> because it overrides Equals
    private struct State
    {
        public double X1, X2, Y1, Y2;
        public double LastValue;
    }
    #pragma warning restore CA1066

    public int NotchFreq { get; }
    public double Bandwidth { get; }
    public override bool IsHot => _index >= WarmupPeriod;

    public Notch(int period, double q = 1.0)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        if (q <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(q), "Q factor must be positive.");
        }

        NotchFreq = period;
        Bandwidth = q;
        WarmupPeriod = period;
        Name = $"Notch({period},{q})";

        double omega = Math.PI * 2.0 / period;
        double sn = Math.Sin(omega);
        double cs = Math.Cos(omega);
        double alpha = sn / (2.0 * q);

        double a0 = 1.0 + alpha;
        double invA0 = 1.0 / a0;

        _b0 = invA0;
        _b1 = -2.0 * cs * invA0;
        _b2 = invA0;
        _a1 = -2.0 * cs * invA0;
        _a2 = (1.0 - alpha) * invA0;

        Init();
    }

    public Notch(TSeries source, int period, double q = 1.0) : this(period, q)
    {
        _publisher = source;
        _handler = Sub;
        source.Pub += _handler;
    }

    private void Sub(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public void Init()
    {
        _index = 0;
        _state = default;
        _p_state = default;
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _index++;
        }
        else
        {
            _state = _p_state;
        }

        double val = input.Value;
        if (double.IsNaN(val) || double.IsInfinity(val))
        {
            val = _state.LastValue;
        }

        // Fused Multiply Add: y = b0*x + b1*x1 + b2*x2 - a1*y1 - a2*y2
        double y = Math.FusedMultiplyAdd(_b0, val, 0);
        y = Math.FusedMultiplyAdd(_b1, _state.X1, y);
        y = Math.FusedMultiplyAdd(_b2, _state.X2, y);
        y = Math.FusedMultiplyAdd(-_a1, _state.Y1, y);
        y = Math.FusedMultiplyAdd(-_a2, _state.Y2, y);

        _state.X2 = _state.X1;
        _state.X1 = val;
        _state.Y2 = _state.Y1;
        _state.Y1 = y;

        if (!double.IsNaN(val) && !double.IsInfinity(val))
        {
            _state.LastValue = val;
        }

        TValue result = new TValue(input.Time, y);
        Last = result;
        PubEvent(result, isNew);
        return result;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Calculate(srcSpan, outArray.AsSpan(), NotchFreq, Bandwidth);

        for (int i = 0; i < outArray.Length; i++)
        {
            result.Add(new TValue(source.Times[i], outArray[i]));
        }

        if (srcSpan.Length > 0)
        {
            _index += srcSpan.Length;
            // Best effort state restoration from the end of the block
            // We assume the strict history for X is valid.
            double lastVal = srcSpan[srcSpan.Length - 1];
            _state.LastValue = lastVal;

            if (srcSpan.Length >= 2)
            {
                _state.X1 = srcSpan[srcSpan.Length - 1];
                _state.X2 = srcSpan[srcSpan.Length - 2];
                _state.Y1 = outArray[outArray.Length - 1];
                _state.Y2 = outArray[outArray.Length - 2];
            }
            else
            {
                _state.X2 = _state.X1;
                _state.X1 = srcSpan[0];
                _state.Y2 = _state.Y1;
                _state.Y1 = outArray[0];
            }
            _p_state = _state;
        }

        return result;
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, double q = 1.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output lengths must match.", nameof(output));
        }

        double omega = Math.PI * 2.0 / period;
        double sn = Math.Sin(omega);
        double cs = Math.Cos(omega);
        double alpha = sn / (2.0 * q);

        double a0 = 1.0 + alpha;
        double invA0 = 1.0 / a0;

        double b0 = invA0;
        double b1 = -2.0 * cs * invA0;
        double b2 = invA0;
        double a1 = -2.0 * cs * invA0;
        double a2 = (1.0 - alpha) * invA0;

        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        double lastVal = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsNaN(val) || double.IsInfinity(val))
            {
                val = lastVal;
            }
            else
            {
                lastVal = val;
            }

            double y = Math.FusedMultiplyAdd(b0, val, 0);
            y = Math.FusedMultiplyAdd(b1, x1, y);
            y = Math.FusedMultiplyAdd(b2, x2, y);
            y = Math.FusedMultiplyAdd(-a1, y1, y);
            y = Math.FusedMultiplyAdd(-a2, y2, y);

            output[i] = y;

            x2 = x1;
            x1 = val;
            y2 = y1;
            y1 = y;
        }
    }

    public static TSeries Calculate(TSeries source, int period, double q = 1.0)
    {
        var result = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Calculate(srcSpan, outArray.AsSpan(), period, q);

        for (int i = 0; i < outArray.Length; i++)
        {
            result.Add(new TValue(source.Times[i], outArray[i]));
        }

        return result;
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
