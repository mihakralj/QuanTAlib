using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Butter2 : AbstractBase
{
    private readonly int _period;
    private double _a1, _a2, _b0, _b1, _b2;
    private double _invA0;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double X1, X2;
        public double Y1, Y2;
        public int Count;
    }

    public override bool IsHot => _state.Count >= 2;

    public Butter2(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        _period = period;
        CalculateCoefficients();
        Name = $"Butter2({_period})";
        WarmupPeriod = 4 * period;
        _handler = Handle;
        Init();
    }

    public Butter2(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeCoefficients(int period, out double a1, out double a2, out double b0, out double b1, out double b2, out double invA0)
    {
        double omega = 2.0 * Math.PI / period;
        double sinOmega = Math.Sin(omega);
        double cosOmega = Math.Cos(omega);
        double alpha = sinOmega / Math.Sqrt(2.0);

        double a0 = 1.0 + alpha;
        a1 = -2.0 * cosOmega;
        a2 = 1.0 - alpha;

        b0 = (1.0 - cosOmega) / 2.0;
        b1 = 1.0 - cosOmega;
        b2 = (1.0 - cosOmega) / 2.0;

        invA0 = 1.0 / a0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateCoefficients()
    {
        ComputeCoefficients(_period, out _a1, out _a2, out _b0, out _b1, out _b2, out _invA0);
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
            Update(new TValue(baseTime + (interval * i), source[i]));
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
        // IIR: y = (b0*x + b1*x1 + b2*x2 - a1*y1 - a2*y2) * invA0
        // Using chained FMA for precision
        // Computation: 6 multiplications, 4 additions per cycle
        double y = _state.Count < 2
            ? x
            : Math.FusedMultiplyAdd(-_a2, _state.Y2,
                Math.FusedMultiplyAdd(-_a1, _state.Y1,
                    Math.FusedMultiplyAdd(_b2, _state.X2,
                        Math.FusedMultiplyAdd(_b1, _state.X1, _b0 * x)))) * _invA0;

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

        // Replay a reasonable amount (e.g. 4*period) for convergence of IIR state.
        int replayCount = Math.Min(source.Count, 4 * _period);
        int start = source.Count - replayCount;

        for (int i = start; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return result;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Butter2(period);
        return indicator.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int period, double initialLast)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination span must have length >= source length.");
        }

        ComputeCoefficients(period, out double a1, out double a2, out double b0, out double b1, out double b2, out double invA0);

        double x1 = 0, x2 = 0;
        double y1 = 0, y2 = 0;
        int validSampleCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double x = source[i];
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                destination[i] = i > 0 ? destination[i - 1] : initialLast;
                continue;
            }
            // IIR: y = (b0*x + b1*x1 + b2*x2 - a1*y1 - a2*y2) * invA0
            // Using chained FMA for precision
            double y = validSampleCount < 2
                ? x
                : Math.FusedMultiplyAdd(-a2, y2,
                    Math.FusedMultiplyAdd(-a1, y1,
                        Math.FusedMultiplyAdd(b2, x2,
                            Math.FusedMultiplyAdd(b1, x1, b0 * x)))) * invA0;

            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;

            if (validSampleCount < 2)
            {
                validSampleCount++;
            }

            destination[i] = y;
        }
    }
    public static (TSeries Results, Butter2 Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Butter2(period);
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
