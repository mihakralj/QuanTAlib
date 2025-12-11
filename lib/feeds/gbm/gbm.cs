using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace QuanTAlib;

/// <summary>
/// Geometric Brownian Motion (GBM) generator for simulating OHLCV data.
/// Generates realistic price data for testing indicators and strategies.
/// Stateless design - only maintains minimal state needed for price continuity.
/// </summary>
[SkipLocalsInit]
#pragma warning disable S101 // Rename class 'GBM' to match pascal case naming rules
#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class GBM : IFeed
#pragma warning restore S101
{
    private readonly Random? _rnd;

    private double _lastPrice;
    private long _lastTime;

    private readonly double _mu;
    private readonly double _sigma;

    // Precomputed GBM constants
    private readonly double _drift;
    private readonly double _vol;
    private readonly long _defaultTimeStep;

    // State for streaming bar formation (only when isNew=false)
    private TBar _currentBar;
    private bool _hasCurrentBar;

    // Box-Muller optimization: cache second normal
    private double _cachedZ;
    private bool _hasCachedZ;

    /// <summary>
    /// Creates a new GBM generator.
    /// </summary>
    /// <param name="startPrice">Initial price (default: 100.0, must be positive)</param>
    /// <param name="mu">Annual drift/return rate (default: 0.05 = 5%)</param>
    /// <param name="sigma">Annual volatility (default: 0.2 = 20%, must be non-negative)</param>
    /// <param name="defaultTimeframe">Default timeframe for bars (default: 1 minute)</param>
    /// <param name="seed">Optional random seed for reproducibility (default: null for non-deterministic)</param>
    public GBM(
        double startPrice = 100.0,
        double mu = 0.05,
        double sigma = 0.2,
        TimeSpan? defaultTimeframe = null,
        int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(sigma);

        _rnd = seed.HasValue ? new Random(seed.Value) : null;
        _lastPrice = startPrice;
        _lastTime = DateTime.UtcNow.Ticks;

        _mu = mu;
        _sigma = sigma;

        // Use provided timeframe or default to 1 minute
        var timeframe = defaultTimeframe ?? TimeSpan.FromMinutes(1);
        _defaultTimeStep = timeframe.Ticks;

        // Calculate dt based on timeframe (assuming 252 trading days/year, 6.5 hours/day)
        double minutesPerYear = 252.0 * 6.5 * 60.0;
        double dt = timeframe.TotalMinutes / minutesPerYear;

        _drift = (mu - 0.5 * sigma * sigma) * dt;
        _vol = sigma * Math.Sqrt(dt);
    }

    /// <summary>
    /// Generates a random double in [0, 1) using either the seeded Random or RandomNumberGenerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NextDouble()
    {
        if (_rnd != null)
        {
            return _rnd.NextDouble();
        }

        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        ulong ul = BitConverter.ToUInt64(buffer);
        return (ul >> 11) * (1.0 / (1ul << 53));
    }

    /// <summary>
    /// Generates next standard normal using Box-Muller transform with caching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NextNormal()
    {
        if (_hasCachedZ)
        {
            _hasCachedZ = false;
            return _cachedZ;
        }

        double u1 = 1.0 - NextDouble();
        double u2 = 1.0 - NextDouble();
        double mag = Math.Sqrt(-2.0 * Math.Log(u1));
        double angle = 2.0 * Math.PI * u2;

        _cachedZ = mag * Math.Sin(angle);
        _hasCachedZ = true;

        return mag * Math.Cos(angle);
    }

    /// <summary>
    /// Gets the next bar with full bidirectional control.
    /// GBM always honors the request - isNew parameter unchanged on return.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar Next(ref bool isNew)
    {
        // GBM always honors request - parameter unchanged

        if (isNew || !_hasCurrentBar)
        {
            // Generate new bar
            long currentTime = _lastTime + _defaultTimeStep;

            double z = NextNormal();
            double price = _lastPrice * Math.Exp(_drift + _vol * z);
            double volume = 1000 + NextDouble() * 1000;

            double open = _lastPrice;
            double close = price;
            double high = Math.Max(open, close) * (1.0 + NextDouble() * 0.01);
            double low = Math.Min(open, close) * (1.0 - NextDouble() * 0.01);

            _currentBar = new TBar(currentTime, open, high, low, close, volume);
            _hasCurrentBar = true;

            _lastPrice = close;
            _lastTime = currentTime;
        }
        else
        {
            // Update current bar (intra-bar tick)
            double z = NextNormal();
            double price = _lastPrice * Math.Exp(_drift + _vol * z);
            double additionalVolume = 1000 + NextDouble() * 1000;

            var bar = _currentBar;
            double newClose = price;
            double newHigh = Math.Max(bar.High, newClose);
            double newLow = Math.Min(bar.Low, newClose);
            double newVolume = bar.Volume + additionalVolume;

            _currentBar = new TBar(bar.Time, bar.Open, newHigh, newLow, newClose, newVolume);
            _lastPrice = newClose;
        }

        return _currentBar;
    }

    /// <summary>
    /// Gets the next bar with simple control.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar Next(bool isNew = true)
    {
        // Delegate to ref version
        return Next(ref isNew);
    }

    /// <summary>
    /// Generates a batch of bars using optimized batch processing with explicit time parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));

        var series = new TBarSeries(count);

        // Pre-allocate arrays for SoA layout
        long[] t = new long[count];
        double[] o = new double[count];
        double[] h = new double[count];
        double[] l = new double[count];
        double[] c = new double[count];
        double[] v = new double[count];

        // Calculate dt for this specific interval
        double minutesPerYear = 252.0 * 6.5 * 60.0;
        double dt = interval.TotalMinutes / minutesPerYear;
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double vol = _sigma * Math.Sqrt(dt);

        long timeStep = interval.Ticks;
        double currentPrice = _lastPrice;
        long currentTime = startTime;

        for (int i = 0; i < count; i++)
        {
            double z = NextNormal();
            double price = currentPrice * Math.Exp(drift + vol * z);

            double open = currentPrice;
            double close = price;

            double rnd1 = NextDouble();
            double rnd2 = NextDouble();
            double rnd3 = NextDouble();

            t[i] = currentTime;
            o[i] = open;
            c[i] = close;
            h[i] = Math.Max(open, close) * (1.0 + rnd1 * 0.01);
            l[i] = Math.Min(open, close) * (1.0 - rnd2 * 0.01);
            v[i] = 1000 + rnd3 * 1000;

            currentPrice = price;
            currentTime += timeStep;
        }

        // Update internal state to continue from end of batch
        _lastPrice = currentPrice;
        _lastTime = currentTime - timeStep; // Last bar time, not next bar time

        // Bulk add to series
        series.Add(t, o, h, l, c, v);

        // Reset streaming state after batch
        _hasCurrentBar = false;

        return series;
    }
}
