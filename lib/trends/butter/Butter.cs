using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

public sealed class Butter : AbstractBase
{
    private readonly int _period;
    private double _a1, _a2, _b0, _b1, _b2;
    private double _invA0;
    private State _state;
    private State _p_state;

    private record struct State
    {
        public double X1, X2;
        public double Y1, Y2;
        public int Count;
    }

    public override bool IsHot => _state.Count >= 2;

    public Butter(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        _period = period;
        CalculateCoefficients();
        Name = $"Butter({_period})";
        WarmupPeriod = 2;
        Init();
    }

    public Butter(object source, int period) : this(period)
    {
        var pub = (ITValuePublisher)source;
        pub.Pub += Handle;
    }

    private void Handle(TValue value)
    {
        Update(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateCoefficients()
    {
        double omega = 2.0 * Math.PI / _period;
        double sinOmega = Math.Sin(omega);
        double cosOmega = Math.Cos(omega);
        double alpha = sinOmega / Math.Sqrt(2.0);

        double a0 = 1.0 + alpha;
        _a1 = -2.0 * cosOmega;
        _a2 = 1.0 - alpha;
        
        _b0 = (1.0 - cosOmega) / 2.0;
        _b1 = 1.0 - cosOmega;
        _b2 = (1.0 - cosOmega) / 2.0;

        _invA0 = 1.0 / a0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State();
        _p_state = new State();
        Last = new TValue(0, double.NaN);
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
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

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            // Return Last (initialized to NaN) if no valid input has been seen yet
            return Last;
        }

        double x = input.Value;
        double y = _state.Count < 2
            ? x
            : (_b0 * x + _b1 * _state.X1 + _b2 * _state.X2 - _a1 * _state.Y1 - _a2 * _state.Y2) * _invA0;

        // Update state
        _state.X2 = _state.X1;
        _state.X1 = x;
        _state.Y2 = _state.Y1;
        _state.Y1 = y;
        
        if (_state.Count < 2)
        {
            _state.Count++;
        }

        var tValue = new TValue(input.Time, y);
        Last = tValue;
        PubEvent(tValue);
        return tValue;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        Span<double> output = new double[source.Count];
        Calculate(source.Values, output, _period, double.NaN);
        
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(source[i].Time, output[i]));
        }

        // Restore state
        Reset();
        
        // Replay a reasonable amount (e.g. 4*period) for convergence of IIR state.
        int replayCount = Math.Min(source.Count, 4 * _period);
        int start = source.Count - replayCount;
        
        for (int i = start; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return result;
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int period, double initialLast)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination span must have length >= source length.");
        }

        double omega = 2.0 * Math.PI / period;
        double sinOmega = Math.Sin(omega);
        double cosOmega = Math.Cos(omega);
        double alpha = sinOmega / Math.Sqrt(2.0);

        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cosOmega;
        double a2 = 1.0 - alpha;
        
        double b0 = (1.0 - cosOmega) / 2.0;
        double b1 = 1.0 - cosOmega;
        double b2 = (1.0 - cosOmega) / 2.0;

        double invA0 = 1.0 / a0;

        double x1 = 0, x2 = 0;
        double y1 = 0, y2 = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double x = source[i];
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                destination[i] = i > 0 ? destination[i - 1] : initialLast;
                continue;
            }
            double y = i < 2
                ? x
                : (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) * invA0;

            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;

            destination[i] = y;
        }
    }
}
