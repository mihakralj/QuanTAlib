using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PFE: Polarized Fractal Efficiency
/// Measures trend efficiency using fractal geometry: the ratio of the straight-line
/// distance to the total fractal path distance, signed by direction, smoothed with EMA.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>straightLine = sqrt((close - close[period])^2 + period^2)</item>
/// <item>fractalPath = sum(sqrt((close[i] - close[i+1])^2 + 1), i=0..period-1)</item>
/// <item>rawPfe = sign(close - close[period]) * (straightLine / fractalPath) * 100</item>
/// <item>pfe = EMA(rawPfe, smoothPeriod) with bias compensation</item>
/// </list>
///
/// <b>Sources:</b>
/// Hans Hannula, "Polarized Fractal Efficiency", TASC January 1994
/// </remarks>
/// <seealso href="Pfe.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Pfe : AbstractBase
{
    private readonly int _period;
    private readonly int _smoothPeriod;
    private readonly RingBuffer _closeBuffer;  // period+1 close values
    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _periodSquared;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema,
        double E,
        double LastRawPfe,
        double LastValidValue,
        int Count
    )
    {
        public bool IsCompensated => E <= 1e-10;
    }

    private State _s;
    private State _ps;

    /// <summary>
    /// Creates PFE with specified period and EMA smoothing period.
    /// </summary>
    /// <param name="period">Fractal path lookback period (must be &gt; 1, default 10)</param>
    /// <param name="smoothPeriod">EMA smoothing period (must be &gt; 0, default 5)</param>
    public Pfe(int period = 10, int smoothPeriod = 5)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }
        if (smoothPeriod < 1)
        {
            throw new ArgumentException("Smooth period must be greater than or equal to 1", nameof(smoothPeriod));
        }

        _period = period;
        _smoothPeriod = smoothPeriod;
        _closeBuffer = new RingBuffer(period + 1);
        _alpha = 2.0 / (smoothPeriod + 1);
        _decay = 1.0 - _alpha;
        _periodSquared = (double)period * period;
        Name = $"Pfe({period},{smoothPeriod})";
        WarmupPeriod = period + 1;
        _s = new State(0, 1.0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates PFE with specified source and parameters.
    /// </summary>
    public Pfe(ITValuePublisher source, int period = 10, int smoothPeriod = 5) : this(period, smoothPeriod)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True when close buffer has period+1 values (enough for full PFE calculation).
    /// </summary>
    public override bool IsHot => _s.E <= 0.05;

    /// <summary>
    /// Updates the indicator with a single TValue input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
            _closeBuffer.UpdateNewest(_closeBuffer.Newest);
        }

        var s = _s;

        // NaN/Infinity handling: last-valid substitution
        double val = input.Value;
        if (double.IsFinite(val))
        {
            s.LastValidValue = val;
        }
        else
        {
            val = s.LastValidValue;
        }

        if (isNew)
        {
            _closeBuffer.Add(val);
            s.Count++;
        }
        else
        {
            _closeBuffer.UpdateNewest(val);
        }

        // Calculate raw PFE when we have enough data
        double result;
        if (_closeBuffer.IsFull)
        {
            // Straight-line distance: sqrt((close - close[period])^2 + period^2)
            double currentClose = _closeBuffer.Newest;
            double laggedClose = _closeBuffer.Oldest;
            double priceDiff = currentClose - laggedClose;
            double straightLine = Math.Sqrt(Math.FusedMultiplyAdd(priceDiff, priceDiff, _periodSquared));

            // Fractal path: sum of bar-to-bar Euclidean distances
            double fractalPath = 0.0;
            int bufCount = _closeBuffer.Count;
            for (int i = 0; i < _period; i++)
            {
                double c1 = _closeBuffer[bufCount - 1 - i];
                double c2 = _closeBuffer[bufCount - 2 - i];
                double d = c1 - c2;
                fractalPath += Math.Sqrt(Math.FusedMultiplyAdd(d, d, 1.0));
            }

            // Raw PFE = sign * (straight / fractal) * 100
            double rawPfe;
            if (fractalPath > 1e-10)
            {
                double efficiency = straightLine / fractalPath * 100.0;
                rawPfe = priceDiff >= 0.0 ? efficiency : -efficiency;
            }
            else
            {
                rawPfe = 0.0;
            }

            s.LastRawPfe = rawPfe;

            // EMA smoothing with bias compensation
            if (s.Count <= _period + 1)
            {
                // First valid rawPfe: seed EMA
                s.Ema = rawPfe;
                s.E = _decay;
                result = rawPfe;
            }
            else
            {
                s.Ema = Math.FusedMultiplyAdd(s.Ema, _decay, _alpha * rawPfe);
                if (!s.IsCompensated)
                {
                    s.E *= _decay;
                    double c = 1.0 / (1.0 - s.E);
                    result = c * s.Ema;
                }
                else
                {
                    result = s.Ema;
                }
            }
        }
        else
        {
            result = 0.0;
        }

        _s = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _smoothPeriod);
        source.Times.CopyTo(tSpan);

        // Prime internal state by replaying last WarmupPeriod bars
        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _closeBuffer.Clear();
        _s = default;
        _ps = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod + _smoothPeriod * 3);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue
        _s.LastValidValue = 0;
        _s.E = 1.0;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _s.LastValidValue = source[i];
                break;
            }
        }

        if (_s.LastValidValue == 0)
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _s.LastValidValue = source[i];
                    break;
                }
            }
        }

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]), isNew: true);
        }

        _ps = _s;
    }

    /// <summary>
    /// Calculates PFE for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10, int smoothPeriod = 5)
    {
        var pfe = new Pfe(period, smoothPeriod);
        return pfe.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation for close price arrays.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Close prices.</param>
    /// <param name="output">Output PFE values.</param>
    /// <param name="period">Fractal path lookback period.</param>
    /// <param name="smoothPeriod">EMA smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10, int smoothPeriod = 5)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }
        if (smoothPeriod < 1)
        {
            throw new ArgumentException("Smooth period must be greater than or equal to 1", nameof(smoothPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period, smoothPeriod);
    }

    /// <summary>
    /// Calculates PFE and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Pfe Indicator) Calculate(TSeries source, int period = 10, int smoothPeriod = 5)
    {
        var indicator = new Pfe(period, smoothPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, int smoothPeriod)
    {
        int len = source.Length;
        int closeBufSize = period + 1;
        double periodSquared = (double)period * period;
        double alpha = 2.0 / (smoothPeriod + 1);
        double decay = 1.0 - alpha;

        const int StackAllocThreshold = 256;

        // Close buffer (period+1)
        double[]? rentedClose = closeBufSize > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(closeBufSize) : null;
        Span<double> closeBuf = rentedClose != null
            ? rentedClose.AsSpan(0, closeBufSize)
            : stackalloc double[closeBufSize];

        try
        {
            double lastValid = 0;
            int closeIdx = 0;
            int closeFilled = 0;
            double ema = 0;
            double e = 1.0;
            bool emaSeeded = false;

            // Find first valid value to seed lastValid
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                // Update close buffer
                closeBuf[closeIdx] = val;
                if (closeFilled < closeBufSize)
                {
                    closeFilled++;
                }
                closeIdx++;
                if (closeIdx >= closeBufSize)
                {
                    closeIdx = 0;
                }

                // Calculate PFE
                if (closeFilled >= closeBufSize)
                {
                    // Newest is at closeIdx-1, oldest is at closeIdx (both mod closeBufSize)
                    int newestIdx = (closeIdx - 1 + closeBufSize) % closeBufSize;
                    int oldestIdx = closeIdx % closeBufSize;

                    double currentClose = closeBuf[newestIdx];
                    double laggedClose = closeBuf[oldestIdx];
                    double priceDiff = currentClose - laggedClose;
                    double straightLine = Math.Sqrt(Math.FusedMultiplyAdd(priceDiff, priceDiff, periodSquared));

                    // Fractal path: sum of bar-to-bar Euclidean distances
                    double fractalPath = 0.0;
                    for (int j = 0; j < period; j++)
                    {
                        int c1Idx = (newestIdx - j + closeBufSize) % closeBufSize;
                        int c2Idx = (newestIdx - j - 1 + closeBufSize) % closeBufSize;
                        double d = closeBuf[c1Idx] - closeBuf[c2Idx];
                        fractalPath += Math.Sqrt(Math.FusedMultiplyAdd(d, d, 1.0));
                    }

                    double rawPfe;
                    if (fractalPath > 1e-10)
                    {
                        double efficiency = straightLine / fractalPath * 100.0;
                        rawPfe = priceDiff >= 0.0 ? efficiency : -efficiency;
                    }
                    else
                    {
                        rawPfe = 0.0;
                    }

                    // EMA smoothing with bias compensation
                    if (!emaSeeded)
                    {
                        ema = rawPfe;
                        e = decay;
                        emaSeeded = true;
                        output[i] = rawPfe;
                    }
                    else
                    {
                        ema = Math.FusedMultiplyAdd(ema, decay, alpha * rawPfe);
                        if (e > 1e-10)
                        {
                            e *= decay;
                            double c = 1.0 / (1.0 - e);
                            output[i] = c * ema;
                        }
                        else
                        {
                            output[i] = ema;
                        }
                    }
                }
                else
                {
                    output[i] = 0.0;
                }
            }
        }
        finally
        {
            if (rentedClose != null)
            {
                ArrayPool<double>.Shared.Return(rentedClose);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _closeBuffer.Clear();
        _s = new State(0, 1.0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }
}
