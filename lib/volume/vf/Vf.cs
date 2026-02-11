using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Force (VF) indicator measuring the force of volume behind price movements.
/// </summary>
/// <remarks>
/// VF multiplies price change by volume with EMA smoothing:
/// <c>rawVF = (Close - prevClose) × Volume</c>, <c>VF = EMA(rawVF, period)</c>
/// with warmup compensation: <c>VF = compensator × EMA</c> where <c>compensator = 1 / (1 - e)</c>.
///
/// This implementation is optimized for streaming updates with O(1) per bar using EMA recursion.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Vf.md">Detailed documentation</seealso>
/// <seealso href="vf.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Vf : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double EmaValue,
        double E,
        double PrevClose,
        double LastValidClose,
        double LastValidVolume,
        bool Warmup,
        int Index);

    private State _s;
    private State _ps;
    private readonly int _period;
    private readonly double _alpha;

    /// <inheritdoc/>
    public TValue Last { get; private set; }
    /// <inheritdoc/>
    public bool IsHot => _s.Index >= _period;
    /// <inheritdoc/>
    public int WarmupPeriod => _period;
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the VF indicator.
    /// </summary>
    /// <param name="period">The smoothing period (default: 14).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Vf(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _alpha = 2.0 / (period + 1);
        Name = $"Vf({period})";
        Reset();
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(EmaValue: 0, E: 1, PrevClose: 0, LastValidClose: 0, LastValidVolume: 0, Warmup: true, Index: 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Updates the VF with a new bar.
    /// </summary>
    /// <param name="input">The bar data.</param>
    /// <param name="isNew">True if this is a new bar, false if updating current bar.</param>
    /// <returns>The current VF value.</returns>
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
        double close = double.IsFinite(input.Close) ? input.Close : s.LastValidClose;
        double volume = double.IsFinite(input.Volume) ? input.Volume : s.LastValidVolume;

        // Update last valid values
        if (double.IsFinite(input.Close) && input.Close > 0)
        {
            s.LastValidClose = input.Close;
        }
        if (double.IsFinite(input.Volume) && input.Volume >= 0)
        {
            s.LastValidVolume = input.Volume;
        }

        double vfResult;

        if (s.Index == 0)
        {
            // First bar: no previous close, raw_vf = 0
            s.PrevClose = close;
            s.EmaValue = 0;
            vfResult = 0;
        }
        else
        {
            // Calculate price change and raw VF
            double priceChange = close - s.PrevClose;
            double rawVf = priceChange * volume;

            // Update EMA: ema = alpha * (raw - ema) + ema = alpha * raw + (1 - alpha) * ema
            s.EmaValue = Math.FusedMultiplyAdd(_alpha, rawVf - s.EmaValue, s.EmaValue);

            // Apply warmup compensation
            if (s.Warmup)
            {
                s.E *= (1.0 - _alpha);
                double compensator = 1.0 / (1.0 - s.E);
                vfResult = compensator * s.EmaValue;
                s.Warmup = s.E > 1e-10;
            }
            else
            {
                vfResult = s.EmaValue;
            }

            // Store for next iteration
            s.PrevClose = close;
        }

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, vfResult);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VF with a TValue input.
    /// </summary>
    /// <remarks>
    /// VF requires volume data for proper calculation. This method throws NotSupportedException
    /// because TValue does not contain volume information. Use Update(TBar) instead.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown because VF requires volume data.</exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        // VF requires volume; TValue does not contain volume, so this operation is not supported
        throw new NotSupportedException("VF requires volume data. Use Update(TBar) instead of Update(TValue).");
    }

    /// <summary>
    /// Updates the VF with a series of bars (batch mode).
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
    /// Calculates VF for a series of bars (static batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <param name="period">The smoothing period (default: 14).</param>
    /// <returns>The result series.</returns>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates VF for spans of close and volume data (high-performance span mode).
    /// </summary>
    /// <param name="close">The close price span.</param>
    /// <param name="volume">The volume span.</param>
    /// <param name="output">The output VF span.</param>
    /// <param name="period">The smoothing period (default: 14).</param>
    /// <exception cref="ArgumentException">Thrown when span lengths don't match or period is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }
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

        double alpha = 2.0 / (period + 1);
        double emaValue = 0;
        double e = 1.0;
        bool warmup = true;

        double lastValidClose = close[0];
        double lastValidVolume = volume[0];

        // First bar: no previous close, VF = 0
        output[0] = 0;
        double prevClose = double.IsFinite(close[0]) ? close[0] : 0;
        if (double.IsFinite(close[0]) && close[0] > 0)
        {
            lastValidClose = close[0];
        }
        if (double.IsFinite(volume[0]) && volume[0] >= 0)
        {
            lastValidVolume = volume[0];
        }

        for (int i = 1; i < len; i++)
        {
            // Get valid values
            double c = double.IsFinite(close[i]) ? close[i] : lastValidClose;
            double v = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;

            // Update last valid values
            if (double.IsFinite(close[i]) && close[i] > 0)
            {
                lastValidClose = close[i];
            }
            if (double.IsFinite(volume[i]) && volume[i] >= 0)
            {
                lastValidVolume = volume[i];
            }

            // Calculate price change and raw VF
            double priceChange = c - prevClose;
            double rawVf = priceChange * v;

            // Update EMA
            emaValue = Math.FusedMultiplyAdd(alpha, rawVf - emaValue, emaValue);

            double vfResult;
            if (warmup)
            {
                e *= (1.0 - alpha);
                double compensator = 1.0 / (1.0 - e);
                vfResult = compensator * emaValue;
                warmup = e > 1e-10;
            }
            else
            {
                vfResult = emaValue;
            }

            output[i] = vfResult;
            prevClose = c;
        }
    }

    public static (TSeries Results, Vf Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Vf(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}