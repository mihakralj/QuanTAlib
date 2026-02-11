using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Oscillator (VO) measuring the difference between two volume moving averages.
/// </summary>
/// <remarks>
/// VO compares short and long volume SMAs: <c>VO = ((SMA(vol,short) - SMA(vol,long)) / SMA(vol,long)) × 100</c>,
/// with optional signal line: <c>Signal = SMA(VO, signalPeriod)</c>.
///
/// This implementation is optimized for streaming updates with O(1) per bar using running sums.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Vo.md">Detailed documentation</seealso>
/// <seealso href="vo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Vo : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumShort,
        double SumLong,
        double SumSignal,
        int HeadShort,
        int HeadLong,
        int HeadSignal,
        int CountShort,
        int CountLong,
        int CountSignal,
        double LastValidVolume,
        double SignalValue,
        int Index);

    private State _s;
    private State _ps;
    private readonly int _shortPeriod;
    private readonly int _longPeriod;
    private readonly int _signalPeriod;
    private readonly double[] _bufferShort;
    private readonly double[] _bufferLong;
    private readonly double[] _bufferSignal;
    private double[]? _pBufferShort;
    private double[]? _pBufferLong;
    private double[]? _pBufferSignal;

    /// <inheritdoc/>
    public TValue Last { get; private set; }
    /// <summary>Gets the current signal line value.</summary>
    public double Signal => _s.SignalValue;
    /// <inheritdoc/>
    public bool IsHot => _s.Index >= _longPeriod;
    /// <inheritdoc/>
    public int WarmupPeriod => _longPeriod;
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the VO indicator.
    /// </summary>
    /// <param name="shortPeriod">The short-term period (default: 5).</param>
    /// <param name="longPeriod">The long-term period (default: 10).</param>
    /// <param name="signalPeriod">The signal line period (default: 10).</param>
    /// <exception cref="ArgumentException">Thrown when periods are invalid.</exception>
    public Vo(int shortPeriod = 5, int longPeriod = 10, int signalPeriod = 10)
    {
        if (shortPeriod < 1)
        {
            throw new ArgumentException("Short period must be at least 1", nameof(shortPeriod));
        }
        if (longPeriod < 1)
        {
            throw new ArgumentException("Long period must be at least 1", nameof(longPeriod));
        }
        if (shortPeriod >= longPeriod)
        {
            throw new ArgumentException("Short period must be less than long period", nameof(shortPeriod));
        }
        if (signalPeriod < 1)
        {
            throw new ArgumentException("Signal period must be at least 1", nameof(signalPeriod));
        }

        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;
        _signalPeriod = signalPeriod;
        _bufferShort = new double[shortPeriod];
        _bufferLong = new double[longPeriod];
        _bufferSignal = new double[signalPeriod];
        Name = $"Vo({shortPeriod},{longPeriod},{signalPeriod})";
        Reset();
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(
            SumShort: 0, SumLong: 0, SumSignal: 0,
            HeadShort: 0, HeadLong: 0, HeadSignal: 0,
            CountShort: 0, CountLong: 0, CountSignal: 0,
            LastValidVolume: 0, SignalValue: 0, Index: 0);
        _ps = _s;
        Array.Clear(_bufferShort);
        Array.Clear(_bufferLong);
        Array.Clear(_bufferSignal);
        _pBufferShort = null;
        _pBufferLong = null;
        _pBufferSignal = null;
        Last = default;
    }

    /// <summary>
    /// Updates the VO with a new bar.
    /// </summary>
    /// <param name="input">The bar data.</param>
    /// <param name="isNew">True if this is a new bar, false if updating current bar.</param>
    /// <returns>The current VO value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _pBufferShort = (double[])_bufferShort.Clone();
            _pBufferLong = (double[])_bufferLong.Clone();
            _pBufferSignal = (double[])_bufferSignal.Clone();
        }
        else
        {
            _s = _ps;
            if (_pBufferShort != null)
            {
                Array.Copy(_pBufferShort, _bufferShort, _shortPeriod);
            }
            if (_pBufferLong != null)
            {
                Array.Copy(_pBufferLong, _bufferLong, _longPeriod);
            }
            if (_pBufferSignal != null)
            {
                Array.Copy(_pBufferSignal, _bufferSignal, _signalPeriod);
            }
        }

        var s = _s;

        // Handle NaN/Infinity - substitute with last valid value
        double volume = double.IsFinite(input.Volume) && input.Volume >= 0 ? input.Volume : s.LastValidVolume;
        if (double.IsFinite(input.Volume) && input.Volume >= 0)
        {
            s.LastValidVolume = input.Volume;
        }

        // Ensure minimum volume of 1 to avoid division issues
        volume = Math.Max(volume, 1.0);

        // Update short SMA buffer
        if (s.CountShort >= _shortPeriod)
        {
            s.SumShort -= _bufferShort[s.HeadShort];
        }
        else
        {
            s.CountShort++;
        }
        _bufferShort[s.HeadShort] = volume;
        s.SumShort += volume;
        s.HeadShort = (s.HeadShort + 1) % _shortPeriod;

        // Update long SMA buffer
        if (s.CountLong >= _longPeriod)
        {
            s.SumLong -= _bufferLong[s.HeadLong];
        }
        else
        {
            s.CountLong++;
        }
        _bufferLong[s.HeadLong] = volume;
        s.SumLong += volume;
        s.HeadLong = (s.HeadLong + 1) % _longPeriod;

        // Calculate SMAs
        double shortMa = s.CountShort > 0 ? s.SumShort / s.CountShort : volume;
        double longMa = s.CountLong > 0 ? s.SumLong / s.CountLong : volume;

        // Calculate VO
        double voValue = longMa > 0 ? ((shortMa - longMa) / longMa) * 100.0 : 0.0;

        // Update signal SMA buffer
        if (s.CountSignal >= _signalPeriod)
        {
            s.SumSignal -= _bufferSignal[s.HeadSignal];
        }
        else
        {
            s.CountSignal++;
        }
        _bufferSignal[s.HeadSignal] = voValue;
        s.SumSignal += voValue;
        s.HeadSignal = (s.HeadSignal + 1) % _signalPeriod;

        // Calculate signal line
        s.SignalValue = s.CountSignal > 0 ? s.SumSignal / s.CountSignal : voValue;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        Last = new TValue(input.Time, voValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the VO with a TValue input.
    /// </summary>
    /// <remarks>
    /// VO requires volume data for proper calculation. Using TValue without volume data
    /// will keep VO unchanged.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // VO requires volume; without it, we can't compute
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
    /// Updates the VO with a series of bars (batch mode).
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
    /// Calculates VO for a series of bars (static batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <param name="shortPeriod">The short-term period (default: 5).</param>
    /// <param name="longPeriod">The long-term period (default: 10).</param>
    /// <param name="signalPeriod">The signal line period (default: 10).</param>
    /// <returns>The result series.</returns>
    public static TSeries Batch(TBarSeries source, int shortPeriod = 5, int longPeriod = 10, int signalPeriod = 10)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Volume.Values, v, shortPeriod, longPeriod);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates VO for spans of volume data (high-performance span mode).
    /// Note: This method computes only the VO values, not the signal line.
    /// For signal line computation, use the instance Update methods.
    /// </summary>
    /// <param name="volume">The volume span.</param>
    /// <param name="output">The output VO span.</param>
    /// <param name="shortPeriod">The short-term period (default: 5).</param>
    /// <param name="longPeriod">The long-term period (default: 10).</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> volume, Span<double> output, int shortPeriod = 5, int longPeriod = 10)
    {
        if (shortPeriod < 1)
        {
            throw new ArgumentException("Short period must be at least 1", nameof(shortPeriod));
        }
        if (longPeriod < 1)
        {
            throw new ArgumentException("Long period must be at least 1", nameof(longPeriod));
        }
        if (shortPeriod >= longPeriod)
        {
            throw new ArgumentException("Short period must be less than long period", nameof(shortPeriod));
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

        // Allocate buffers
        const int StackallocThreshold = 256;
        double[]? rentedShort = null;
        double[]? rentedLong = null;
        scoped Span<double> bufferShort;
        scoped Span<double> bufferLong;

        if (shortPeriod <= StackallocThreshold)
        {
            bufferShort = stackalloc double[shortPeriod];
        }
        else
        {
            rentedShort = System.Buffers.ArrayPool<double>.Shared.Rent(shortPeriod);
            bufferShort = rentedShort.AsSpan(0, shortPeriod);
        }

        if (longPeriod <= StackallocThreshold)
        {
            bufferLong = stackalloc double[longPeriod];
        }
        else
        {
            rentedLong = System.Buffers.ArrayPool<double>.Shared.Rent(longPeriod);
            bufferLong = rentedLong.AsSpan(0, longPeriod);
        }

        try
        {
            bufferShort.Clear();
            bufferLong.Clear();

            double sumShort = 0, sumLong = 0;
            int headShort = 0, headLong = 0;
            int countShort = 0, countLong = 0;
            double lastValidVolume = 1.0;

            for (int i = 0; i < len; i++)
            {
                // Get valid volume
                double vol = double.IsFinite(volume[i]) && volume[i] >= 0 ? volume[i] : lastValidVolume;
                if (double.IsFinite(volume[i]) && volume[i] >= 0)
                {
                    lastValidVolume = volume[i];
                }
                vol = Math.Max(vol, 1.0);

                // Update short SMA
                if (countShort >= shortPeriod)
                {
                    sumShort -= bufferShort[headShort];
                }
                else
                {
                    countShort++;
                }
                bufferShort[headShort] = vol;
                sumShort += vol;
                headShort = (headShort + 1) % shortPeriod;

                // Update long SMA
                if (countLong >= longPeriod)
                {
                    sumLong -= bufferLong[headLong];
                }
                else
                {
                    countLong++;
                }
                bufferLong[headLong] = vol;
                sumLong += vol;
                headLong = (headLong + 1) % longPeriod;

                // Calculate VO
                double shortMa = countShort > 0 ? sumShort / countShort : vol;
                double longMa = countLong > 0 ? sumLong / countLong : vol;
                output[i] = longMa > 0 ? ((shortMa - longMa) / longMa) * 100.0 : 0.0;
            }
        }
        finally
        {
            if (rentedShort != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedShort);
            }
            if (rentedLong != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedLong);
            }
        }
    }

    public static (TSeries Results, Vo Indicator) Calculate(TBarSeries source, int shortPeriod = 5, int longPeriod = 10, int signalPeriod = 10)
    {
        var indicator = new Vo(shortPeriod, longPeriod, signalPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}