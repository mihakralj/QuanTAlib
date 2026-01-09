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
public sealed class GBM : IFeed
#pragma warning restore S101
{
    private readonly Random? _rnd;

    private double _lastPrice;
    private long _lastTime;

    private readonly double _drift;
    private readonly double _vol;
    private readonly long _defaultTimeStep;

    private TBar _currentBar;
    private bool _hasCurrentBar;

    private double _cachedZ;
    private bool _hasCachedZ;

    /// <summary>
    /// Gets the annual drift/return rate.
    /// </summary>
    public double Mu { get; }

    /// <summary>
    /// Gets the annual volatility.
    /// </summary>
    public double Sigma { get; }

    /// <summary>
    /// Gets the starting price.
    /// </summary>
    public double StartPrice { get; }

    /// <summary>
    /// Gets the current price state.
    /// </summary>
    public double CurrentPrice => _lastPrice;

    /// <summary>
    /// Gets whether the generator has a current bar in progress.
    /// </summary>
    public bool HasCurrentBar => _hasCurrentBar;

    /// <summary>
    /// Creates a new GBM generator.
    /// </summary>
    /// <param name="startPrice">Initial price (default: 100.0, must be positive and finite)</param>
    /// <param name="mu">Annual drift/return rate (default: 0.05 = 5%, must be finite)</param>
    /// <param name="sigma">Annual volatility (default: 0.2 = 20%, must be non-negative and finite)</param>
    /// <param name="defaultTimeframe">Default timeframe for bars (default: 1 minute, must be positive)</param>
    /// <param name="seed">Optional random seed for reproducibility (default: null for non-deterministic)</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when startPrice is not positive/finite, sigma is negative/non-finite,
    /// mu is non-finite, or defaultTimeframe is non-positive.
    /// </exception>
    public GBM(
        double startPrice = 100.0,
        double mu = 0.05,
        double sigma = 0.2,
        TimeSpan? defaultTimeframe = null,
        int? seed = null)
    {
        // Validate startPrice
        if (startPrice <= 0 || !double.IsFinite(startPrice))
            throw new ArgumentOutOfRangeException(nameof(startPrice), startPrice, "Start price must be positive and finite");

        // Validate mu
        if (!double.IsFinite(mu))
            throw new ArgumentOutOfRangeException(nameof(mu), mu, "Drift (mu) must be finite");

        // Validate sigma
        if (sigma < 0 || !double.IsFinite(sigma))
            throw new ArgumentOutOfRangeException(nameof(sigma), sigma, "Volatility (sigma) must be non-negative and finite");

        // Use provided timeframe or default to 1 minute
        var timeframe = defaultTimeframe ?? TimeSpan.FromMinutes(1);

        // Validate timeframe
        if (timeframe <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(defaultTimeframe), defaultTimeframe, "Timeframe must be positive");

        _rnd = seed.HasValue ? new Random(seed.Value) : null;
        StartPrice = startPrice;
        _lastPrice = startPrice;
        _lastTime = DateTime.UtcNow.Ticks;

        Mu = mu;
        Sigma = sigma;

        _defaultTimeStep = timeframe.Ticks;

        const double minutesPerYear = 252.0 * 6.5 * 60.0;
        double dt = timeframe.TotalMinutes / minutesPerYear;

        _drift = (mu - 0.5 * sigma * sigma) * dt;
        _vol = sigma * Math.Sqrt(dt);
    }

    /// <summary>
    /// Resets the generator to its initial state.
    /// </summary>
    public void Reset()
    {
        _lastPrice = StartPrice;
        _lastTime = DateTime.UtcNow.Ticks;
        _currentBar = default;
        _hasCurrentBar = false;
        _cachedZ = 0;
        _hasCachedZ = false;
    }

    /// <summary>
    /// Resets the generator to its initial state with a specific start time.
    /// </summary>
    /// <param name="startTime">The start time in ticks.</param>
    public void Reset(long startTime)
    {
        _lastPrice = StartPrice;
        _lastTime = startTime;
        _currentBar = default;
        _hasCurrentBar = false;
        _cachedZ = 0;
        _hasCachedZ = false;
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

        // Guard against log(0) which produces -Infinity
        if (u1 <= double.Epsilon)
            u1 = double.Epsilon;

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

            // Ensure price stays positive and finite
            if (!double.IsFinite(price) || price <= 0)
                price = _lastPrice;

            double volume = 1000 + NextDouble() * 1000;

            double open = _lastPrice;
            double close = price;

            double rnd1 = NextDouble();
            double rnd2 = NextDouble();

            double high = Math.Max(open, close) * (1.0 + rnd1 * 0.01);
            double low = Math.Min(open, close) * (1.0 - rnd2 * 0.01);

            // Ensure valid OHLC constraints
            high = Math.Max(high, Math.Max(open, close));
            low = Math.Min(low, Math.Min(open, close));
            low = Math.Max(double.Epsilon, low); // Ensure positive

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

            // Ensure price stays positive and finite
            if (!double.IsFinite(price) || price <= 0)
                price = _lastPrice;

            double additionalVolume = 1000 + NextDouble() * 1000;

            var bar = _currentBar;
            double newClose = price;
            double newHigh = Math.Max(bar.High, newClose);
            double newLow = Math.Min(bar.Low, newClose);
            newLow = Math.Max(double.Epsilon, newLow); // Ensure positive
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
    /// <param name="count">Number of bars to generate (must be positive)</param>
    /// <param name="startTime">Starting timestamp in ticks</param>
    /// <param name="interval">Time interval between bars (must be positive)</param>
    /// <returns>A TBarSeries containing the generated bars</returns>
    /// <exception cref="ArgumentException">Thrown when count is not positive</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when interval is not positive</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be positive");

        var series = new TBarSeries(count);

        // Pre-allocate arrays for SoA layout
        long[] t = new long[count];
        double[] o = new double[count];
        double[] h = new double[count];
        double[] l = new double[count];
        double[] c = new double[count];
        double[] v = new double[count];

        const double minutesPerYear = 252.0 * 6.5 * 60.0;
        double dt = interval.TotalMinutes / minutesPerYear;
        double drift = (Mu - 0.5 * Sigma * Sigma) * dt;
        double vol = Sigma * Math.Sqrt(dt);

        long timeStep = interval.Ticks;
        double currentPrice = _lastPrice;
        long currentTime = startTime;

        for (int i = 0; i < count; i++)
        {
            double z = NextNormal();
            double price = currentPrice * Math.Exp(drift + vol * z);

            // Ensure price stays positive and finite
            if (!double.IsFinite(price) || price <= 0)
                price = currentPrice;

            double open = currentPrice;
            double close = price;

            double rnd1 = NextDouble();
            double rnd2 = NextDouble();
            double rnd3 = NextDouble();

            t[i] = currentTime;
            o[i] = open;
            c[i] = close;

            double high = Math.Max(open, close) * (1.0 + rnd1 * 0.01);
            double low = Math.Min(open, close) * (1.0 - rnd2 * 0.01);

            // Ensure valid OHLC constraints
            high = Math.Max(high, Math.Max(open, close));
            low = Math.Min(low, Math.Min(open, close));
            low = Math.Max(double.Epsilon, low); // Ensure positive

            h[i] = high;
            l[i] = low;
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
#pragma warning restore S2245
