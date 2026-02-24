using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Modf : AbstractBase
{
    private readonly double _alpha;
    private readonly double _oneMinusAlpha;
    private readonly double _beta;
    private readonly bool _feedback;
    private readonly double _fbWeight;
    private readonly double _oneMinusFbWeight;
    private readonly double _oneMinusBeta;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private int _index;

    private State _s;
    private State _ps;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double B;
        public double C;
        public double Os;
        public double Ts;
        public double LastValue;
        public bool Initialized;
    }

    public int Period { get; }
    public double Beta => _beta;
    public bool Feedback => _feedback;
    public double FbWeight => _fbWeight;
    public override bool IsHot => _index >= WarmupPeriod;

    public Modf(int period, double beta = 0.8, bool feedback = false, double fbWeight = 0.5)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2.");
        }
        if (beta < 0.0 || beta > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(beta), "Beta must be in [0, 1].");
        }
        if (fbWeight <= 0.0 || fbWeight > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fbWeight), "Feedback weight must be in (0, 1].");
        }

        Period = period;
        _beta = beta;
        _feedback = feedback;
        _fbWeight = fbWeight;
        _oneMinusFbWeight = 1.0 - fbWeight;
        _alpha = 2.0 / (period + 1);
        _oneMinusAlpha = 1.0 - _alpha;
        _oneMinusBeta = 1.0 - beta;
        WarmupPeriod = period;
        Name = feedback
            ? $"Modf({period},{beta:F1},fb={fbWeight:F2})"
            : $"Modf({period},{beta:F1})";

        Init();
    }

    public Modf(TSeries source, int period, double beta = 0.8, bool feedback = false, double fbWeight = 0.5)
        : this(period, beta, feedback, fbWeight)
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
        _s = default;
        _ps = default;
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
        if (isNew) { _ps = _s; _index++; }
        else { _s = _ps; }

        var s = _s;

        double val = input.Value;
        if (double.IsNaN(val) || double.IsInfinity(val))
        {
            val = s.LastValue;
        }

        double result;
        if (!s.Initialized)
        {
            s.B = val;
            s.C = val;
            s.Os = 0.0;
            s.Ts = val;
            s.Initialized = true;
            result = val;
        }
        else
        {
            // Input: optionally blend source with previous output (feedback)
            double a = _feedback
                ? Math.FusedMultiplyAdd(_oneMinusFbWeight, s.Ts, _fbWeight * val)
                : val;

            // Upper band: EMA that snaps up to 'a' when a exceeds EMA
            double emaB = Math.FusedMultiplyAdd(_oneMinusAlpha, s.B, _alpha * a);
            s.B = a > emaB ? a : emaB;

            // Lower band: EMA that snaps down to 'a' when a falls below EMA
            double emaC = Math.FusedMultiplyAdd(_oneMinusAlpha, s.C, _alpha * a);
            s.C = a < emaC ? a : emaC;

            // Oscillator state: 1 = upper (bullish), 0 = lower (bearish)
            // skipcq: CS-R1085 - exact equality intentional for snap detection
            if (a == s.B)
            {
                s.Os = 1.0;
            }
            else if (a == s.C)
            {
                s.Os = 0.0;
            }

            // Beta-weighted band combinations
            double upper = Math.FusedMultiplyAdd(_beta, s.B, _oneMinusBeta * s.C);
            double lower = Math.FusedMultiplyAdd(_beta, s.C, _oneMinusBeta * s.B);

            // Final output: state-selected weighted band
            result = Math.FusedMultiplyAdd(s.Os, upper, (1.0 - s.Os) * lower);
            s.Ts = result;
        }

        if (!double.IsNaN(val) && !double.IsInfinity(val))
        {
            s.LastValue = val;
        }

        _s = s;

        TValue output = new(input.Time, result);
        Last = output;
        PubEvent(output, isNew);
        return output;
    }

    public override TSeries Update(TSeries source)
    {
        var tsResult = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), Period, _beta, _feedback, _fbWeight);

        for (int i = 0; i < outArray.Length; i++)
        {
            tsResult.Add(new TValue(source.Times[i], outArray[i]));
        }

        if (srcSpan.Length > 0)
        {
            int replayStart = Math.Max(0, srcSpan.Length - Math.Max(WarmupPeriod, 4));
            Reset();
            for (int i = replayStart; i < srcSpan.Length; i++)
            {
                Update(new TValue(source.Times[i], srcSpan[i]), isNew: true);
            }
        }

        return tsResult;
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int period, double beta = 0.8, bool feedback = false, double fbWeight = 0.5)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output lengths must match.", nameof(output));
        }
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2.");
        }

        double alpha = 2.0 / (period + 1);
        double oneMinusAlpha = 1.0 - alpha;
        double oneMinusBeta = 1.0 - beta;
        double oneMinusFbWeight = 1.0 - fbWeight;

        double b = 0, c = 0, os = 0, ts = 0, lastVal = 0;
        bool initialized = false;

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

            if (!initialized)
            {
                b = val;
                c = val;
                os = 0;
                ts = val;
                initialized = true;
                output[i] = val;
                continue;
            }

            double a = feedback
                ? Math.FusedMultiplyAdd(oneMinusFbWeight, ts, fbWeight * val)
                : val;

            double emaB = Math.FusedMultiplyAdd(oneMinusAlpha, b, alpha * a);
            b = a > emaB ? a : emaB;

            double emaC = Math.FusedMultiplyAdd(oneMinusAlpha, c, alpha * a);
            c = a < emaC ? a : emaC;

            if (a == b)
            {
                os = 1.0;
            }
            else if (a == c)
            {
                os = 0.0;
            }

            double upper = Math.FusedMultiplyAdd(beta, b, oneMinusBeta * c);
            double lower = Math.FusedMultiplyAdd(beta, c, oneMinusBeta * b);

            ts = Math.FusedMultiplyAdd(os, upper, (1.0 - os) * lower);
            output[i] = ts;
        }
    }

    public static TSeries Batch(TSeries source, int period, double beta = 0.8,
        bool feedback = false, double fbWeight = 0.5)
    {
        var result = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), period, beta, feedback, fbWeight);

        for (int i = 0; i < outArray.Length; i++)
        {
            result.Add(new TValue(source.Times[i], outArray[i]));
        }

        return result;
    }

    public static (TSeries Results, Modf Indicator) Calculate(TSeries source,
        int period, double beta = 0.8, bool feedback = false, double fbWeight = 0.5)
    {
        var indicator = new Modf(period, beta, feedback, fbWeight);
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
