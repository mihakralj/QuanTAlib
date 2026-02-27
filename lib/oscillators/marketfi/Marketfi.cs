// MARKETFI: Market Facilitation Index
// Bill Williams' measure of price movement efficiency per unit of volume.
// Formula: MFI = (High - Low) / Volume
// Guard: Volume == 0 → 0.0 (no market activity = zero facilitation)

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MARKETFI: Market Facilitation Index
/// </summary>
/// <remarks>
/// Bill Williams' efficiency measure: how much price moves per unit of volume.
/// <list type="bullet">
///   <item>MARKETFI = (High − Low) / Volume</item>
///   <item>Zero volume guard: result = 0.0 (market closed / no activity)</item>
///   <item>Large MARKETFI + rising volume → strong trend continuation</item>
///   <item>Small MARKETFI + falling volume → squat / accumulation phase</item>
/// </list>
/// O(1) per bar — no period, no buffers, pure division.
/// IsHot fires on the first bar (no warmup required).
///
/// References:
///   Williams, Bill (1995). Trading Chaos.
///   PineScript reference: marketfi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Marketfi : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValid,
        int Count);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid output (always 1 — no warmup).</summary>
    public static int WarmupPeriod => 1;

    /// <summary>True from the first bar onward.</summary>
    public bool IsHot => _s.Count >= 1;

    /// <summary>Current MARKETFI value (price range per unit of volume).</summary>
    public TValue Last { get; private set; }

    public event TValuePublishedHandler? Pub;

    /// <summary>Creates a MARKETFI indicator.</summary>
    public Marketfi()
    {
        _s = new State(0.0, 0);
        _ps = _s;
        Name = "Marketfi";
        _barHandler = HandleBar;
    }

    /// <summary>Creates MARKETFI chained to a TBarSeries source.</summary>
    public Marketfi(TBarSeries source) : this()
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>Resets all state to initial conditions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(0.0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Updates MARKETFI with a new OHLCV bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current MARKETFI value as TValue</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var s = _s;

        if (isNew)
        {
            _ps = s;
            s.Count++;
        }
        else
        {
            s = _ps;
        }

        // Sanitize OHLCV inputs — use last-valid on NaN/Infinity
        double high = double.IsFinite(input.High) ? input.High : s.LastValid;
        double low = double.IsFinite(input.Low) ? input.Low : s.LastValid;
        double volume = double.IsFinite(input.Volume) ? input.Volume : 0.0;

        // Core formula: price range per unit of volume
        // Zero-volume guard: return 0.0 (no facilitation when no trades occurred)
        double mfi = volume != 0.0 ? (high - low) / volume : 0.0;

        if (double.IsFinite(mfi))
        {
            s.LastValid = mfi;
        }
        else
        {
            mfi = s.LastValid;
        }

        _s = s;

        Last = new TValue(input.Time, mfi);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates MARKETFI from a scalar TValue (uses Val as proxy; High=Low=Val, Volume=1).
    /// Primarily for ITValuePublisher compatibility — TBar is the natural input for MARKETFI.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double v = double.IsFinite(input.Value) ? input.Value : _s.LastValid;
        // Scalar input: treat as zero-range bar with unit volume → MFI = 0
        return Update(new TBar(input.Time, v, v, v, v, 1.0), isNew);
    }

    /// <summary>
    /// Batch-computes MARKETFI over raw High/Low/Volume spans. Zero-allocation path.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="volume">Source volume</param>
    /// <param name="output">Destination span for MARKETFI values</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> volume,
        Span<double> output)
    {
        int len = high.Length;

        if (low.Length != len)
        {
            throw new ArgumentException("Low length must match high length", nameof(low));
        }

        if (volume.Length != len)
        {
            throw new ArgumentException("Volume length must match high length", nameof(volume));
        }

        if (output.Length != len)
        {
            throw new ArgumentException("Output length must match input length", nameof(output));
        }

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double v = double.IsFinite(volume[i]) ? volume[i] : 0.0;
            output[i] = v != 0.0 ? (h - l) / v : 0.0;
        }
    }

    /// <summary>Primes the indicator by replaying historical data without firing events.</summary>
    public void Prime(TBarSeries source)
    {
        foreach (var bar in source)
        {
            Update(bar, isNew: true);
        }
    }
}
