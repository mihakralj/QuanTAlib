using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Ssf3 : AbstractBase
{
    private readonly int _period;
    private double _coef1, _coef2, _coef3, _coef4;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Y1, Y2, Y3;
        public double LastValidValue;
        public int Count;
    }

    public override bool IsHot => _state.Count >= 4;

    public Ssf3(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        CalculateCoefficients();
        Name = $"Ssf3({_period})";
        WarmupPeriod = 6 * period;
        _handler = new TValuePublishedHandler(Handle);
        Init();
    }

    public Ssf3(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeCoefficients(int period, out double coef1, out double coef2, out double coef3, out double coef4)
    {
        double sqrt3Pi = Math.Sqrt(3.0) * Math.PI;
        int p = Math.Max(1, period);
        double a1 = Math.Exp(-Math.PI / p);
        double b1 = 2.0 * a1 * Math.Cos(sqrt3Pi / p);
        double c1 = a1 * a1;

        coef2 = b1 + c1;
        coef3 = -(c1 + b1 * c1);
        coef4 = c1 * c1;
        coef1 = 1.0 - coef2 - coef3 - coef4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateCoefficients()
    {
        ComputeCoefficients(_period, out _coef1, out _coef2, out _coef3, out _coef4);
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

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime baseTime = DateTime.UtcNow;
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(baseTime + interval * i, source[i]));
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
            if (_state.Count == 0)
            {
                return Last;
            }
            // Use last valid value
            input = new TValue(input.Time, _state.LastValidValue);
        }

        double x = input.Value;
        _state.LastValidValue = x;

        // 3-pole SSF: y = coef1*x + coef2*y1 + coef3*y2 + coef4*y3
        // Single-sample feedforward (vs binomial for Butter3)
        double y = _state.Count < 4
            ? x
            : Math.FusedMultiplyAdd(_coef4, _state.Y3,
                Math.FusedMultiplyAdd(_coef3, _state.Y2,
                    Math.FusedMultiplyAdd(_coef2, _state.Y1, _coef1 * x)));

        // Update state: shift output history
        _state.Y3 = _state.Y2;
        _state.Y2 = _state.Y1;
        _state.Y1 = y;

        if (_state.Count < 4)
        {
            _state.Count++;
        }

        var tValue = new TValue(input.Time, y);
        Last = tValue;
        PubEvent(tValue, isNew);
        return tValue;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        Span<double> output = new double[source.Count];
        Batch(source.Values, output, _period, double.NaN);

        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(source[i].Time, output[i]));
        }

        // Restore state
        Reset();

        // Replay for convergence of 3-pole IIR state
        int replayCount = Math.Min(source.Count, 6 * _period);
        int start = source.Count - replayCount;

        for (int i = start; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return result;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Ssf3(period);
        return indicator.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int period, double initialLast)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination span must have length >= source length.");
        }

        ComputeCoefficients(period, out double coef1, out double coef2, out double coef3, out double coef4);

        double y1 = 0, y2 = 0, y3 = 0;
        double lastValid = 0;
        int validSampleCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double x = source[i];
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                if (validSampleCount == 0)
                {
                    destination[i] = initialLast;
                    continue;
                }
                x = lastValid;
            }
            else
            {
                lastValid = x;
            }

            // 3-pole SSF: y = coef1*x + coef2*y1 + coef3*y2 + coef4*y3
            double y = validSampleCount < 4
                ? x
                : Math.FusedMultiplyAdd(coef4, y3,
                    Math.FusedMultiplyAdd(coef3, y2,
                        Math.FusedMultiplyAdd(coef2, y1, coef1 * x)));

            y3 = y2;
            y2 = y1;
            y1 = y;

            if (validSampleCount < 4)
            {
                validSampleCount++;
            }

            destination[i] = y;
        }
    }

    public static (TSeries Results, Ssf3 Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Ssf3(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
