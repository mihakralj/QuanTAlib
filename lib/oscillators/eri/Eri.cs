using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ERI: Elder Ray Index
/// </summary>
/// <remarks>
/// Measures buying/selling pressure relative to an EMA trend line.
/// Bull Power = High − EMA(Close, period); Bear Power = Low − EMA(Close, period).
/// Primary output (Last) is Bull Power; Bear Power is accessible via the BearPower property.
/// The Quantower adapter handles OHLCV bar decomposition.
///
/// Calculation: <c>EMA = EMA(close, period)</c> with exponential warmup compensation,
/// then <c>BullPower = high − EMA</c>, <c>BearPower = low − EMA</c>.
/// </remarks>
/// <seealso href="eri.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Eri : AbstractBase
{
    private readonly double _alpha;
    private readonly double _decay;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema,
        double E,
        bool Warmup,
        int Index,
        double LastValidClose,
        double LastValidHigh,
        double LastValidLow,
        double BearPower);

    private State _s;
    private State _ps;

    public override bool IsHot => _s.Index >= WarmupPeriod;

    /// <summary>
    /// Bear Power = Low − EMA(Close). Updated after each Update call.
    /// </summary>
    public double BearPower => _s.BearPower;

    public Eri(int period = 13)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1.", nameof(period));
        }

        _alpha = 2.0 / (period + 1.0);
        _decay = 1.0 - _alpha;
        Name = $"Eri({period})";
        WarmupPeriod = period;

        _s = new State(Ema: 0, E: 1.0, Warmup: true, Index: 0,
            LastValidClose: 0, LastValidHigh: 0, LastValidLow: 0, BearPower: 0);
        _ps = _s;
    }

    public Eri(ITValuePublisher src, int period = 13) : this(period)
    {
        src.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    /// <summary>
    /// Updates with a TBar (High, Low, Close). Returns Bull Power as the primary value.
    /// Bear Power is accessible via the BearPower property.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
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

        double close = bar.Close;
        double high = bar.High;
        double low = bar.Low;

        // NaN/Infinity guard for close
        if (!double.IsFinite(close))
        {
            close = s.LastValidClose;
        }
        else
        {
            s.LastValidClose = close;
        }

        // NaN/Infinity guard for high
        if (!double.IsFinite(high))
        {
            high = s.LastValidHigh;
        }
        else
        {
            s.LastValidHigh = high;
        }

        // NaN/Infinity guard for low
        if (!double.IsFinite(low))
        {
            low = s.LastValidLow;
        }
        else
        {
            s.LastValidLow = low;
        }

        // Compute EMA of close
        double emaVal;
        if (s.Index == 0)
        {
            s.Ema = close;
            emaVal = close;
        }
        else
        {
            s.Ema = Math.FusedMultiplyAdd(s.Ema, _decay, _alpha * close);

            if (s.Warmup)
            {
                s.E *= _decay;
                double c = s.E > 1e-10 ? 1.0 / (1.0 - s.E) : 1.0;
                emaVal = s.Ema * c;

                if (s.E <= 1e-10)
                {
                    s.Warmup = false;
                }
            }
            else
            {
                emaVal = s.Ema;
            }
        }

        double bullPower = high - emaVal;
        s.BearPower = low - emaVal;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(bar.Time, bullPower);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates with a single TValue (treated as close price with high=low=close).
    /// For proper ERI computation, use Update(TBar) instead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
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
        _s = new State(Ema: 0, E: 1.0, Warmup: true, Index: 0,
            LastValidClose: 0, LastValidHigh: 0, LastValidLow: 0, BearPower: 0);
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

    /// <summary>
    /// Span-based calculation for close-only data.
    /// Computes EMA(close) and outputs Bull Power = close − EMA (since high=low=close).
    /// For proper H/L/C computation, use the TBar overloads.
    /// </summary>
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
        // When high=low=close, Bull Power = close - ema = 0 on first bar
        output[0] = source[0] - ema;

        double e = 1.0;
        bool warmup = true;
        double lastValid = source[0];

        for (int i = 1; i < len; i++)
        {
            double value = source[i];
            if (!double.IsFinite(value))
            {
                value = lastValid;
            }
            else
            {
                lastValid = value;
            }

            ema = Math.FusedMultiplyAdd(ema, beta, alpha * value);

            double emaVal;
            if (warmup)
            {
                e *= beta;
                double c = e > 1e-10 ? 1.0 / (1.0 - e) : 1.0;
                emaVal = ema * c;

                if (e <= 1e-10)
                {
                    warmup = false;
                }
            }
            else
            {
                emaVal = ema;
            }

            // For close-only spans, Bull Power = close - EMA
            output[i] = value - emaVal;
        }
    }

    public static (TSeries Results, Eri Indicator) Calculate(TSeries source, int period = 13)
    {
        var indicator = new Eri(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
