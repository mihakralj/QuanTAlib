using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Price Volume Trend (PVT) that weights volume by relative price change,
/// providing a cumulative measure of buying and selling pressure proportional to price moves.
/// </summary>
/// <remarks>
/// PVT Formula:
/// <c>PVT = PVT_prev + Volume × ((Close - Close_prev) / Close_prev)</c>.
///
/// Unlike OBV which uses all-or-nothing volume, PVT assigns proportional volume based on price change magnitude.
/// This implementation is optimized for streaming updates with O(1) per bar using cumulative summation.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Pvt.md">Detailed documentation</seealso>
/// <seealso href="pvt.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pvt : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PvtValue,
        double PrevClose,
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
    /// Current PVT value.
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
    /// Creates a new PVT indicator.
    /// </summary>
    public Pvt()
    {
        _s = new State(PvtValue: 0, PrevClose: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Name = "Pvt";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(PvtValue: 0, PrevClose: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
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

        // Calculate PVT: volume * (price_change / prev_price)
        if (s.Index > 0 && s.PrevClose > 0)
        {
            double priceChange = close - s.PrevClose;
            double priceChangeRatio = priceChange / s.PrevClose;
            double volumeAdjustment = volume * priceChangeRatio;
            s.PvtValue += volumeAdjustment;
        }

        // Store for next iteration
        s.PrevClose = close;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, s.PvtValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVT with price and volume directly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double price, double volume, long time, bool isNew = true)
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

        // Handle NaN/Infinity
        double close = double.IsFinite(price) ? price : s.LastValidClose;
        double vol = double.IsFinite(volume) ? volume : s.LastValidVolume;

        if (double.IsFinite(price) && price > 0)
        {
            s.LastValidClose = price;
        }

        if (double.IsFinite(volume) && volume > 0)
        {
            s.LastValidVolume = volume;
        }

        // Calculate PVT: volume * (price_change / prev_price)
        if (s.Index > 0 && s.PrevClose > 0)
        {
            double priceChange = close - s.PrevClose;
            double priceChangeRatio = priceChange / s.PrevClose;
            double volumeAdjustment = vol * priceChangeRatio;
            s.PvtValue += volumeAdjustment;
        }

        // Store for next iteration
        s.PrevClose = close;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(time, s.PvtValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVT with a TValue input.
    /// </summary>
    /// <remarks>
    /// PVT requires volume data to compute. Using TValue without volume data will
    /// keep PVT unchanged. For proper PVT calculation, use Update(TBar).
    /// </remarks>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // PVT requires volume; without it, we can't compute
        // Return current value unchanged
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.PvtValue);
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

    public static TSeries Batch(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, v);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }

        if (close.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        // First value is zero (no comparison yet)
        output[0] = 0;

        double prevClose = close[0];
        double pvt = 0;

        for (int i = 1; i < len; i++)
        {
            double currentClose = close[i];
            double currentVolume = volume[i];

            // Calculate PVT if inputs are finite and prevClose is positive (consistent with Update method)
            if (double.IsFinite(currentClose) && double.IsFinite(currentVolume) &&
                double.IsFinite(prevClose) && prevClose > 0)
            {
                double priceChange = currentClose - prevClose;
                double priceChangeRatio = priceChange / prevClose;
                pvt += currentVolume * priceChangeRatio;
            }

            output[i] = pvt;

            // Update prevClose only if current is valid
            if (double.IsFinite(currentClose))
            {
                prevClose = currentClose;
            }
        }
    }

    public static (TSeries Results, Pvt Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Pvt();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}