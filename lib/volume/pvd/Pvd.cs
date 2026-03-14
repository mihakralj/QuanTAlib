using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Price Volume Divergence (PVD) that measures divergence between price momentum
/// and volume momentum, detecting situations where price and volume move in opposite directions.
/// </summary>
/// <remarks>
/// PVD Formula:
/// <c>Price_ROC = ((Close - Close[pricePeriod]) / Close[pricePeriod]) × 100</c>,
/// <c>Volume_ROC = ((Volume - Volume[volumePeriod]) / Volume[volumePeriod]) × 100</c>,
/// <c>Raw_Divergence = Sign(Price_ROC) × -Sign(Volume_ROC) × (|Price_ROC| + |Volume_ROC|)</c>,
/// <c>PVD = SMA(Raw_Divergence, smoothingPeriod)</c>.
///
/// Positive values indicate price up/volume down or price down/volume up divergence;
/// negative values indicate price and volume moving in same direction.
/// This implementation is optimized for streaming updates with O(1) per bar using ring buffers.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Pvd.md">Detailed documentation</seealso>
/// <seealso href="pvd.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pvd : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidClose,
        double LastValidVolume,
        double LastValidPvd,
        int Index);

    private State _s;
    private State _ps;
    private readonly RingBuffer _priceBuffer;
    private readonly RingBuffer _volumeBuffer;
    private readonly RingBuffer _divergenceBuffer;
    private readonly int _pricePeriod;
    private readonly int _volumePeriod;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _s.Index >= WarmupPeriod;
    public int WarmupPeriod { get; }
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of PVD.
    /// </summary>
    /// <param name="pricePeriod">Lookback period for price momentum (default 14).</param>
    /// <param name="volumePeriod">Lookback period for volume momentum (default 14).</param>
    /// <param name="smoothingPeriod">Period for smoothing divergence (default 3).</param>
    /// <exception cref="ArgumentException">Thrown when any period is less than 1.</exception>
    public Pvd(int pricePeriod = 14, int volumePeriod = 14, int smoothingPeriod = 3)
    {
        if (pricePeriod < 1)
        {
            throw new ArgumentException("Price period must be >= 1", nameof(pricePeriod));
        }

        if (volumePeriod < 1)
        {
            throw new ArgumentException("Volume period must be >= 1", nameof(volumePeriod));
        }

        if (smoothingPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be >= 1", nameof(smoothingPeriod));
        }

        _pricePeriod = pricePeriod;
        _volumePeriod = volumePeriod;
        WarmupPeriod = Math.Max(pricePeriod, volumePeriod) + smoothingPeriod;
        Name = $"Pvd({pricePeriod},{volumePeriod},{smoothingPeriod})";

        _priceBuffer = new RingBuffer(pricePeriod + 1);
        _volumeBuffer = new RingBuffer(volumePeriod + 1);
        _divergenceBuffer = new RingBuffer(smoothingPeriod);
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _priceBuffer.Clear();
        _volumeBuffer.Clear();
        _divergenceBuffer.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }

    /// <summary>
    /// Updates the PVD indicator with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _priceBuffer.Snapshot();
            _volumeBuffer.Snapshot();
            _divergenceBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _priceBuffer.Restore();
            _volumeBuffer.Restore();
            _divergenceBuffer.Restore();
        }

        var s = _s;

        // Handle NaN/Infinity in close price
        double close = double.IsFinite(input.Close) ? input.Close : s.LastValidClose;
        if (double.IsFinite(input.Close))
        {
            s.LastValidClose = input.Close;
        }

        // Handle NaN/Infinity in volume
        double volume = double.IsFinite(input.Volume) ? input.Volume : s.LastValidVolume;
        if (double.IsFinite(input.Volume))
        {
            s.LastValidVolume = input.Volume;
        }

        // Add to buffers
        _priceBuffer.Add(close);
        _volumeBuffer.Add(volume);

        if (isNew)
        {
            s.Index++;
        }

        double pvdValue;

        if (_priceBuffer.Count <= _pricePeriod || _volumeBuffer.Count <= _volumePeriod)
        {
            pvdValue = 0.0;
        }
        else
        {
            // Get previous values for ROC calculation
            double prevClose = _priceBuffer[_priceBuffer.Count - 1 - _pricePeriod];
            double prevVolumeRaw = _volumeBuffer[_volumeBuffer.Count - 1 - _volumePeriod];

            // Clamp volumes to non-negative (matching static Calculate behavior)
            double currVolume = Math.Max(volume, 0.0);
            double prevVolume = Math.Max(prevVolumeRaw, 0.0);

            // Calculate ROC percentages
            double priceRoc = prevClose > 0 ? (close - prevClose) / prevClose * 100.0 : 0.0;
            double volumeRoc = prevVolume > 0 ? (currVolume - prevVolume) / prevVolume * 100.0 : 0.0;

            // Get momentum signs
            int priceMomentum;
            if (priceRoc > 0)
            {
                priceMomentum = 1;
            }
            else if (priceRoc < 0)
            {
                priceMomentum = -1;
            }
            else
            {
                priceMomentum = 0;
            }

            int volumeMomentum;
            if (volumeRoc > 0)
            {
                volumeMomentum = 1;
            }
            else if (volumeRoc < 0)
            {
                volumeMomentum = -1;
            }
            else
            {
                volumeMomentum = 0;
            }

            // Calculate magnitude and raw divergence
            double magnitude = Math.Abs(priceRoc) + Math.Abs(volumeRoc);
            double divergenceRaw = priceMomentum * -volumeMomentum * magnitude;

            // Add to smoothing buffer
            _divergenceBuffer.Add(divergenceRaw);

            // Calculate smoothed value (SMA of divergence)
            double sum = 0.0;
            int count = _divergenceBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                sum += _divergenceBuffer[i];
            }

            pvdValue = count > 0 ? sum / count : divergenceRaw;
        }

        s.LastValidPvd = pvdValue;
        _s = s;

        Last = new TValue(input.Time, pvdValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVD with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// PVD requires OHLCV bar data to calculate Price and Volume ROC.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "PVD requires OHLCV bar data to calculate Price and Volume ROC. " +
            "Use Update(TBar) instead.");
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            TValue val = Update(source[i], isNew: true);
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

    public static TSeries Batch(TBarSeries source, int pricePeriod = 14, int volumePeriod = 14, int smoothingPeriod = 3)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Close.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, v, pricePeriod, volumePeriod, smoothingPeriod);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output,
        int pricePeriod = 14, int volumePeriod = 14, int smoothingPeriod = 3)
    {
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }

        if (close.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (pricePeriod < 1)
        {
            throw new ArgumentException("Price period must be >= 1", nameof(pricePeriod));
        }

        if (volumePeriod < 1)
        {
            throw new ArgumentException("Volume period must be >= 1", nameof(volumePeriod));
        }

        if (smoothingPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be >= 1", nameof(smoothingPeriod));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        int maxPeriod = Math.Max(pricePeriod, volumePeriod);

        // Allocate buffer for raw divergence
        Span<double> rawDivergence = len <= 256 ? stackalloc double[len] : new double[len];

        // Calculate raw divergence for each bar (use NaN to mark invalid entries)
        for (int i = 0; i < len; i++)
        {
            if (i < maxPeriod)
            {
                rawDivergence[i] = double.NaN; // Mark as invalid - no ROC data yet
                continue;
            }

            double currClose = close[i];
            double currVolume = Math.Max(volume[i], 0.0);
            double prevClose = close[i - pricePeriod];
            double prevVolume = Math.Max(volume[i - volumePeriod], 0.0);

            double priceRoc = prevClose > 0 ? (currClose - prevClose) / prevClose * 100.0 : 0.0;
            double volumeRoc = prevVolume > 0 ? (currVolume - prevVolume) / prevVolume * 100.0 : 0.0;

            int priceMomentum;
            if (priceRoc > 0)
            {
                priceMomentum = 1;
            }
            else if (priceRoc < 0)
            {
                priceMomentum = -1;
            }
            else
            {
                priceMomentum = 0;
            }

            int volumeMomentum;
            if (volumeRoc > 0)
            {
                volumeMomentum = 1;
            }
            else if (volumeRoc < 0)
            {
                volumeMomentum = -1;
            }
            else
            {
                volumeMomentum = 0;
            }

            double magnitude = Math.Abs(priceRoc) + Math.Abs(volumeRoc);
            rawDivergence[i] = priceMomentum * -volumeMomentum * magnitude;
        }

        // Apply SMA smoothing (only over valid divergence entries, skip NaN)
        for (int i = 0; i < len; i++)
        {
            if (i < maxPeriod)
            {
                // No valid divergence data yet - output 0 (matching instance behavior)
                output[i] = 0.0;
            }
            else
            {
                // Calculate SMA over valid entries in the smoothing window
                double sum = 0.0;
                int validCount = 0;
                int windowStart = Math.Max(maxPeriod, i - smoothingPeriod + 1);

                for (int j = windowStart; j <= i; j++)
                {
                    sum += rawDivergence[j];
                    validCount++;
                }

                output[i] = validCount > 0 ? sum / validCount : 0.0;
            }
        }
    }

    public static (TSeries Results, Pvd Indicator) Calculate(TBarSeries source, int pricePeriod = 14, int volumePeriod = 14, int smoothingPeriod = 3)
    {
        var indicator = new Pvd(pricePeriod, volumePeriod, smoothingPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}