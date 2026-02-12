using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AOBV: Archer On-Balance Volume
/// </summary>
/// <remarks>
/// Applies dual EMA smoothing (4,14) to OBV for fast/slow signal lines.
/// Fast crossing above slow indicates bullish momentum; below indicates bearish.
///
/// Calculation: <c>OBV = cumulative sum(±Volume)</c> based on close direction,
/// <c>AOBV_Fast = EMA(OBV, 4)</c>, <c>AOBV_Slow = EMA(OBV, 14)</c>.
/// </remarks>
/// <seealso href="Aobv.md">Detailed documentation</seealso>
/// <seealso href="aobv.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Aobv : ITValuePublisher
{
    private const int FastPeriod = 4;
    private const int SlowPeriod = 14;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Obv;
        public double EmaFast;
        public double EmaSlow;
        public double EFast;
        public double ESlow;
        public double PrevClose;
        public double LastValidClose;   // NaN sentinel - no valid value yet
        public double LastValidVolume;  // NaN sentinel - no valid value yet
        public bool WarmupFast;
        public bool WarmupSlow;
        public int Index;
    }

    private State _s;
    private State _ps;

    private readonly double _alphaFast;
    private readonly double _betaFast;
    private readonly double _alphaSlow;
    private readonly double _betaSlow;

#pragma warning disable S2325 // Interface contract cannot be static
    public string Name => "AOBV(4,14)";
#pragma warning restore S2325
    public event TValuePublishedHandler? Pub;
    public TValue Last { get; private set; }
    public TValue LastFast { get; private set; }
    public TValue LastSlow { get; private set; }
    public bool IsHot => _s.Index >= SlowPeriod;
#pragma warning disable S2325 // Interface contract cannot be static
    public int WarmupPeriod => SlowPeriod;
#pragma warning restore S2325

    public Aobv()
    {
        _alphaFast = 2.0 / (FastPeriod + 1);
        _betaFast = 1.0 - _alphaFast;
        _alphaSlow = 2.0 / (SlowPeriod + 1);
        _betaSlow = 1.0 - _alphaSlow;

        _s = new State
        {
            EFast = 1.0,
            ESlow = 1.0,
            WarmupFast = true,
            WarmupSlow = true,
            LastValidClose = double.NaN,   // NaN sentinel until first valid value
            LastValidVolume = double.NaN   // NaN sentinel until first valid value
        };
        _ps = _s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State
        {
            EFast = 1.0,
            ESlow = 1.0,
            WarmupFast = true,
            WarmupSlow = true,
            LastValidClose = double.NaN,   // NaN sentinel until first valid value
            LastValidVolume = double.NaN   // NaN sentinel until first valid value
        };
        _ps = _s;
        Last = default;
        LastFast = default;
        LastSlow = default;
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

        // Handle NaN/Infinity for close - use input if finite, else last valid, else skip this bar's OBV contribution
        double close;
        if (double.IsFinite(input.Close))
        {
            close = input.Close;
            s.LastValidClose = input.Close;
        }
        else if (double.IsFinite(s.LastValidClose))
        {
            close = s.LastValidClose;
        }
        else
        {
            // No valid close seen yet - use 0 as neutral (won't affect OBV comparison meaningfully on first bar)
            close = 0;
        }

        // Handle NaN/Infinity for volume - use input if finite, else last valid, else 0 (neutral)
        double volume;
        if (double.IsFinite(input.Volume))
        {
            volume = input.Volume;
            s.LastValidVolume = input.Volume;
        }
        else if (double.IsFinite(s.LastValidVolume))
        {
            volume = s.LastValidVolume;
        }
        else
        {
            // No valid volume seen yet - use 0 as neutral (won't change OBV)
            volume = 0;
        }

        // Calculate OBV
        if (s.Index == 0)
        {
            // First bar initialization - all values start at 0
            s.Obv = 0;
            s.EmaFast = 0;
            s.EmaSlow = 0;
        }
        else
        {
            double prevClose = s.PrevClose;
            if (close > prevClose)
            {
                s.Obv += volume;
            }
            else if (close < prevClose)
            {
                s.Obv -= volume;
            }
        }

        // Calculate EMA Fast with warmup compensation
        s.EmaFast = Math.FusedMultiplyAdd(_alphaFast, s.Obv - s.EmaFast, s.EmaFast);

        double resultFast;
        if (s.WarmupFast)
        {
            s.EFast *= _betaFast;
            double c = 1.0 / (1.0 - s.EFast);
            resultFast = c * s.EmaFast;
            if (s.EFast <= 1e-10)
            {
                s.WarmupFast = false;
            }
        }
        else
        {
            resultFast = s.EmaFast;
        }

        // Calculate EMA Slow with warmup compensation
        s.EmaSlow = Math.FusedMultiplyAdd(_alphaSlow, s.Obv - s.EmaSlow, s.EmaSlow);

        double resultSlow;
        if (s.WarmupSlow)
        {
            s.ESlow *= _betaSlow;
            double c = 1.0 / (1.0 - s.ESlow);
            resultSlow = c * s.EmaSlow;
            if (s.ESlow <= 1e-10)
            {
                s.WarmupSlow = false;
            }
        }
        else
        {
            resultSlow = s.EmaSlow;
        }

        // Store previous close for next iteration
        s.PrevClose = close;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        LastFast = new TValue(input.Time, resultFast);
        LastSlow = new TValue(input.Time, resultSlow);
        Last = LastFast; // Primary output is fast line

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates AOBV with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// AOBV requires OHLCV bar data to calculate OBV from close and volume.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "AOBV requires OHLCV bar data to calculate OBV from close and volume. " +
            "Use Update(TBar) instead.");
    }

    public (TSeries Fast, TSeries Slow) Update(TBarSeries source)
    {
        var tFast = new List<long>(source.Count);
        var vFast = new List<double>(source.Count);
        var tSlow = new List<long>(source.Count);
        var vSlow = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
            tFast.Add(LastFast.Time);
            vFast.Add(LastFast.Value);
            tSlow.Add(LastSlow.Time);
            vSlow.Add(LastSlow.Value);
        }

        return (new TSeries(tFast, vFast), new TSeries(tSlow, vSlow));
    }


    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    public static (TSeries Fast, TSeries Slow) Calculate(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return ([], []);
        }

        var t = source.Open.Times.ToArray();
        var vFast = new double[source.Count];
        var vSlow = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, vFast, vSlow);

        return (new TSeries(t, vFast), new TSeries(t, vSlow));
    }

    /// <summary>
    /// Calculates AOBV (Archer On-Balance Volume) from close and volume spans.
    /// </summary>
    /// <param name="close">Input close prices. NaN/Infinity values are replaced with last valid value.</param>
    /// <param name="volume">Input volume values. NaN/Infinity values are replaced with last valid value.</param>
    /// <param name="outputFast">Output span for fast EMA line.</param>
    /// <param name="outputSlow">Output span for slow EMA line.</param>
    /// <remarks>
    /// Input sanitization: NaN/Infinity values in close or volume are replaced with the last valid
    /// value seen. If no valid value has been seen yet, 0 is used as a neutral fallback.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume,
        Span<double> outputFast, Span<double> outputSlow)
    {
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }

        if (close.Length != outputFast.Length)
        {
            throw new ArgumentException("Output Fast span must be of the same length as input", nameof(outputFast));
        }

        if (close.Length != outputSlow.Length)
        {
            throw new ArgumentException("Output Slow span must be of the same length as input", nameof(outputSlow));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        double alphaFast = 2.0 / (FastPeriod + 1);
        double betaFast = 1.0 - alphaFast;
        double alphaSlow = 2.0 / (SlowPeriod + 1);
        double betaSlow = 1.0 - alphaSlow;

        double obv = 0;
        double emaFast = 0;
        double emaSlow = 0;
        double eFast = 1.0;
        double eSlow = 1.0;
        bool warmupFast = true;
        bool warmupSlow = true;

        // NaN sentinel for last valid values
        double lastValidClose = double.NaN;
        double lastValidVolume = double.NaN;
        double prevClose = 0;

        for (int i = 0; i < len; i++)
        {
            // Handle NaN/Infinity for close - use input if finite, else last valid, else 0 (neutral)
            double c;
            if (double.IsFinite(close[i]))
            {
                c = close[i];
                lastValidClose = close[i];
            }
            else if (double.IsFinite(lastValidClose))
            {
                c = lastValidClose;
            }
            else
            {
                c = 0;
            }

            // Handle NaN/Infinity for volume - use input if finite, else last valid, else 0 (neutral)
            double v;
            if (double.IsFinite(volume[i]))
            {
                v = volume[i];
                lastValidVolume = volume[i];
            }
            else if (double.IsFinite(lastValidVolume))
            {
                v = lastValidVolume;
            }
            else
            {
                v = 0;
            }

            // Calculate OBV
            if (i == 0)
            {
                obv = 0; // First bar, no comparison
            }
            else
            {
                if (c > prevClose)
                {
                    obv += v;
                }
                else if (c < prevClose)
                {
                    obv -= v;
                }
            }

            // EMA Fast
            emaFast = Math.FusedMultiplyAdd(alphaFast, obv - emaFast, emaFast);
            if (warmupFast)
            {
                eFast *= betaFast;
                double comp = 1.0 / (1.0 - eFast);
                outputFast[i] = comp * emaFast;
                if (eFast <= 1e-10)
                {
                    warmupFast = false;
                }
            }
            else
            {
                outputFast[i] = emaFast;
            }

            // EMA Slow
            emaSlow = Math.FusedMultiplyAdd(alphaSlow, obv - emaSlow, emaSlow);
            if (warmupSlow)
            {
                eSlow *= betaSlow;
                double comp = 1.0 / (1.0 - eSlow);
                outputSlow[i] = comp * emaSlow;
                if (eSlow <= 1e-10)
                {
                    warmupSlow = false;
                }
            }
            else
            {
                outputSlow[i] = emaSlow;
            }

            prevClose = c;
        }
    }
}