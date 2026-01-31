using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Accumulation (VA) indicator that measures cumulative volume flow
/// relative to each bar's range midpoint, indicating buying or selling pressure.
/// </summary>
/// <remarks>
/// VA Formula:
/// <c>Midpoint = (High + Low) / 2</c>,
/// <c>VA_period = Volume × (Close - Midpoint)</c>,
/// <c>VA = Σ(VA_period)</c>.
///
/// Positive values indicate buying pressure (close above midpoint); negative indicates selling pressure.
/// This implementation is optimized for streaming updates with O(1) per bar using cumulative summation.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed
/// for each OHLCV component independently.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Va.md">Detailed documentation</seealso>
/// <seealso href="va.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Va : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double VaValue,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        double LastValidVolume,
        int Index);

    private State _s;
    private State _ps;

    /// <inheritdoc/>
    public TValue Last { get; private set; }
    /// <inheritdoc/>
    public bool IsHot => _s.Index >= 1;
    /// <inheritdoc/>
    public static int WarmupPeriod => 1;
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the VA indicator.
    /// </summary>
    public Va()
    {
        Name = "Va";
        Reset();
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(VaValue: 0, LastValidHigh: 0, LastValidLow: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Updates the VA with a new bar.
    /// </summary>
    /// <param name="input">The bar data.</param>
    /// <param name="isNew">True if this is a new bar, false if updating current bar.</param>
    /// <returns>The current VA value.</returns>
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

        // Handle NaN/Infinity - substitute with last valid values
        double high = double.IsFinite(input.High) ? input.High : s.LastValidHigh;
        double low = double.IsFinite(input.Low) ? input.Low : s.LastValidLow;
        double close = double.IsFinite(input.Close) ? input.Close : s.LastValidClose;
        double volume = double.IsFinite(input.Volume) ? input.Volume : s.LastValidVolume;

        // Update last valid values
        if (double.IsFinite(input.High) && input.High > 0)
        {
            s.LastValidHigh = input.High;
        }
        if (double.IsFinite(input.Low) && input.Low > 0)
        {
            s.LastValidLow = input.Low;
        }
        if (double.IsFinite(input.Close) && input.Close > 0)
        {
            s.LastValidClose = input.Close;
        }
        if (double.IsFinite(input.Volume) && input.Volume >= 0)
        {
            s.LastValidVolume = input.Volume;
        }

        // Calculate VA for this period
        double midpoint = (high + low) / 2.0;
        double vaPeriod = volume * (close - midpoint);

        // Accumulate
        s.VaValue += vaPeriod;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, s.VaValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VA with a TValue input.
    /// </summary>
    /// <remarks>
    /// VA requires OHLCV data for proper calculation. Using TValue without full bar data
    /// will keep VA unchanged.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // VA requires OHLCV; without it, we can't compute
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.VaValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VA with a series of bars (batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <returns>The result series.</returns>
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
    /// Calculates VA for a series of bars (static batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <returns>The result series.</returns>
    public static TSeries Calculate(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates VA for spans of OHLCV data (high-performance span mode).
    /// </summary>
    /// <param name="high">The high price span.</param>
    /// <param name="low">The low price span.</param>
    /// <param name="close">The close price span.</param>
    /// <param name="volume">The volume span.</param>
    /// <param name="output">The output VA span.</param>
    /// <exception cref="ArgumentException">Thrown when span lengths don't match.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (high.Length != low.Length || high.Length != close.Length || high.Length != volume.Length)
        {
            throw new ArgumentException("All input spans must be of the same length", nameof(volume));
        }
        if (high.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double va = 0;
        double lastValidHigh = high[0];
        double lastValidLow = low[0];
        double lastValidClose = close[0];
        double lastValidVolume = volume[0];

        for (int i = 0; i < len; i++)
        {
            // Get valid values
            double h = double.IsFinite(high[i]) ? high[i] : lastValidHigh;
            double l = double.IsFinite(low[i]) ? low[i] : lastValidLow;
            double c = double.IsFinite(close[i]) ? close[i] : lastValidClose;
            double v = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;

            // Update last valid values
            if (double.IsFinite(high[i]) && high[i] > 0)
            {
                lastValidHigh = high[i];
            }
            if (double.IsFinite(low[i]) && low[i] > 0)
            {
                lastValidLow = low[i];
            }
            if (double.IsFinite(close[i]) && close[i] > 0)
            {
                lastValidClose = close[i];
            }
            if (double.IsFinite(volume[i]) && volume[i] >= 0)
            {
                lastValidVolume = volume[i];
            }

            // Calculate VA
            double midpoint = (h + l) / 2.0;
            double vaPeriod = v * (c - midpoint);
            va += vaPeriod;

            output[i] = va;
        }
    }
}