using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Time Weighted Average Price (TWAP) that gives equal weight to each price point
/// within a session, optionally resetting at specified period intervals.
/// </summary>
/// <remarks>
/// TWAP Formula:
/// <c>SumPrices += Price</c>,
/// <c>Count += 1</c>,
/// <c>TWAP = SumPrices / Count</c>.
///
/// Session resets when period > 0 and index exceeds period; period of 0 means never reset.
/// This implementation is optimized for streaming updates with O(1) per bar using running sums.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Twap.md">Detailed documentation</seealso>
/// <seealso href="twap.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Twap : ITValuePublisher
{
    private readonly int _period;
    private const int DefaultPeriod = 0;  // 0 = never reset (continuous)

    // State management using record struct for efficiency
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double SumPrices;
        public int Count;
        public int Index;
        public double LastValid;
        public double Twap;
    }

    private State _s;
    private State _ps;

    /// <inheritdoc/>
    public TValue Last { get; private set; }
    /// <inheritdoc/>
    public bool IsHot { get; private set; }
    /// <inheritdoc/>
    public static int WarmupPeriod => 1;
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the TWAP indicator.
    /// </summary>
    /// <param name="period">The session period in bars (0 = never reset). Default is 0.</param>
    /// <exception cref="ArgumentException">Thrown when period is negative.</exception>
    public Twap(int period = DefaultPeriod)
    {
        if (period < 0)
        {
            throw new ArgumentException("Period must be non-negative", nameof(period));
        }
        _period = period;
        Name = period == 0 ? "Twap(∞)" : $"Twap({_period})";
        Reset();
    }

    /// <summary>
    /// Initializes a new instance of the TWAP indicator with a data source.
    /// </summary>
    /// <param name="source">The source indicator providing price data.</param>
    /// <param name="period">The session period in bars (0 = never reset). Default is 0.</param>
    public Twap(ITValuePublisher source, int period = DefaultPeriod) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _s = new State
        {
            SumPrices = 0,
            Count = 0,
            Index = 0,
            LastValid = 0,
            Twap = 0
        };
        _ps = _s;
        Last = default;
        IsHot = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetFiniteValue(double value, double fallback)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private void Handle(object? _, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    /// <summary>
    /// Updates the TWAP with a new bar.
    /// </summary>
    /// <param name="bar">The bar data.</param>
    /// <param name="isNew">True if this is a new bar, false if updating current bar.</param>
    /// <returns>The current TWAP value.</returns>
    public TValue Update(TBar bar, bool isNew = true)
    {
        // Use typical price (HLC3) for TWAP
        double typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
        return Update(new TValue(bar.Time, typicalPrice), isNew);
    }

    /// <summary>
    /// Updates the TWAP with a new price value.
    /// </summary>
    /// <param name="input">The price value.</param>
    /// <param name="isNew">True if this is a new value, false if updating current value.</param>
    /// <returns>The current TWAP value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        // Local copy for struct promotion
        var s = _s;

        // Get valid price (substitute NaN/Infinity with last valid)
        double price = GetFiniteValue(input.Value, s.LastValid);
        s.LastValid = price;

        // Check for session reset
        if (isNew)
        {
            s.Index++;

            // Reset on period boundary (period > 0 means reset every N bars)
            if (_period > 0 && s.Index > _period)
            {
                s.SumPrices = 0;
                s.Count = 0;
                s.Index = 1;
            }
        }

        // Accumulate price
        s.SumPrices += price;
        s.Count++;

        // Calculate TWAP
        s.Twap = s.Count > 0 ? s.SumPrices / s.Count : price;

        // Write back state
        _s = s;

        // Update state tracking
        IsHot = true;  // TWAP is valid after first value

        // Publish result
        Last = new TValue(input.Time, s.Twap);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });

        return Last;
    }

    /// <summary>
    /// Updates the TWAP with a series of bars (batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <returns>The result series.</returns>
    public TSeries Update(TBarSeries source)
    {
        var result = new TSeries(source.Count);
        var prices = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            TBar bar = source[i];
            prices[i] = (bar.High + bar.Low + bar.Close) / 3.0;
        }

        var output = new double[source.Count];
        Calculate(prices, output, _period);

        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(source[i].Time, output[i]));
        }

        // Restore internal state by replaying last values
        Reset();
        // For continuous TWAP (_period == 0), replay entire series
        // For periodic TWAP, replay last _period bars
        int replayCount = _period == 0 ? source.Count : Math.Min(_period, source.Count);
        int replayStart = source.Count - replayCount;
        for (int i = replayStart; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }

        return result;
    }

    /// <summary>
    /// Calculates TWAP for a series of bars (static batch mode).
    /// </summary>
    /// <param name="source">The bar series.</param>
    /// <param name="period">The session period in bars (0 = never reset).</param>
    /// <returns>The result series.</returns>
    public static TSeries Calculate(TBarSeries source, int period = DefaultPeriod)
    {
        var twap = new Twap(period);
        var result = new TSeries(source.Count);

        foreach (var bar in source)
        {
            result.Add(twap.Update(bar));
        }

        return result;
    }

    /// <summary>
    /// Calculates TWAP for span of prices (high-performance span mode).
    /// </summary>
    /// <param name="price">The source price span.</param>
    /// <param name="output">The output TWAP span.</param>
    /// <param name="period">The session period in bars (0 = never reset). Default is 0.</param>
    /// <exception cref="ArgumentException">Thrown when output length doesn't match price length or period is invalid.</exception>
    public static void Calculate(ReadOnlySpan<double> price, Span<double> output, int period = DefaultPeriod)
    {
        if (output.Length != price.Length)
        {
            throw new ArgumentException("Output length must match price length", nameof(output));
        }
        if (period < 0)
        {
            throw new ArgumentException("Period must be non-negative", nameof(period));
        }
        if (price.Length == 0)
        {
            return;
        }

        double sumPrices = 0;
        int count = 0;
        int index = 0;
        double lastValid = price[0];

        for (int i = 0; i < price.Length; i++)
        {
            // Get valid price
            double p = double.IsFinite(price[i]) ? price[i] : lastValid;
            lastValid = p;

            index++;

            // Reset on period boundary
            if (period > 0 && index > period)
            {
                sumPrices = 0;
                count = 0;
                index = 1;
            }

            // Accumulate
            sumPrices += p;
            count++;

            // Calculate TWAP
            output[i] = sumPrices / count;
        }
    }
}