using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBANDS: Bollinger Bands
/// A volatility-based channel indicator consisting of a moving average middle band
/// with upper and lower bands positioned at a specified number of standard deviations.
/// Developed by John Bollinger in the 1980s for volatility analysis.
/// </summary>
/// <remarks>
/// The BBands calculation process:
/// 1. Calculate SMA of price over the period
/// 2. Calculate standard deviation over the period
/// 3. Upper band = SMA + (multiplier × StdDev)
/// 4. Lower band = SMA - (multiplier × StdDev)
///
/// Key characteristics:
/// - Adapts dynamically to volatility changes
/// - Wider bands indicate higher volatility
/// - Narrower bands indicate lower volatility
/// - Price tends to oscillate between bands
/// - Can identify overbought/oversold conditions
///
/// Sources:
///     John Bollinger - "Bollinger on Bollinger Bands" (2001)
///     https://www.bollingerbands.com/
/// </remarks>
[SkipLocalsInit]
public sealed class Bbands : AbstractBase
{
    private readonly Sma _sma;
    private readonly StdDev _stdev;
    private readonly int _period;
    private readonly double _multiplier;
    private const int DefaultPeriod = 20;
    private const double DefaultMultiplier = 2.0;
    private const double MinMultiplier = 0.1;
    private const int MinPeriod = 2;

    public override bool IsHot => _index >= WarmupPeriod;
    private int _index;

    /// <summary>
    /// Middle band (SMA of price)
    /// </summary>
    public TValue Middle { get; private set; }

    /// <summary>
    /// Upper band (SMA + multiplier × StdDev)
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Lower band (SMA - multiplier × StdDev)
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// Band width (Upper - Lower)
    /// </summary>
    public TValue Width { get; private set; }

    /// <summary>
    /// Percent B: (Price - Lower) / (Upper - Lower)
    /// </summary>
    public TValue PercentB { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bbands(int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        _period = period;
        _multiplier = multiplier;
        _sma = new Sma(period);
        _stdev = new StdDev(period, isPopulation: true);
        WarmupPeriod = period;
        Name = $"Bbands({period},{multiplier:F1})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bbands(ITValuePublisher source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
        : this(period, multiplier)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _index = 0;
        Middle = new TValue(DateTime.UtcNow, 0);
        Upper = new TValue(DateTime.UtcNow, 0);
        Lower = new TValue(DateTime.UtcNow, 0);
        Width = new TValue(DateTime.UtcNow, 0);
        PercentB = new TValue(DateTime.UtcNow, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double GetFiniteValue(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _index++;
        }

        double finiteValue = GetFiniteValue(input.Value, Middle.Value);

        // Update SMA and StdDev
        TValue smaValue = _sma.Update(new TValue(input.Time, finiteValue), isNew);
        TValue stdevValue = _stdev.Update(new TValue(input.Time, finiteValue), isNew);

        double middle = smaValue.Value;
        double stdDev = stdevValue.Value;
        double offset = _multiplier * stdDev;

        double upper = middle + offset;
        double lower = middle - offset;
        double width = upper - lower;

        // Calculate Percent B
        double percentB = 0.0;
        if (width > double.Epsilon)
        {
            percentB = (finiteValue - lower) / width;
        }

        // Update all band values
        Middle = new TValue(input.Time, middle);
        Upper = new TValue(input.Time, upper);
        Lower = new TValue(input.Time, lower);
        Width = new TValue(input.Time, width);
        PercentB = new TValue(input.Time, percentB);
        Last = Middle;

        PubEvent(Middle, isNew);
        return Middle;
    }

    /// <summary>
    /// Updates the indicator with a new time series and returns the middle band series.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ReadOnlySpan<double> sourceSpan = source.Values;
        ReadOnlySpan<long> timeSpan = source.Times;
        int len = sourceSpan.Length;

        TSeries middleSeries = new(capacity: len);
        Span<double> middleSpan = stackalloc double[len];
        Span<double> upperSpan = stackalloc double[len];
        Span<double> lowerSpan = stackalloc double[len];

        Calculate(sourceSpan, middleSpan, upperSpan, lowerSpan, _period, _multiplier);

        for (int i = 0; i < len; i++)
        {
            middleSeries.Add(timeSpan[i], middleSpan[i], isNew: true);
        }

        // Restore state from the last period values
        Reset();
        int startIdx = Math.Max(0, len - _period);
        for (int i = startIdx; i < len; i++)
        {
            Update(new TValue(timeSpan[i], sourceSpan[i]), isNew: true);
        }

        return middleSeries;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override void Reset()
    {
        _sma.Reset();
        _stdev.Reset();
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;
        
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    /// <summary>
    /// Calculates Bollinger Bands for the entire series and returns the middle band series.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        Bbands bbands = new(period, multiplier);
        return bbands.Update(source);
    }

    /// <summary>
    /// Calculates Bollinger Bands across all input values using SIMD-optimized operations where possible.
    /// </summary>
    public static void Calculate(
        ReadOnlySpan<double> source,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period = DefaultPeriod,
        double multiplier = DefaultMultiplier)
    {
        if (source.Length != middle.Length || source.Length != upper.Length || source.Length != lower.Length)
        {
            throw new ArgumentException("All spans must have the same length.", nameof(source));
        }
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Calculate SMA using static batch method
        Sma.Batch(source, middle, period);

        // Calculate standard deviation and bands
        for (int i = 0; i < len; i++)
        {
            if (i < period - 1)
            {
                upper[i] = double.NaN;
                lower[i] = double.NaN;
                continue;
            }

            // Calculate standard deviation for the current window
            double sum = 0.0;
            double sumSq = 0.0;
            int count = 0;

            for (int j = i - period + 1; j <= i; j++)
            {
                double val = source[j];
                if (double.IsFinite(val))
                {
                    sum += val;
                    sumSq += val * val;
                    count++;
                }
            }

            double variance = 0.0;
            if (count > 0)
            {
                double mean = sum / count;
                variance = (sumSq / count) - (mean * mean);
                variance = Math.Max(0.0, variance); // Guard against negative due to floating point
            }

            double stdDev = Math.Sqrt(variance);
            double offset = multiplier * stdDev;

            upper[i] = middle[i] + offset;
            lower[i] = middle[i] - offset;
        }
    }
}