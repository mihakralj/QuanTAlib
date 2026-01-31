using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EFI: Elder Force Index
/// </summary>
/// <remarks>
/// Measures force of price movements by combining price change with volume.
/// Large positive values indicate strong buying; large negative indicates selling pressure.
///
/// Calculation: <c>EFI = (Close - prev_Close) × Volume</c>,
/// <c>Smoothed_EFI = EMA(EFI, period)</c>.
/// </remarks>
/// <seealso href="Efi.md">Detailed documentation</seealso>
/// <seealso href="efi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Efi : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _beta;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double PrevClose;
        public double Ema;
        public double E;
        public bool Warmup;
        public int Index;
        public double LastValid;
    }

    private State _s;
    private State _ps;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current EFI value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed enough bars.
    /// </summary>
    public bool IsHot => _s.Index >= _period;

    /// <summary>
    /// Warmup period required before the indicator is considered hot.
    /// </summary>
    public int WarmupPeriod => _period;

    /// <summary>
    /// Creates a new EFI indicator.
    /// </summary>
    /// <param name="period">Lookback period for EMA smoothing (default: 13)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Efi(int period = 13)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _alpha = 2.0 / (period + 1.0);
        _beta = 1.0 - _alpha;
        Name = $"EFI({period})";

        _s = new State
        {
            PrevClose = double.NaN,
            Ema = 0,
            E = 1.0,
            Warmup = true,
            Index = 0,
            LastValid = 0
        };
        _ps = _s;
    }


    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State
        {
            PrevClose = double.NaN,
            Ema = 0,
            E = 1.0,
            Warmup = true,
            Index = 0,
            LastValid = 0
        };
        _ps = _s;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
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

        double close = input.Close;
        double volume = input.Volume;

        // Validate inputs
        if (!double.IsFinite(close))
        {
            close = s.LastValid;
        }
        else
        {
            s.LastValid = close;
        }

        if (!double.IsFinite(volume))
        {
            volume = 0;
        }

        // Calculate raw force
        double rawForce;
        if (double.IsNaN(s.PrevClose))
        {
            rawForce = 0;
        }
        else
        {
            rawForce = (close - s.PrevClose) * volume;
        }

        // Update EMA with bias correction
        double result;
        if (s.Index == 0)
        {
            s.Ema = 0;
            result = rawForce;
        }
        else
        {
            // EMA: ema = alpha * (value - ema) + ema = alpha * value + beta * ema
            s.Ema = Math.FusedMultiplyAdd(_alpha, rawForce - s.Ema, s.Ema);

            if (s.Warmup)
            {
                s.E *= _beta;
                double c = 1.0 / (1.0 - s.E);
                result = c * s.Ema;

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
            s.PrevClose = close;
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, result);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates EFI with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// EFI requires OHLCV bar data to calculate price change and volume.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "EFI requires OHLCV bar data to calculate price change and volume. " +
            "Use Update(TBar) instead.");
    }

    public TSeries Update(TBarSeries source)
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

    public static TSeries Calculate(TBarSeries source, int period = 13)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 13)
    {
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }

        if (close.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 - alpha;

        // First bar: no previous close, so raw force = 0
        output[0] = 0;

        double ema = 0;
        double e = 1.0;
        bool warmup = true;

        for (int i = 1; i < len; i++)
        {
            double rawForce = (close[i] - close[i - 1]) * volume[i];

            // EMA update with bias correction
            ema = Math.FusedMultiplyAdd(alpha, rawForce - ema, ema);

            if (warmup)
            {
                e *= beta;
                double c = 1.0 / (1.0 - e);
                output[i] = c * ema;

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
}