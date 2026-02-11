using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MFI: Money Flow Index
/// </summary>
/// <remarks>
/// Volume-weighted RSI measuring buying/selling pressure for overbought/oversold conditions.
/// Oscillates 0-100; above 80 indicates overbought, below 20 oversold.
///
/// Calculation: <c>TP = (H+L+C)/3</c>, <c>MFR = Sum(Positive_MF) / Sum(Negative_MF)</c>,
/// <c>MFI = 100 - (100 / (1 + MFR))</c>.
/// </remarks>
/// <seealso href="Mfi.md">Detailed documentation</seealso>
/// <seealso href="mfi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Mfi : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _posMfBuffer;
    private readonly RingBuffer _negMfBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumPosMf,
        double SumNegMf,
        double PrevTypicalPrice,
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
    /// Current MFI value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed enough bars (period).
    /// </summary>
    public bool IsHot => _s.Index >= _period;

    /// <summary>
    /// Warmup period required before the indicator is considered hot.
    /// </summary>
    public int WarmupPeriod => _period;

    /// <summary>
    /// Creates a new MFI indicator.
    /// </summary>
    /// <param name="period">Lookback period (default: 14)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Mfi(int period = 14)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _posMfBuffer = new RingBuffer(period);
        _negMfBuffer = new RingBuffer(period);
        Name = $"Mfi({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _posMfBuffer.Clear();
        _negMfBuffer.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _posMfBuffer.Snapshot();
            _negMfBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _posMfBuffer.Restore();
            _negMfBuffer.Restore();
        }

        var s = _s;

        // Handle NaN/Infinity in volume
        double volume = double.IsFinite(input.Volume) ? input.Volume : s.LastValidVolume;
        if (double.IsFinite(input.Volume))
        {
            s.LastValidVolume = input.Volume;
        }

        // Calculate typical price
        double typicalPrice = (input.High + input.Low + input.Close) / 3.0;

        // Calculate raw money flow
        double rawMoneyFlow = typicalPrice * volume;

        // Determine if positive or negative money flow
        double posMf = 0;
        double negMf = 0;

        if (s.Index > 0)
        {
            if (typicalPrice > s.PrevTypicalPrice)
            {
                posMf = rawMoneyFlow;
            }
            else if (typicalPrice < s.PrevTypicalPrice)
            {
                negMf = rawMoneyFlow;
            }
            // If equal, both remain 0 (neutral)
        }

        // Update rolling sums
        if (_posMfBuffer.IsFull)
        {
            s.SumPosMf -= _posMfBuffer.Oldest;
            s.SumNegMf -= _negMfBuffer.Oldest;
        }

        _posMfBuffer.Add(posMf);
        _negMfBuffer.Add(negMf);
        s.SumPosMf += posMf;
        s.SumNegMf += negMf;

        // Store for next iteration
        s.PrevTypicalPrice = typicalPrice;

        if (isNew)
        {
            s.Index++;
        }

        // Calculate MFI
        double mfiValue;
        if (s.SumNegMf > double.Epsilon)
        {
            double ratio = s.SumPosMf / s.SumNegMf;
            mfiValue = 100.0 - (100.0 / (1.0 + ratio));
        }
        else if (s.SumPosMf > double.Epsilon)
        {
            // All positive flow, no negative
            mfiValue = 100.0;
        }
        else
        {
            // No flow at all
            mfiValue = 50.0;
        }

        _s = s;

        Last = new TValue(input.Time, mfiValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates MFI with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// MFI requires OHLCV bar data to calculate Typical Price and Money Flow.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "MFI requires OHLCV bar data to calculate Typical Price and Money Flow. " +
            "Use Update(TBar) instead.");
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

    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 14)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and Low spans must be of the same length", nameof(low));
        }

        if (high.Length != close.Length)
        {
            throw new ArgumentException("High and Close spans must be of the same length", nameof(close));
        }

        if (high.Length != volume.Length)
        {
            throw new ArgumentException("High and Volume spans must be of the same length", nameof(volume));
        }

        if (high.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Calculate typical prices
        Span<double> tp = len <= 256 ? stackalloc double[len] : new double[len];
        for (int i = 0; i < len; i++)
        {
            tp[i] = (high[i] + low[i] + close[i]) / 3.0;
        }

        // Calculate positive and negative money flows
        Span<double> posMf = len <= 256 ? stackalloc double[len] : new double[len];
        Span<double> negMf = len <= 256 ? stackalloc double[len] : new double[len];

        posMf[0] = 0;
        negMf[0] = 0;

        for (int i = 1; i < len; i++)
        {
            double rawMf = tp[i] * volume[i];

            if (tp[i] > tp[i - 1])
            {
                posMf[i] = rawMf;
                negMf[i] = 0;
            }
            else if (tp[i] < tp[i - 1])
            {
                posMf[i] = 0;
                negMf[i] = rawMf;
            }
            else
            {
                posMf[i] = 0;
                negMf[i] = 0;
            }
        }

        // Calculate MFI using rolling sums
        double sumPos = 0;
        double sumNeg = 0;

        for (int i = 0; i < len; i++)
        {
            sumPos += posMf[i];
            sumNeg += negMf[i];

            if (i >= period)
            {
                sumPos -= posMf[i - period];
                sumNeg -= negMf[i - period];
            }

            if (sumNeg > double.Epsilon)
            {
                double ratio = sumPos / sumNeg;
                output[i] = 100.0 - (100.0 / (1.0 + ratio));
            }
            else if (sumPos > double.Epsilon)
            {
                output[i] = 100.0;
            }
            else
            {
                output[i] = 50.0;
            }
        }
    }

    public static (TSeries Results, Mfi Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Mfi(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}