using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Bill Williams Market Facilitation Index (BW_MFI) with 4-zone classification,
/// measuring price movement efficiency per unit of volume and categorizing each bar into
/// one of four market states based on MFI and volume direction changes.
/// </summary>
/// <remarks>
/// BW_MFI Formula:
/// <c>MFI = (High − Low) / Volume</c>,
/// Zone classification by comparing current vs previous bar:
/// <c>Zone 1 (Green):  MFI↑ + Volume↑ → trend continuation</c>,
/// <c>Zone 2 (Fade):   MFI↓ + Volume↓ → fading momentum</c>,
/// <c>Zone 3 (Fake):   MFI↑ + Volume↓ → fake breakout</c>,
/// <c>Zone 4 (Squat):  MFI↓ + Volume↑ → accumulation/distribution</c>.
///
/// Zone 4 (Squat) is the most significant: large volume with small range indicates a
/// battle between bulls and bears, often preceding a breakout. Zone 1 (Green) confirms
/// trend strength. Zone 3 (Fake) warns of unsupported price moves.
/// This implementation is optimized for streaming updates with O(1) per bar.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="BwMfi.md">Detailed documentation</seealso>
/// <seealso href="bw_mfi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class BwMfi : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValid,
        double PrevMfi,
        double PrevVolume,
        int Count);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid zone output (2 — need previous bar for comparison).</summary>
    public static int WarmupPeriod => 2;

    /// <summary>True when at least two bars have been processed (zone classification requires comparison).</summary>
    public bool IsHot => _s.Count >= 2;

    /// <summary>Current BW_MFI value (price range per unit of volume).</summary>
    public TValue Last { get; private set; }

    /// <summary>Current zone classification (1=Green, 2=Fade, 3=Fake, 4=Squat, 0=insufficient data).</summary>
    public int Zone { get; private set; }

    public event TValuePublishedHandler? Pub;

    /// <summary>Creates a BW_MFI indicator.</summary>
    public BwMfi()
    {
        _s = new State(0.0, 0.0, 0.0, 0);
        _ps = _s;
        Name = "BwMfi";
        _barHandler = HandleBar;
    }

    /// <summary>Creates BW_MFI chained to a TBarSeries source.</summary>
    public BwMfi(TBarSeries source) : this()
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
        _s = new State(0.0, 0.0, 0.0, 0);
        _ps = _s;
        Last = default;
        Zone = 0;
    }

    /// <summary>
    /// Updates BW_MFI with a new OHLCV bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current BW_MFI value as TValue</returns>
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
            int count = s.Count;
            s = _ps;
            s.Count = count;
        }

        // Sanitize OHLCV inputs — use last-valid on NaN/Infinity
        double high = double.IsFinite(input.High) ? input.High : s.LastValid;
        double low = double.IsFinite(input.Low) ? input.Low : s.LastValid;
        double volume = double.IsFinite(input.Volume) ? input.Volume : 0.0;

        // Core formula: price range per unit of volume
        double mfi = volume != 0.0 ? (high - low) / volume : 0.0;

        if (double.IsFinite(mfi))
        {
            s.LastValid = mfi;
        }
        else
        {
            mfi = s.LastValid;
        }

        // Zone classification: requires previous bar comparison
        int zone;
        if (s.Count < 2)
        {
            zone = 0; // insufficient data
        }
        else
        {
            bool mfiUp = mfi > s.PrevMfi;
            bool volUp = volume > s.PrevVolume;

            if (mfiUp && volUp)
            {
                zone = 1; // Green: trend continuation
            }
            else if (!mfiUp && !volUp)
            {
                zone = 2; // Fade: fading momentum
            }
            else if (mfiUp && !volUp)
            {
                zone = 3; // Fake: unsupported price move
            }
            else
            {
                zone = 4; // Squat: accumulation/distribution
            }
        }

        // Store current values for next comparison
        s.PrevMfi = mfi;
        s.PrevVolume = volume;

        _s = s;
        Zone = zone;

        Last = new TValue(input.Time, mfi);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates BW_MFI from a scalar TValue (uses Val as proxy; High=Low=Val, Volume=1).
    /// Primarily for ITValuePublisher compatibility — TBar is the natural input for BW_MFI.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double v = double.IsFinite(input.Value) ? input.Value : _s.LastValid;
        return Update(new TBar(input.Time, v, v, v, v, 1.0), isNew);
    }

    /// <summary>
    /// Batch-computes BW_MFI and zones over raw High/Low/Volume spans. Zero-allocation path.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="volume">Source volume</param>
    /// <param name="mfiOutput">Destination span for MFI values</param>
    /// <param name="zoneOutput">Destination span for zone classifications (1-4, 0 for first bar)</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> volume,
        Span<double> mfiOutput,
        Span<int> zoneOutput)
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
        if (mfiOutput.Length != len)
        {
            throw new ArgumentException("MFI output length must match input length", nameof(mfiOutput));
        }
        if (zoneOutput.Length != len)
        {
            throw new ArgumentException("Zone output length must match input length", nameof(zoneOutput));
        }

        if (len == 0)
        {
            return;
        }

        // First bar: compute MFI, zone = 0 (no previous to compare)
        double v0 = double.IsFinite(volume[0]) ? volume[0] : 0.0;
        double mfi0 = v0 != 0.0 ? (high[0] - low[0]) / v0 : 0.0;
        mfiOutput[0] = mfi0;
        zoneOutput[0] = 0;

        double prevMfi = mfi0;
        double prevVol = v0;

        for (int i = 1; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double vol = double.IsFinite(volume[i]) ? volume[i] : 0.0;
            double mfi = vol != 0.0 ? (h - l) / vol : 0.0;
            mfiOutput[i] = mfi;

            bool mfiUp = mfi > prevMfi;
            bool volUp = vol > prevVol;

            if (mfiUp && volUp)
            {
                zoneOutput[i] = 1;
            }
            else if (!mfiUp && !volUp)
            {
                zoneOutput[i] = 2;
            }
            else if (mfiUp && !volUp)
            {
                zoneOutput[i] = 3;
            }
            else
            {
                zoneOutput[i] = 4;
            }

            prevMfi = mfi;
            prevVol = vol;
        }
    }

    /// <summary>
    /// Batch-computes BW_MFI values only (without zones) over raw spans. Zero-allocation path.
    /// </summary>
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
