using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Positive Volume Index (PVI) that tracks price changes only on days when
/// volume increases, based on the theory that high-volume days reflect uninformed crowd trading.
/// </summary>
/// <remarks>
/// PVI Formula:
/// <c>If Volume &gt; Previous_Volume: PVI = Previous_PVI × (Close / Previous_Close)</c>,
/// <c>If Volume ≤ Previous_Volume: PVI = Previous_PVI (unchanged)</c>.
///
/// Typically starts at 100 or 1000. When PVI is below its 1-year moving average, there is
/// a 67% probability of a bear market according to Fosback's research.
/// This implementation is optimized for streaming updates with O(1) per bar.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Pvi.md">Detailed documentation</seealso>
/// <seealso href="pvi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pvi : ITValuePublisher
{
    private readonly double _startValue;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PviValue,
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
    /// Current PVI value.
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
    /// Creates a new PVI indicator.
    /// </summary>
    /// <param name="startValue">Initial PVI value (default: 100)</param>
    /// <exception cref="ArgumentException">Thrown when startValue is not positive.</exception>
    public Pvi(double startValue = 100.0)
    {
        if (startValue <= 0)
        {
            throw new ArgumentException("Start value must be positive", nameof(startValue));
        }

        _startValue = startValue;
        _s = new State(PviValue: startValue, PrevClose: 0, PrevVolume: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Name = $"Pvi({startValue})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(PviValue: _startValue, PrevClose: 0, PrevVolume: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
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

        // Calculate PVI - only update when volume increases
        // Matches PineScript: if not (na(src) or na(vol) or na(src[1]) or na(vol[1]) or src[1] == 0.0 or vol[1] <= 0.0) and vol > vol[1]
        if (s.Index > 0 && s.PrevClose > 0 && s.PrevVolume > 0 && volume > s.PrevVolume)
        {
            s.PviValue *= close / s.PrevClose;
        }
        // If volume <= previous volume, PVI stays the same

        // Store for next iteration
        s.PrevClose = close;
        s.PrevVolume = volume;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, s.PviValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVI with a TValue input.
    /// </summary>
    /// <remarks>
    /// PVI requires volume data to determine when to update. Using TValue without
    /// volume data will keep PVI unchanged. For proper PVI calculation, use Update(TBar).
    /// </remarks>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // PVI requires volume; without it, we can't determine direction
        // Return current value unchanged
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.PviValue);
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

    public static TSeries Batch(TBarSeries source, double startValue = 100.0)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Close.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, v, startValue);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, double startValue = 100.0)
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

        // Sanitized previous values for PVI calculation
        double prevClose = double.IsFinite(close[0]) ? close[0] : lastValidClose;
        double prevVolume = double.IsFinite(volume[0]) ? volume[0] : lastValidVolume;

        double pvi = startValue;
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

            // Only update when volume increases (using sanitized values)
            // Matches PineScript: if not (na(src) or na(vol) or na(src[1]) or na(vol[1]) or src[1] == 0.0 or vol[1] <= 0.0) and vol > vol[1]
            if (prevClose > 0 && prevVolume > 0 && currentVolume > prevVolume)
            {
                pvi *= currentClose / prevClose;
            }
            // Otherwise PVI stays the same

            output[i] = pvi;

            // Store sanitized values for next iteration
            prevClose = currentClose;
            prevVolume = currentVolume;
        }
    }

    public static (TSeries Results, Pvi Indicator) Calculate(TBarSeries source, double startValue = 100.0)
    {
        var indicator = new Pvi(startValue);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}