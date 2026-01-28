using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NVI: Negative Volume Index
/// </summary>
/// <remarks>
/// Negative Volume Index tracks price changes on days when volume decreases compared
/// to the previous day. The theory is that on low-volume days, the "smart money"
/// (institutional investors) is taking positions, while high-volume days are driven
/// by less-informed traders.
///
/// Calculation:
/// - If Volume &lt; Previous Volume: NVI = Previous NVI × (Close / Previous Close)
/// - If Volume &gt;= Previous Volume: NVI = Previous NVI (unchanged)
/// - Typically starts at 100 or 1000
///
/// NVI is often used with its signal line (a moving average of NVI) to generate
/// buy/sell signals. When NVI crosses above its signal line, it may indicate
/// a bullish trend driven by smart money.
///
/// Sources:
/// https://www.investopedia.com/terms/n/nvi.asp
/// https://school.stockcharts.com/doku.php?id=technical_indicators:negative_volume_index
/// </remarks>
[SkipLocalsInit]
public sealed class Nvi : ITValuePublisher
{
    private readonly double _startValue;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double NviValue,
        double PrevClose,
        double PrevVolume,
        double LastValidClose,
        double LastValidVolume,
        int Index);

    private State _s;
    private State _ps;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current NVI value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least 2 bars.
    /// </summary>
    public bool IsHot => _s.Index >= 2;

    /// <summary>
    /// Warmup period required before the indicator is considered hot.
    /// </summary>
#pragma warning disable S2325 // Instance property required by indicator interface convention
    public int WarmupPeriod => 2;
#pragma warning restore S2325

    /// <summary>
    /// Creates a new NVI indicator.
    /// </summary>
    /// <param name="startValue">Initial NVI value (default: 100)</param>
    /// <exception cref="ArgumentException">Thrown when startValue is not positive.</exception>
    public Nvi(double startValue = 100.0)
    {
        if (startValue <= 0)
        {
            throw new ArgumentException("Start value must be positive", nameof(startValue));
        }

        _startValue = startValue;
        _s = new State(NviValue: startValue, PrevClose: 0, PrevVolume: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Name = $"Nvi({startValue})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(NviValue: _startValue, PrevClose: 0, PrevVolume: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
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

        // Handle NaN/Infinity in close and volume
        double close = double.IsFinite(input.Close) ? input.Close : s.LastValidClose;
        double volume = double.IsFinite(input.Volume) ? input.Volume : s.LastValidVolume;

        if (double.IsFinite(input.Close) && input.Close > 0)
        {
            s.LastValidClose = input.Close;
        }

        if (double.IsFinite(input.Volume) && input.Volume > 0)
        {
            s.LastValidVolume = input.Volume;
        }

        // Calculate NVI - only update when volume decreases
        if (s.Index > 0 && s.PrevClose > 0 && s.PrevVolume > 0 && close > 0 && volume < s.PrevVolume)
        {
            s.NviValue *= close / s.PrevClose;
        }
        // If volume >= previous volume, NVI stays the same

        // Store for next iteration
        s.PrevClose = close;
        s.PrevVolume = volume;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, s.NviValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates NVI with a TValue input.
    /// </summary>
    /// <remarks>
    /// NVI requires volume data to determine when to update. Using TValue without
    /// volume data will keep NVI unchanged. For proper NVI calculation, use Update(TBar).
    /// </remarks>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // NVI requires volume; without it, we can't determine direction
        // Return current value unchanged
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.NviValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
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

    public static TSeries Calculate(TBarSeries source, double startValue = 100.0)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Close.Values, source.Volume.Values, v, startValue);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, double startValue = 100.0)
    {
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }

        if (close.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (startValue <= 0)
        {
            throw new ArgumentException("Start value must be positive", nameof(startValue));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        // Track last valid values for NaN/Infinity substitution (mirrors Update behavior)
        double lastValidClose = 0;
        double lastValidVolume = 0;

        // First value is just the start value
        output[0] = startValue;

        // Handle first bar's close/volume for last-valid tracking
        if (double.IsFinite(close[0]) && close[0] > 0)
        {
            lastValidClose = close[0];
        }
        if (double.IsFinite(volume[0]) && volume[0] > 0)
        {
            lastValidVolume = volume[0];
        }

        // Sanitized previous values for NVI calculation
        double prevClose = double.IsFinite(close[0]) ? close[0] : lastValidClose;
        double prevVolume = double.IsFinite(volume[0]) ? volume[0] : lastValidVolume;

        double nvi = startValue;
        for (int i = 1; i < len; i++)
        {
            // Sanitize current close/volume (substitute last-valid if not finite)
            double currentClose = double.IsFinite(close[i]) ? close[i] : lastValidClose;
            double currentVolume = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;

            // Update last-valid tracking when values are finite and > 0
            if (double.IsFinite(close[i]) && close[i] > 0)
            {
                lastValidClose = close[i];
            }
            if (double.IsFinite(volume[i]) && volume[i] > 0)
            {
                lastValidVolume = volume[i];
            }

            // Only update when volume decreases (using sanitized values)
            if (prevClose > 0 && prevVolume > 0 && currentClose > 0 && currentVolume < prevVolume)
            {
                nvi *= currentClose / prevClose;
            }
            // Otherwise NVI stays the same

            output[i] = nvi;

            // Store sanitized values for next iteration
            prevClose = currentClose;
            prevVolume = currentVolume;
        }
    }
}