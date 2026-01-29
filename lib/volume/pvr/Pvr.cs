using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PVR: Price Volume Rank
/// A categorical indicator that ranks price-volume relationships into discrete states.
/// Returns values 0-4 based on price and volume direction changes.
/// </summary>
/// <remarks>
/// The PVR calculation process:
/// Compares current price and volume with previous values:
/// - 1: Price up, Volume up (strong bullish)
/// - 2: Price up, Volume down (weak bullish)
/// - 3: Price down, Volume down (weak bearish)
/// - 4: Price down, Volume up (strong bearish)
/// - 0: Price unchanged
///
/// Key characteristics:
/// - Categorical output (0, 1, 2, 3, or 4)
/// - No warmup period needed (only requires 1 previous bar)
/// - Useful for filtering trade signals based on price-volume confirmation
///
/// Sources:
///     https://github.com/mihakralj/pinescript/blob/main/indicators/volume/pvr.md
/// </remarks>
[SkipLocalsInit]
public sealed class Pvr : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double PrevPrice;
        public double PrevVolume;
        public double LastValidPrice;
        public double LastValidVolume;
        public bool HasPrevious;
    }

    private State _s;
    private State _ps;

    public string Name { get; }
    public int WarmupPeriod { get; } = 1;
    public TValue Last { get; private set; }
    public bool IsHot { get; private set; }
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the Pvr class.
    /// </summary>
    public Pvr()
    {
        Name = "Pvr";
        _s = new State { LastValidPrice = 0.0, LastValidVolume = 0.0 };
        _ps = _s;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The bar data containing Close and Volume</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The PVR rank (0-4)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return Update(bar.Close, bar.Volume, bar.Time, isNew);
    }

    /// <summary>
    /// Updates the indicator with price and volume values.
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
        double currentPrice = double.IsFinite(price) ? price : s.LastValidPrice;
        double currentVolume = double.IsFinite(volume) ? Math.Max(volume, 0.0) : s.LastValidVolume;

        if (double.IsFinite(price))
        {
            s.LastValidPrice = price;
        }
        if (double.IsFinite(volume))
        {
            s.LastValidVolume = Math.Max(volume, 0.0);
        }

        double pvrValue;
        if (!s.HasPrevious)
        {
            // First bar - no previous to compare
            pvrValue = 0.0;
            s.HasPrevious = true;
            IsHot = false;
        }
        else
        {
            // Calculate PVR based on price and volume direction
            double prevPrice = s.PrevPrice;
            double prevVolume = s.PrevVolume;

            if (currentPrice > prevPrice)
            {
                pvrValue = currentVolume > prevVolume ? 1.0 : 2.0;
            }
            else if (currentPrice < prevPrice)
            {
                pvrValue = currentVolume < prevVolume ? 3.0 : 4.0;
            }
            else
            {
                pvrValue = 0.0;
            }
            IsHot = true;
        }

        // Store current values for next comparison
        s.PrevPrice = currentPrice;
        s.PrevVolume = currentVolume;

        _s = s;

        Last = new TValue(time, pvrValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVR with a bar series.
    /// </summary>
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
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _s = new State { LastValidPrice = 0.0, LastValidVolume = 0.0 };
        _ps = _s;
        Last = default;
        IsHot = false;
    }

    /// <summary>
    /// Calculates PVR for a series of bars.
    /// </summary>
    public static TSeries Calculate(TBarSeries bars)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var t = bars.Open.Times.ToArray();
        var v = new double[bars.Count];

        Calculate(bars.Close.Values, bars.Volume.Values, v);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates PVR values using span-based processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (price.Length != output.Length)
        {
            throw new ArgumentException("Output span must have the same length as price input", nameof(output));
        }
        if (price.Length != volume.Length)
        {
            throw new ArgumentException("Volume span must have the same length as price input", nameof(volume));
        }

        int length = price.Length;
        if (length == 0)
        {
            return;
        }

        // First bar - validate initial values (mirror instance Update behavior)
        output[0] = 0.0;
        double prevPrice = double.IsFinite(price[0]) ? price[0] : 0.0;
        double prevVolume = double.IsFinite(volume[0]) ? Math.Max(volume[0], 0.0) : 0.0;

        // If first values were NaN, find first finite values as fallback
        if (prevPrice == 0.0 && !double.IsFinite(price[0]))
        {
            for (int j = 1; j < length; j++)
            {
                if (double.IsFinite(price[j]))
                {
                    prevPrice = price[j];
                    break;
                }
            }
        }
        if (prevVolume == 0.0 && !double.IsFinite(volume[0]))
        {
            for (int j = 1; j < length; j++)
            {
                if (double.IsFinite(volume[j]))
                {
                    prevVolume = Math.Max(volume[j], 0.0);
                    break;
                }
            }
        }

        for (int i = 1; i < length; i++)
        {
            double currentPrice = price[i];
            double currentVolume = volume[i];

            // Handle NaN
            if (!double.IsFinite(currentPrice))
            {
                currentPrice = prevPrice;
            }
            if (!double.IsFinite(currentVolume))
            {
                currentVolume = prevVolume;
            }
            currentVolume = Math.Max(currentVolume, 0.0);

            // Calculate PVR
            if (currentPrice > prevPrice)
            {
                output[i] = currentVolume > prevVolume ? 1.0 : 2.0;
            }
            else if (currentPrice < prevPrice)
            {
                output[i] = currentVolume < prevVolume ? 3.0 : 4.0;
            }
            else
            {
                output[i] = 0.0;
            }

            prevPrice = currentPrice;
            prevVolume = currentVolume;
        }
    }
}