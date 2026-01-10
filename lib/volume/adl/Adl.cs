using System.Runtime.CompilerServices;
using System.Numerics;

namespace QuanTAlib;

/// <summary>
/// ADL: Accumulation/Distribution Line
/// </summary>
/// <remarks>
/// The Accumulation/Distribution Line is a cumulative indicator that uses volume and price
/// to assess whether a stock is being accumulated or distributed.
///
/// Calculation:
/// 1. Money Flow Multiplier = [(Close - Low) - (High - Close)] / (High - Low)
/// 2. Money Flow Volume = Money Flow Multiplier * Volume
/// 3. ADL = Previous ADL + Money Flow Volume
///
/// If High equals Low, the Multiplier is 0.
///
/// Sources:
/// https://www.investopedia.com/terms/a/accumulationdistribution.asp
/// https://school.stockcharts.com/doku.php?id=technical_indicators:accumulation_distribution_line
/// </remarks>
[SkipLocalsInit]
public sealed class Adl : ITValuePublisher
{
    private double _adl;
    private double _p_adl;
    private bool _isInitialized;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public static string Name => "ADL";

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current ADL value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least one bar.
    /// </summary>
    public bool IsHot => _isInitialized;

    /// <summary>
    /// Creates a new ADL indicator.
    /// </summary>
    public Adl()
    {
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _adl = 0;
        _p_adl = 0;
        _isInitialized = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_adl = _adl;
        }
        else
        {
            _adl = _p_adl;
        }

        double highLowRange = input.High - input.Low;
        double mfm = 0;

        if (highLowRange > double.Epsilon)
        {
            mfm = (input.Close - input.Low - (input.High - input.Close)) / highLowRange;
        }

        double mfv = mfm * input.Volume;
        _adl += mfv;

        _isInitialized = true;
        Last = new TValue(input.Time, _adl);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = true });
        return Last;
    }

    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_adl = _adl;
        }
        else
        {
            _adl = _p_adl;
        }

        Last = new TValue(input.Time, _adl);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = true });
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
        if (source.Count == 0) return [];

        var t = source.Open.Times; // Times are same for all series
        var v = new double[source.Count];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v);

        return new TSeries([.. t], [.. v]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (high.Length != low.Length || high.Length != close.Length || high.Length != volume.Length || high.Length != output.Length)
            throw new ArgumentException("All spans must be of the same length", nameof(output));

        int len = high.Length;
        int i = 0;

        if (Vector.IsHardwareAccelerated && len >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var epsilon = new Vector<double>(double.Epsilon);

            for (; i <= len - vectorSize; i += vectorSize)
            {
                var h = new Vector<double>(high.Slice(i, vectorSize));
                var l = new Vector<double>(low.Slice(i, vectorSize));
                var c = new Vector<double>(close.Slice(i, vectorSize));
                var vol = new Vector<double>(volume.Slice(i, vectorSize));

                var hl = h - l;
                var num = c - l - (h - c);

                var mask = Vector.GreaterThan(hl, epsilon);
                var safeHl = Vector.ConditionalSelect(mask, hl, Vector<double>.One);
                var mfm = num / safeHl;
                mfm = Vector.ConditionalSelect(mask, mfm, Vector<double>.Zero);

                var mfv = mfm * vol;
                mfv.CopyTo(output.Slice(i, vectorSize));
            }
        }

        for (; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double vol = volume[i];

            double hl = h - l;
            double mfm = 0;
            if (hl > double.Epsilon)
            {
                mfm = (c - l - (h - c)) / hl;
            }
            output[i] = mfm * vol;
        }

        double sum = 0;
        for (i = 0; i < len; i++)
        {
            sum += output[i];
            output[i] = sum;
        }
    }
}
