using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FI: Force Index
/// </summary>
/// <remarks>
/// Measures buying/selling pressure as EMA-smoothed raw force.
/// Input via Update(TValue) expects pre-computed raw force = (close − prevClose) × volume.
/// The Quantower adapter handles OHLCV decomposition.
///
/// Calculation: <c>FI = EMA(rawForce, period)</c> with exponential warmup compensation.
/// </remarks>
/// <seealso href="fi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Fi : AbstractBase
{
    private readonly double _alpha;
    private readonly double _decay;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema,
        double E,
        bool Warmup,
        int Index,
        double LastValid);

    private State _s;
    private State _ps;

    public override bool IsHot => !_s.Warmup;

    public Fi(int period = 13)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1.", nameof(period));
        }

        _alpha = 2.0 / (period + 1.0);
        _decay = 1.0 - _alpha;
        Name = $"Fi({period})";
        WarmupPeriod = period;

        _s = new State(Ema: 0, E: 1.0, Warmup: true, Index: 0, LastValid: 0);
        _ps = _s;
    }

    public Fi(ITValuePublisher src, int period = 13) : this(period)
    {
        src.Pub += (object? sender, in TValueEventArgs e) =>
        {
            Update(e.Value, e.IsNew);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = s.LastValid;
        }
        else
        {
            s.LastValid = value;
        }

        double result;
        if (s.Index == 0)
        {
            s.Ema = value;
            result = value;
        }
        else
        {
            s.Ema = Math.FusedMultiplyAdd(s.Ema, _decay, _alpha * value);

            if (s.Warmup)
            {
                s.E *= _decay;
                double c = s.E > 1e-10 ? 1.0 / (1.0 - s.E) : 1.0;
                result = s.Ema * c;

                if (s.E <= 1e-10)
                {
                    s.Warmup = false;
                }
            }
            else
            {
                result = s.Ema;
            }
        }

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        long baseTicks = DateTime.UtcNow.Ticks;

        Reset();
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(new DateTime(baseTicks + (interval.Ticks * i), DateTimeKind.Utc), source[i]), isNew: true);
        }
    }

    public override void Reset()
    {
        _s = new State(Ema: 0, E: 1.0, Warmup: true, Index: 0, LastValid: 0);
        _ps = _s;
        Last = default;
    }

    public static TSeries Batch(TSeries source, int period = 13)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Values, v, period);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period = 13)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Output span must be the same length as input.", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 - alpha;

        double ema = source[0];
        output[0] = ema;

        double e = 1.0;
        bool warmup = true;

        for (int i = 1; i < len; i++)
        {
            double value = source[i];
            if (!double.IsFinite(value))
            {
                value = output[i - 1];
            }

            ema = Math.FusedMultiplyAdd(ema, beta, alpha * value);

            if (warmup)
            {
                e *= beta;
                double c = e > 1e-10 ? 1.0 / (1.0 - e) : 1.0;
                output[i] = ema * c;

                if (e <= 1e-10)
                {
                    warmup = false;
                }
            }
            else
            {
                output[i] = ema;
            }
        }
    }

    public static (TSeries Results, Fi Indicator) Calculate(TSeries source, int period = 13)
    {
        var indicator = new Fi(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
