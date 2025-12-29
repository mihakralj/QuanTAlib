using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

/// <summary>
/// Provides standardized test data for validation tests.
/// Uses GBM (Geometric Brownian Motion) to generate realistic price data
/// and converts it to formats required by external validation libraries.
/// </summary>
public sealed class ValidationTestData : IDisposable
{
    /// <summary>
    /// Default number of bars for validation tests.
    /// 5000 bars ensures sufficient convergence for most indicators.
    /// </summary>
    public const int DefaultCount = 5000;

    /// <summary>
    /// Default starting price for generated data.
    /// </summary>
    public const double DefaultStartPrice = 1000.0;

    /// <summary>
    /// Default annual drift for GBM (5%).
    /// </summary>
    public const double DefaultMu = 0.05;

    /// <summary>
    /// Default annual volatility for GBM (200%).
    /// High volatility ensures diverse price scenarios.
    /// </summary>
    public const double DefaultSigma = 2.0;

    /// <summary>
    /// Default random seed for reproducibility.
    /// </summary>
    public const int DefaultSeed = 123;

    /// <summary>
    /// Gets the generated bar series.
    /// </summary>
    public TBarSeries Bars { get; }

    /// <summary>
    /// Gets the close price series.
    /// </summary>
    public TSeries Data { get; }

    /// <summary>
    /// Gets the quotes in Skender.Stock.Indicators format.
    /// </summary>
    public IReadOnlyList<Quote> SkenderQuotes { get; }

    /// <summary>
    /// Gets the raw close price data as a ReadOnlyMemory for span-based APIs.
    /// </summary>
    public ReadOnlyMemory<double> RawData { get; }

    /// <summary>
    /// Gets the raw open prices as read-only memory.
    /// </summary>
    public ReadOnlyMemory<double> OpenPrices { get; }

    /// <summary>
    /// Gets the raw high prices as read-only memory.
    /// </summary>
    public ReadOnlyMemory<double> HighPrices { get; }

    /// <summary>
    /// Gets the raw low prices as read-only memory.
    /// </summary>
    public ReadOnlyMemory<double> LowPrices { get; }

    /// <summary>
    /// Gets the raw close prices as read-only memory.
    /// </summary>
    public ReadOnlyMemory<double> ClosePrices { get; }

    /// <summary>
    /// Gets the raw volume data as read-only memory.
    /// </summary>
    public ReadOnlyMemory<double> VolumeData { get; }

    /// <summary>
    /// Gets the timestamps as read-only memory.
    /// </summary>
    public ReadOnlyMemory<long> Timestamps { get; }

    /// <summary>
    /// Gets the number of bars in the dataset.
    /// </summary>
    public int Count => Bars.Count;

    /// <summary>
    /// Creates validation test data with default parameters.
    /// </summary>
    public ValidationTestData()
        : this(DefaultCount, DefaultStartPrice, DefaultMu, DefaultSigma, DefaultSeed)
    {
    }

    /// <summary>
    /// Creates validation test data with specified parameters.
    /// </summary>
    /// <param name="count">Number of bars to generate</param>
    /// <param name="startPrice">Starting price</param>
    /// <param name="mu">Annual drift rate</param>
    /// <param name="sigma">Annual volatility</param>
    /// <param name="seed">Random seed for reproducibility</param>
    public ValidationTestData(
        int count,
        double startPrice = DefaultStartPrice,
        double mu = DefaultMu,
        double sigma = DefaultSigma,
        int seed = DefaultSeed)
    {
        var gbm = new GBM(startPrice, mu, sigma, seed: seed);
        Bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        Data = Bars.Close;

        // Extract raw arrays efficiently (avoid LINQ in hot path)
        int barCount = Bars.Count;
        var openPrices = new double[barCount];
        var highPrices = new double[barCount];
        var lowPrices = new double[barCount];
        var closePrices = new double[barCount];
        var volumeData = new double[barCount];
        var timestamps = new long[barCount];

        // Use span-based access for efficiency
        var openSpan = Bars.OpenValues;
        var highSpan = Bars.HighValues;
        var lowSpan = Bars.LowValues;
        var closeSpan = Bars.CloseValues;
        var volumeSpan = Bars.VolumeValues;
        var timeSpan = Bars.Times;

        openSpan.CopyTo(openPrices);
        highSpan.CopyTo(highPrices);
        lowSpan.CopyTo(lowPrices);
        closeSpan.CopyTo(closePrices);
        volumeSpan.CopyTo(volumeData);
        timeSpan.CopyTo(timestamps);

        // Expose as ReadOnlyMemory to prevent external modification
        OpenPrices = openPrices;
        HighPrices = highPrices;
        LowPrices = lowPrices;
        ClosePrices = closePrices;
        VolumeData = volumeData;
        Timestamps = timestamps;
        RawData = closePrices;

        // Build Skender quotes without LINQ
        var quotes = new Quote[barCount];
        for (int i = 0; i < barCount; i++)
        {
            quotes[i] = new Quote
            {
                Date = new DateTime(timestamps[i], DateTimeKind.Utc),
                Open = (decimal)openPrices[i],
                High = (decimal)highPrices[i],
                Low = (decimal)lowPrices[i],
                Close = (decimal)closePrices[i],
                Volume = (decimal)volumeData[i],
            };
        }
        SkenderQuotes = quotes;
    }

    /// <summary>
    /// Creates a subset of the data for smaller tests.
    /// </summary>
    /// <param name="count">Number of bars to include</param>
    /// <returns>A new ValidationTestData instance with the subset</returns>
    public ValidationTestData CreateSubset(int count)
    {
        if (count <= 0 || count > Count)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Count must be between 1 and {Count}");

        return new ValidationTestData(count, DefaultStartPrice, DefaultMu, DefaultSigma, DefaultSeed);
    }

    /// <summary>
    /// Gets the close price span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> GetCloseSpan() => ClosePrices.Span;

    /// <summary>
    /// Gets the high price span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> GetHighSpan() => HighPrices.Span;

    /// <summary>
    /// Gets the low price span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> GetLowSpan() => LowPrices.Span;

    /// <summary>
    /// Gets the open price span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> GetOpenSpan() => OpenPrices.Span;

    /// <summary>
    /// Gets the volume span for SIMD operations.
    /// </summary>
    public ReadOnlySpan<double> GetVolumeSpan() => VolumeData.Span;

    /// <summary>
    /// Disposes of resources (no-op, but implements pattern for test fixtures).
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose
        // Implemented for IDisposable pattern compatibility with test fixtures
    }
}