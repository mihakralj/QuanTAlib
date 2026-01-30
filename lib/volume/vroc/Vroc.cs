using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VROC: Volume Rate of Change
/// Measures the rate of change in volume over a specified period,
/// either as a percentage or as absolute point change.
/// </summary>
/// <remarks>
/// VROC Formula:
///   Percentage Mode: VROC = ((Current Volume - Historical Volume) / Historical Volume) × 100
///   Point Mode: VROC = Current Volume - Historical Volume
///
/// Key characteristics:
/// - Positive when current volume exceeds historical volume
/// - Negative when current volume is below historical volume
/// - Percentage mode normalizes across different securities
/// - Point mode shows absolute volume changes
///
/// Sources:
///   PineScript reference: vroc.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Vroc : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int Head,
        int Count,
        double LastValidVolume,
        int Index);

    private State _s;
    private State _ps;
    private readonly int _period;
    private readonly bool _usePercent;
    private readonly double[] _buffer;
    private double[]? _pBuffer;

    /// <inheritdoc/>
    public TValue Last { get; private set; }
    /// <inheritdoc/>
    public bool IsHot => _s.Index > _period;
    /// <inheritdoc/>
    public int WarmupPeriod => _period + 1;
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the VROC indicator.
    /// </summary>
    /// <param name="period">The lookback period (default: 12).</param>
    /// <param name="usePercent">True for percentage mode, false for point change (default: true).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Vroc(int period = 12, bool usePercent = true)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _usePercent = usePercent;
        _buffer = new double[period + 1]; // Need period + 1 to store historical value
        Name = $"Vroc({period},{(usePercent ? "%" : "pt")})";
        Reset();
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(Head: 0, Count: 0, LastValidVolume: 0, Index: 0);
        _ps = _s;
        Array.Clear(_buffer);
        _pBuffer = null;
        Last = default;
    }

    /// <summary>
    /// Updates the VROC with a new bar.
    /// </summary>
    /// <param name="input">The bar data.</param>
    /// <param name="isNew">True if this is a new bar, false if updating current bar.</param>
    /// <returns>The current VROC value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _pBuffer = (double[])_buffer.Clone();
        }
        else
        {
            _s = _ps;
            if (_pBuffer != null)
            {
                Array.Copy(_pBuffer, _buffer, _buffer.Length);
            }
        }

        var s = _s;
        int bufLen = _period + 1;

        // Handle NaN/Infinity - substitute with last valid value
        double volume = double.IsFinite(input.Volume) && input.Volume >= 0 ? input.Volume : s.LastValidVolume;
        if (double.IsFinite(input.Volume) && input.Volume >= 0)
        {
            s.LastValidVolume = input.Volume;
        }

        double vrocResult;

        // Store current volume in buffer
        _buffer[s.Head] = volume;

        if (s.Count < _period)
        {
            // Still filling the buffer - not enough history yet
            s.Head = (s.Head + 1) % bufLen;
            s.Count++;
            vrocResult = 0;
        }
        else
        {
            // Get historical volume (the value 'period' positions back)
            // With ring buffer of size period+1, historical is at (head - period + bufLen) % bufLen
            // which simplifies to (head + 1) % bufLen when count >= period
            int histIdx = (s.Head + 1) % bufLen;
            double historicalVolume = _buffer[histIdx];

            // Advance head for next iteration
            s.Head = (s.Head + 1) % bufLen;
            if (s.Count < bufLen)
            {
                s.Count++;
            }

            // Calculate VROC
            if (_usePercent)
            {
                vrocResult = historicalVolume > 0
                    ? ((volume - historicalVolume) / historicalVolume) * 100.0
                    : 0.0;
            }
            else
            {
                vrocResult = volume - historicalVolume;
            }
        }

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, vrocResult);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VROC with a TValue input.
    /// </summary>
    /// <remarks>
    /// VROC requires volume data for proper calculation. Using TValue without volume data
    /// will keep VROC unchanged.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // VROC requires volume; without it, we can't compute
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, Last.Value);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VROC with a series of bars (batch mode).
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
    /// Calculates VROC for a series of bars (static batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <param name="period">The lookback period (default: 12).</param>
    /// <param name="usePercent">True for percentage mode, false for point change (default: true).</param>
    /// <returns>The result series.</returns>
    public static TSeries Calculate(TBarSeries source, int period = 12, bool usePercent = true)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.Volume.Values, v, period, usePercent);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates VROC for spans of volume data (high-performance span mode).
    /// </summary>
    /// <param name="volume">The volume span.</param>
    /// <param name="output">The output VROC span.</param>
    /// <param name="period">The lookback period (default: 12).</param>
    /// <param name="usePercent">True for percentage mode, false for point change (default: true).</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> volume, Span<double> output, int period = 12, bool usePercent = true)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }
        if (volume.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        int len = volume.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidVolume = 1.0;

        for (int i = 0; i < len; i++)
        {
            // Get valid volume
            double vol = double.IsFinite(volume[i]) && volume[i] >= 0 ? volume[i] : lastValidVolume;
            if (double.IsFinite(volume[i]) && volume[i] >= 0)
            {
                lastValidVolume = volume[i];
            }

            if (i < period)
            {
                // Not enough history
                output[i] = 0;
            }
            else
            {
                // Get historical volume
                double histVol = double.IsFinite(volume[i - period]) && volume[i - period] >= 0
                    ? volume[i - period]
                    : lastValidVolume;

                if (usePercent)
                {
                    output[i] = histVol > 0
                        ? ((vol - histVol) / histVol) * 100.0
                        : 0.0;
                }
                else
                {
                    output[i] = vol - histVol;
                }
            }
        }
    }
}