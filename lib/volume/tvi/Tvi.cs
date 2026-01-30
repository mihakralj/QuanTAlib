using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TVI: Trade Volume Index
/// </summary>
/// <remarks>
/// Trade Volume Index is a cumulative indicator that measures buying and selling pressure
/// by accumulating volume based on price direction determined by a minimum tick threshold.
/// Unlike OBV which uses any price change, TVI requires price to move beyond a minimum
/// threshold before switching direction, reducing noise from minor price fluctuations.
///
/// Calculation:
/// - If price change > MinTick: direction = 1 (up), TVI += Volume
/// - If price change &lt; -MinTick: direction = -1 (down), TVI -= Volume
/// - If -MinTick &lt;= price change &lt;= MinTick: direction unchanged, TVI += direction * Volume
///
/// Key differences from OBV:
/// - OBV uses any price change to determine direction
/// - TVI uses a minimum tick threshold to filter noise
/// - TVI has "sticky" direction when price moves less than MinTick
///
/// Sources:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/volume/tvi.md
/// </remarks>
[SkipLocalsInit]
public sealed class Tvi : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double TviValue,
        double PrevPrice,
        int Direction,
        double LastValidPrice,
        double LastValidVolume,
        int Index);

    private State _s;
    private State _ps;
    private readonly double _minTick;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current TVI value.
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
    /// Creates a new TVI indicator with the specified minimum tick threshold.
    /// </summary>
    /// <param name="minTick">Minimum price change to register direction change (default: 0.125)</param>
    /// <exception cref="ArgumentException">Thrown when minTick is not positive.</exception>
    public Tvi(double minTick = 0.125)
    {
        if (minTick <= 0)
        {
            throw new ArgumentException("MinTick must be positive", nameof(minTick));
        }

        _minTick = minTick;
        _s = new State(TviValue: 0, PrevPrice: 0, Direction: 1, LastValidPrice: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Name = $"Tvi({minTick})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(TviValue: 0, PrevPrice: 0, Direction: 1, LastValidPrice: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        return Update(input.Close, input.Volume, input.Time, isNew);
    }

    /// <summary>
    /// Updates TVI with price and volume directly.
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

        // Handle NaN/Infinity in price and volume
        double currentPrice = double.IsFinite(price) ? price : s.LastValidPrice;
        double currentVolume = double.IsFinite(volume) ? volume : s.LastValidVolume;

        if (double.IsFinite(price) && price > 0)
        {
            s.LastValidPrice = price;
        }

        if (double.IsFinite(volume) && volume >= 0)
        {
            s.LastValidVolume = volume;
        }

        // Calculate TVI
        if (s.Index > 0 && s.PrevPrice > 0)
        {
            double priceChange = currentPrice - s.PrevPrice;

            // Update direction based on min_tick threshold
            if (priceChange > _minTick)
            {
                s.Direction = 1;
            }
            else if (priceChange < -_minTick)
            {
                s.Direction = -1;
            }
            // else direction stays the same (sticky)

            // Accumulate volume based on direction
            s.TviValue += s.Direction == 1 ? currentVolume : -currentVolume;
        }

        // Store for next iteration
        s.PrevPrice = currentPrice;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(time, s.TviValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates TVI with a TValue input.
    /// </summary>
    /// <remarks>
    /// TVI requires volume data to compute. Using TValue without volume data will
    /// keep TVI unchanged. For proper TVI calculation, use Update(TBar).
    /// </remarks>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // TVI requires volume; without it, we can't compute
        // Return current value unchanged
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.TviValue);
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

    public static TSeries Calculate(TBarSeries source, double minTick = 0.125)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Close.Values, source.Volume.Values, v, minTick);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, double minTick = 0.125)
    {
        if (price.Length != volume.Length)
        {
            throw new ArgumentException("Price and Volume spans must be of the same length", nameof(volume));
        }

        if (price.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (minTick <= 0)
        {
            throw new ArgumentException("MinTick must be positive", nameof(minTick));
        }

        int len = price.Length;
        if (len == 0)
        {
            return;
        }

        // First value is zero (no comparison yet)
        output[0] = 0;

        // Initialize with first valid values (mirror instance Update behavior)
        double prevPrice = double.IsFinite(price[0]) ? price[0] : 0.0;
        double lastValidVolume = double.IsFinite(volume[0]) && volume[0] >= 0 ? volume[0] : 0.0;

        double tvi = 0;
        int direction = 1; // Start with up direction

        for (int i = 1; i < len; i++)
        {
            double currentPrice = price[i];
            double currentVolume = volume[i];

            // Handle NaN - use previous valid values (like instance Update does)
            if (!double.IsFinite(currentPrice))
            {
                currentPrice = prevPrice;
            }
            if (!double.IsFinite(currentVolume) || currentVolume < 0)
            {
                currentVolume = lastValidVolume;
            }
            else
            {
                lastValidVolume = currentVolume;
            }

            // Calculate TVI if we have valid previous price
            if (prevPrice > 0)
            {
                double priceChange = currentPrice - prevPrice;

                // Update direction based on min_tick threshold
                if (priceChange > minTick)
                {
                    direction = 1;
                }
                else if (priceChange < -minTick)
                {
                    direction = -1;
                }
                // else direction stays the same (sticky)

                // Accumulate volume based on direction
                tvi += direction == 1 ? currentVolume : -currentVolume;
            }

            output[i] = tvi;

            // Update prevPrice only if current is valid
            if (double.IsFinite(price[i]) && price[i] > 0)
            {
                prevPrice = price[i];
            }
        }
    }
}