using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// OBV: On Balance Volume
/// </summary>
/// <remarks>
/// Cumulative indicator measuring buying/selling pressure: adds volume on up days, subtracts on down.
/// Divergences between price and OBV can signal potential reversals.
///
/// Calculation: <c>if Close > Prev_Close: OBV += Volume</c>;
/// <c>if Close &lt; Prev_Close: OBV -= Volume</c>; otherwise unchanged.
/// </remarks>
/// <seealso href="Obv.md">Detailed documentation</seealso>
/// <seealso href="obv.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Obv : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ObvValue,
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
    /// Current OBV value.
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
    /// Creates a new OBV indicator.
    /// </summary>
    public Obv()
    {
        _s = new State(ObvValue: 0, PrevClose: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Name = "Obv";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(ObvValue: 0, PrevClose: 0, LastValidClose: 0, LastValidVolume: 0, Index: 0);
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

        // Calculate OBV - compare close to previous close
        if (s.Index > 0 && s.PrevClose > 0)
        {
            if (close > s.PrevClose)
            {
                s.ObvValue += volume;
            }
            else if (close < s.PrevClose)
            {
                s.ObvValue -= volume;
            }
            // If close == prevClose, OBV stays the same
        }

        // Store for next iteration
        s.PrevClose = close;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, s.ObvValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates OBV with a TValue input.
    /// </summary>
    /// <remarks>
    /// OBV requires volume data to compute. Using TValue without volume data will
    /// keep OBV unchanged. For proper OBV calculation, use Update(TBar).
    /// </remarks>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // OBV requires volume; without it, we can't compute
        // Return current value unchanged
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.ObvValue);
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

    public static TSeries Calculate(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Close.Values, source.Volume.Values, v);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
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
        double obv = 0;

        for (int i = 1; i < len; i++)
        {
            double currentClose = close[i];
            double currentVolume = volume[i];

            // Skip OBV update if inputs are not finite (matches TA-Lib behavior)
            if (double.IsFinite(currentClose) && double.IsFinite(currentVolume) && double.IsFinite(prevClose))
            {
                if (currentClose > prevClose)
                {
                    obv += currentVolume;
                }
                else if (currentClose < prevClose)
                {
                    obv -= currentVolume;
                }
                // If close == prevClose, OBV stays the same
            }

            output[i] = obv;

            // Update prevClose only if current is valid
            if (double.IsFinite(currentClose))
            {
                prevClose = currentClose;
            }
        }
    }
}