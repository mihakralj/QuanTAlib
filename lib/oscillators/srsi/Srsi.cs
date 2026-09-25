using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SRSI: Apirine Slow Relative Strength Index
/// </summary>
/// <remarks>
/// A momentum oscillator that measures price changes relative to an EMA rather than
/// to the previous close. Uses Wilder's smoothing (RMA) for gain/loss averaging.
/// Output bounded [0, 100].
///
/// Calculation:
/// <c>EMA = α * Close + (1 - α) * EMA[1]</c>   where α = 2 / (emaLength + 1)
/// <c>diff = Close - EMA</c>
/// <c>posDiff = max(diff, 0),  negDiff = max(-diff, 0)</c>
/// <c>avgPos = RMA(posDiff, rsiLength),  avgNeg = RMA(negDiff, rsiLength)</c>
/// <c>SRS = avgPos / avgNeg</c>
/// <c>SRSI = 100 - 100 / (1 + SRS)</c>
///
/// Reference: Apirine, V. (2015). "The Slow Relative Strength Index."
///            Technical Analysis of Stocks &amp; Commodities, April 2015.
/// </remarks>
/// <seealso href="srsi.md">Detailed documentation</seealso>
/// <seealso href="srsi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Srsi : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema,
        double AvgPos, double AvgNeg,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            Ema = double.NaN,
            AvgPos = double.NaN, AvgNeg = double.NaN,
            Count = 0, LastValid = 0
        };
    }

    private readonly int _emaLength;
    private readonly int _rsiLength;
    private readonly double _emaAlpha;
    private readonly double _rmaAlpha;

    private State _s = State.New();
    private State _ps = State.New();

    /// <summary>
    /// Creates SRSI with specified EMA and RSI lengths.
    /// </summary>
    /// <param name="emaLength">EMA period for price smoothing (must be ≥ 1)</param>
    /// <param name="rsiLength">Wilder's smoothing period for gain/loss (must be ≥ 1)</param>
    public Srsi(int emaLength = 6, int rsiLength = 14)
    {
        if (emaLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(emaLength), emaLength, "EMA length must be at least 1.");
        }
        if (rsiLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rsiLength), rsiLength, "RSI length must be at least 1.");
        }

        _emaLength = emaLength;
        _rsiLength = rsiLength;
        _emaAlpha = 2.0 / (emaLength + 1);
        _rmaAlpha = 1.0 / rsiLength;

        Name = $"Srsi({emaLength},{rsiLength})";
        WarmupPeriod = emaLength + rsiLength;
    }

    /// <summary>
    /// Creates SRSI with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Srsi(ITValuePublisher source, int emaLength = 6, int rsiLength = 14) : this(emaLength, rsiLength)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates SRSI with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Srsi(TSeries source, int emaLength = 6, int rsiLength = 14) : this(emaLength, rsiLength)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _emaLength + _rsiLength;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();

        int len = source.Length;
        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
        }

        Last = new TValue(DateTime.MinValue, ComputeResult());
        _ps = _s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref State s)
    {
        if (double.IsFinite(input))
        {
            s.LastValid = input;
            return input;
        }
        return s.LastValid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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

        double val = GetValidValue(input.Value, ref _s);
        Step(val);
        double result = ComputeResult();

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            double val = source.Values[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
            vSpan[i] = ComputeResult();
        }

        _ps = _s;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: EMA → diff → RMA(posDiff, negDiff).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Step(double input)
    {
        _s.Count++;

        // EMA of close
        double ema;
        if (double.IsNaN(_s.Ema))
        {
            ema = input; // seed EMA with first value
        }
        else
        {
            ema = Math.FusedMultiplyAdd(_emaAlpha, input - _s.Ema, _s.Ema);
        }
        _s.Ema = ema;

        // Difference: Close - EMA
        double diff = input - ema;
        double posDiff = diff > 0 ? diff : 0;
        double negDiff = diff < 0 ? -diff : 0;

        // Wilder's smoothing (RMA) for avg positive/negative differences
        if (double.IsNaN(_s.AvgPos))
        {
            // Seed: first bar
            _s.AvgPos = posDiff;
            _s.AvgNeg = negDiff;
        }
        else
        {
            // RMA: avgPos = (avgPos * (n-1) + posDiff) / n
            //     = avgPos + alpha * (posDiff - avgPos)
            _s.AvgPos = Math.FusedMultiplyAdd(_rmaAlpha, posDiff - _s.AvgPos, _s.AvgPos);
            _s.AvgNeg = Math.FusedMultiplyAdd(_rmaAlpha, negDiff - _s.AvgNeg, _s.AvgNeg);
        }
    }

    /// <summary>
    /// Returns the SRSI value: 100 - 100 / (1 + SRS).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeResult()
    {
        const double epsilon = 1e-10;

        if (double.IsNaN(_s.AvgPos) || double.IsNaN(_s.AvgNeg))
        {
            return 50.0;
        }

        if (_s.AvgNeg < epsilon)
        {
            return (_s.AvgPos < epsilon) ? 50.0 : 100.0;
        }

        double srs = _s.AvgPos / _s.AvgNeg;
        return 100.0 - (100.0 / (1.0 + srs));
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int emaLength = 6, int rsiLength = 14)
    {
        var indicator = new Srsi(emaLength, rsiLength);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int emaLength = 6, int rsiLength = 14)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (emaLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(emaLength), emaLength, "EMA length must be at least 1.");
        }
        if (rsiLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rsiLength), rsiLength, "RSI length must be at least 1.");
        }

        if (source.Length == 0)
        {
            return;
        }

        var indicator = new Srsi(emaLength, rsiLength);
        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                indicator._s.LastValid = val;
            }
            else
            {
                val = indicator._s.LastValid;
            }

            indicator.Step(val);
            output[i] = indicator.ComputeResult();
        }
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Srsi Indicator) Calculate(TSeries source, int emaLength = 6, int rsiLength = 14)
    {
        var indicator = new Srsi(emaLength, rsiLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        Last = default;
    }
}
