// Volatility of Volatility (VOV) Indicator
// Measures the stability of volatility by calculating the standard deviation of volatility

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VOV: Volatility of Volatility
/// A second-order volatility indicator that measures how stable or unstable
/// the volatility itself is, by calculating the standard deviation of a volatility series.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate initial volatility: StdDev(price, volatilityPeriod)</item>
/// <item>Calculate VOV: StdDev(volatility, vovPeriod)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>High VOV indicates unstable/changing volatility regime</item>
/// <item>Low VOV indicates stable/consistent volatility</item>
/// <item>Useful for volatility regime detection and risk management</item>
/// <item>Can signal transitions between calm and turbulent markets</item>
/// </list>
///
/// <b>Interpretation:</b>
/// <list type="bullet">
/// <item>Rising VOV may precede major market moves</item>
/// <item>Falling VOV suggests volatility is stabilizing</item>
/// <item>Extreme VOV values can indicate regime changes</item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Vov : AbstractBase
{
    private readonly int _volatilityPeriod;
    private readonly int _vovPeriod;
    private readonly RingBuffer _priceBuffer;
    private readonly RingBuffer _volatilityBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PriceSum,
        double PriceSumSq,
        double VolSum,
        double VolSumSq,
        double LastValidPrice,
        double LastVov,
        int PriceCount,
        int VolCount
    );
    private State _s;
    private State _ps;

    // Backup buffers for state rollback
    private readonly double[] _priceBackup;
    private readonly double[] _volatilityBackup;

    /// <summary>
    /// Initializes a new instance of the Vov class.
    /// </summary>
    /// <param name="volatilityPeriod">The lookback period for initial volatility calculation (default 20).</param>
    /// <param name="vovPeriod">The lookback period for VOV calculation (default 10).</param>
    /// <exception cref="ArgumentException">Thrown when any period is less than 1.</exception>
    public Vov(int volatilityPeriod = 20, int vovPeriod = 10)
    {
        if (volatilityPeriod <= 0)
        {
            throw new ArgumentException("Volatility period must be greater than 0", nameof(volatilityPeriod));
        }
        if (vovPeriod <= 0)
        {
            throw new ArgumentException("VOV period must be greater than 0", nameof(vovPeriod));
        }
        _volatilityPeriod = volatilityPeriod;
        _vovPeriod = vovPeriod;
        WarmupPeriod = volatilityPeriod + vovPeriod - 1;
        Name = $"Vov({volatilityPeriod},{vovPeriod})";
        _priceBuffer = new RingBuffer(volatilityPeriod);
        _volatilityBuffer = new RingBuffer(vovPeriod);
        _priceBackup = new double[volatilityPeriod];
        _volatilityBackup = new double[vovPeriod];
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Vov class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="volatilityPeriod">The volatility period (default 20).</param>
    /// <param name="vovPeriod">The VOV period (default 10).</param>
    public Vov(ITValuePublisher source, int volatilityPeriod = 20, int vovPeriod = 10) : this(volatilityPeriod, vovPeriod)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.PriceCount >= _volatilityPeriod && _s.VolCount >= _vovPeriod;

    /// <summary>
    /// The volatility lookback period.
    /// </summary>
    public int VolatilityPeriod => _volatilityPeriod;

    /// <summary>
    /// The VOV lookback period.
    /// </summary>
    public int VovPeriod => _vovPeriod;

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (uses close price).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated VOV value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.Close, isNew);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _volatilityPeriod, _vovPeriod);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double price, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            // Backup buffers
            _priceBuffer.CopyTo(_priceBackup);
            _volatilityBuffer.CopyTo(_volatilityBackup);
        }
        else
        {
            _s = _ps;
            // Restore buffers
            _priceBuffer.Clear();
            for (int i = 0; i < _priceBackup.Length && i < _ps.PriceCount; i++)
            {
                _priceBuffer.Add(_priceBackup[i]);
            }
            _volatilityBuffer.Clear();
            for (int i = 0; i < _volatilityBackup.Length && i < _ps.VolCount; i++)
            {
                _volatilityBuffer.Add(_volatilityBackup[i]);
            }
        }

        var s = _s;

        // Handle non-finite values
        if (!double.IsFinite(price))
        {
            price = s.LastValidPrice;
        }
        else
        {
            s.LastValidPrice = price;
        }

        // Update price running sums (remove oldest if buffer is full)
        double priceSum = s.PriceSum;
        double priceSumSq = s.PriceSumSq;
        if (_priceBuffer.Count >= _volatilityPeriod)
        {
            double oldest = _priceBuffer[0];
            priceSum -= oldest;
            priceSumSq -= oldest * oldest;
        }
        priceSum += price;
        priceSumSq += price * price;

        _priceBuffer.Add(price);

        int priceCount = Math.Min(_priceBuffer.Count, _volatilityPeriod);

        // Calculate initial volatility (population standard deviation)
        double volatility = 0;
        if (priceCount > 1)
        {
            double mean = priceSum / priceCount;
            double variance = (priceSumSq / priceCount) - (mean * mean);
            volatility = Math.Sqrt(Math.Max(0.0, variance));
        }

        // Update volatility running sums (remove oldest if buffer is full)
        double volSum = s.VolSum;
        double volSumSq = s.VolSumSq;
        if (_volatilityBuffer.Count >= _vovPeriod)
        {
            double oldestVol = _volatilityBuffer[0];
            volSum -= oldestVol;
            volSumSq -= oldestVol * oldestVol;
        }
        volSum += volatility;
        volSumSq += volatility * volatility;

        _volatilityBuffer.Add(volatility);

        int volCount = Math.Min(_volatilityBuffer.Count, _vovPeriod);

        // Calculate VOV (population standard deviation of volatility)
        double vov = 0;
        if (volCount > 1)
        {
            double volMean = volSum / volCount;
            double volVariance = (volSumSq / volCount) - (volMean * volMean);
            vov = Math.Sqrt(Math.Max(0.0, volVariance));
        }

        if (!double.IsFinite(vov) || vov < 0)
        {
            vov = s.LastVov;
        }
        else
        {
            s.LastVov = vov;
        }

        // Update state
        s.PriceSum = priceSum;
        s.PriceSumSq = priceSumSq;
        s.VolSum = volSum;
        s.VolSumSq = volSumSq;
        if (isNew)
        {
            s.PriceCount = Math.Min(s.PriceCount + 1, _volatilityPeriod);
            // Only start counting vol after we have enough prices for valid volatility
            if (s.PriceCount >= _volatilityPeriod)
            {
                s.VolCount = Math.Min(s.VolCount + 1, _vovPeriod);
            }
        }

        _s = s;

        Last = new TValue(timeTicks, vov);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _priceBuffer.Clear();
        _volatilityBuffer.Clear();
        Array.Clear(_priceBackup);
        Array.Clear(_volatilityBackup);
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates VOV for a series (static).
    /// </summary>
    /// <param name="source">The source series.</param>
    /// <param name="volatilityPeriod">The volatility period.</param>
    /// <param name="vovPeriod">The VOV period.</param>
    /// <returns>A TSeries containing the VOV values.</returns>
    public static TSeries Batch(TSeries source, int volatilityPeriod = 20, int vovPeriod = 10)
    {
        var vov = new Vov(volatilityPeriod, vovPeriod);
        return vov.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    /// <param name="source">Price values.</param>
    /// <param name="output">Output VOV values.</param>
    /// <param name="volatilityPeriod">The volatility period.</param>
    /// <param name="vovPeriod">The VOV period.</param>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> output,
        int volatilityPeriod = 20,
        int vovPeriod = 10)
    {
        if (volatilityPeriod <= 0)
        {
            throw new ArgumentException("Volatility period must be greater than 0", nameof(volatilityPeriod));
        }
        if (vovPeriod <= 0)
        {
            throw new ArgumentException("VOV period must be greater than 0", nameof(vovPeriod));
        }
        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output span must be at least as long as source span", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        // Use ArrayPool for larger allocations
        double[]? priceRented = null;
        double[]? volRented = null;

        if (volatilityPeriod > StackallocThreshold)
        {
            priceRented = ArrayPool<double>.Shared.Rent(volatilityPeriod);
        }
        if (vovPeriod > StackallocThreshold)
        {
            volRented = ArrayPool<double>.Shared.Rent(vovPeriod);
        }

        try
        {
            scoped Span<double> priceBuffer = volatilityPeriod <= StackallocThreshold
                ? stackalloc double[volatilityPeriod]
                : priceRented.AsSpan(0, volatilityPeriod);

            scoped Span<double> volBuffer = vovPeriod <= StackallocThreshold
                ? stackalloc double[vovPeriod]
                : volRented.AsSpan(0, vovPeriod);

            priceBuffer.Clear();
            volBuffer.Clear();

            double lastValidPrice = 0;
            double priceSum = 0, priceSumSq = 0;
            double volSum = 0, volSumSq = 0;
            int priceIdx = 0, volIdx = 0;
            int priceCount = 0, volCount = 0;

            for (int i = 0; i < len; i++)
            {
                double price = source[i];

                // Handle non-finite values
                if (!double.IsFinite(price))
                {
                    price = lastValidPrice;
                }
                else
                {
                    lastValidPrice = price;
                }

                // Update price running sums
                if (priceCount >= volatilityPeriod)
                {
                    priceSum -= priceBuffer[priceIdx];
                    priceSumSq -= priceBuffer[priceIdx] * priceBuffer[priceIdx];
                }
                priceSum += price;
                priceSumSq += price * price;
                priceBuffer[priceIdx] = price;
                priceIdx = (priceIdx + 1) % volatilityPeriod;
                if (priceCount < volatilityPeriod)
                {
                    priceCount++;
                }

                // Calculate volatility
                double volatility = 0;
                if (priceCount > 1)
                {
                    double mean = priceSum / priceCount;
                    double variance = (priceSumSq / priceCount) - (mean * mean);
                    volatility = Math.Sqrt(Math.Max(0.0, variance));
                }

                // Update volatility running sums
                if (volCount >= vovPeriod)
                {
                    volSum -= volBuffer[volIdx];
                    volSumSq -= volBuffer[volIdx] * volBuffer[volIdx];
                }
                volSum += volatility;
                volSumSq += volatility * volatility;
                volBuffer[volIdx] = volatility;
                volIdx = (volIdx + 1) % vovPeriod;
                if (volCount < vovPeriod)
                {
                    volCount++;
                }

                // Calculate VOV
                double vov = 0;
                if (volCount > 1)
                {
                    double volMean = volSum / volCount;
                    double volVariance = (volSumSq / volCount) - (volMean * volMean);
                    vov = Math.Sqrt(Math.Max(0.0, volVariance));
                }

                if (!double.IsFinite(vov) || vov < 0)
                {
                    vov = i > 0 ? output[i - 1] : 0;
                }

                output[i] = vov;
            }
        }
        finally
        {
            if (priceRented != null)
            {
                ArrayPool<double>.Shared.Return(priceRented);
            }
            if (volRented != null)
            {
                ArrayPool<double>.Shared.Return(volRented);
            }
        }
    }

    public static (TSeries Results, Vov Indicator) Calculate(TSeries source, int volatilityPeriod = 20, int vovPeriod = 10)
    {
        var indicator = new Vov(volatilityPeriod, vovPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

}
