// Ulcer Index (UI) Indicator
// Measures downside volatility by tracking drawdowns from recent highs

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// UI: Ulcer Index
/// A volatility indicator that measures downside risk by calculating the
/// root mean square of percentage drawdowns from recent highs.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Track highest close over period (rolling maximum)</item>
/// <item>Calculate percent drawdown: ((close - highestClose) / highestClose) × 100</item>
/// <item>Square the drawdown</item>
/// <item>Average the squared drawdowns over the period</item>
/// <item>Take square root: UI = √(avgSquaredDrawdown)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Measures only downside volatility (unlike ATR which measures both directions)</item>
/// <item>Zero when price is at period high (no drawdown)</item>
/// <item>Higher values indicate deeper/longer drawdowns</item>
/// <item>Useful for risk-adjusted performance metrics (Martin Ratio)</item>
/// </list>
///
/// <b>Sources:</b>
/// Peter G. Martin, Byron B. McCann (1989). "The Investor's Guide to Fidelity Funds."
/// </remarks>
[SkipLocalsInit]
public sealed class Ui : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _closeBuffer;
    private readonly RingBuffer _squaredDrawdownBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumSquaredDrawdown,
        double LastValidClose,
        double LastUi,
        int Count
    );
    private State _s;
    private State _ps;

    // Backup buffers for state rollback
    private readonly double[] _closeBackup;
    private readonly double[] _squaredDrawdownBackup;

    /// <summary>
    /// Initializes a new instance of the Ui class.
    /// </summary>
    /// <param name="period">The lookback period for calculating drawdowns (default 14).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Ui(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        Name = $"Ui({period})";
        _closeBuffer = new RingBuffer(period);
        _squaredDrawdownBuffer = new RingBuffer(period);
        _closeBackup = new double[period];
        _squaredDrawdownBackup = new double[period];
        _s = new State(0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Ui class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The lookback period (default 14).</param>
    public Ui(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// The lookback period.
    /// </summary>
    public int Period => _period;

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
    /// <returns>The calculated Ulcer Index value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.Close, isNew);
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double close, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            // Backup buffers
            _closeBuffer.CopyTo(_closeBackup);
            _squaredDrawdownBuffer.CopyTo(_squaredDrawdownBackup);
        }
        else
        {
            _s = _ps;
            // Restore buffers
            _closeBuffer.Clear();
            for (int i = 0; i < _closeBackup.Length && i < _ps.Count; i++)
            {
                _closeBuffer.Add(_closeBackup[i]);
            }
            _squaredDrawdownBuffer.Clear();
            for (int i = 0; i < _squaredDrawdownBackup.Length && i < _ps.Count; i++)
            {
                _squaredDrawdownBuffer.Add(_squaredDrawdownBackup[i]);
            }
        }

        var s = _s;

        // Handle non-finite values
        if (!double.IsFinite(close))
        {
            close = s.LastValidClose;
        }
        else
        {
            s.LastValidClose = close;
        }

        // Add close to buffer
        _closeBuffer.Add(close);

        // Find highest close over period
        double highestClose = close;
        for (int i = 0; i < _closeBuffer.Count; i++)
        {
            if (_closeBuffer[i] > highestClose)
            {
                highestClose = _closeBuffer[i];
            }
        }

        // Calculate percent drawdown
        double percentDrawdown = highestClose > 0 ? ((close - highestClose) / highestClose) * 100.0 : 0;
        double squaredDrawdown = percentDrawdown * percentDrawdown;

        // Update running sum (remove oldest if buffer is full)
        double sumSquaredDrawdown = s.SumSquaredDrawdown;
        if (_squaredDrawdownBuffer.Count >= _period)
        {
            sumSquaredDrawdown -= _squaredDrawdownBuffer[0];
        }
        sumSquaredDrawdown += squaredDrawdown;

        _squaredDrawdownBuffer.Add(squaredDrawdown);

        // Calculate UI
        int count = Math.Min(_squaredDrawdownBuffer.Count, _period);
        double avgSquaredDrawdown = count > 0 ? sumSquaredDrawdown / count : 0;
        double ui = Math.Sqrt(avgSquaredDrawdown);

        if (!double.IsFinite(ui) || ui < 0)
        {
            ui = s.LastUi;
        }
        else
        {
            s.LastUi = ui;
        }

        // Update state
        s.SumSquaredDrawdown = sumSquaredDrawdown;
        if (isNew)
        {
            s.Count++;
        }

        _s = s;

        Last = new TValue(timeTicks, ui);
        PubEvent(Last, isNew);
        return Last;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _closeBuffer.Clear();
        _squaredDrawdownBuffer.Clear();
        Array.Clear(_closeBackup);
        Array.Clear(_squaredDrawdownBackup);
        _s = new State(0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates Ulcer Index for a series (static).
    /// </summary>
    /// <param name="source">The source series.</param>
    /// <param name="period">The lookback period.</param>
    /// <returns>A TSeries containing the Ulcer Index values.</returns>
    public static TSeries Batch(TSeries source, int period = 14)
    {
        var ui = new Ui(period);
        return ui.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    /// <param name="source">Close prices.</param>
    /// <param name="output">Output Ulcer Index values.</param>
    /// <param name="period">The lookback period.</param>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> output,
        int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
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
        double[]? closeRented = null;
        double[]? sqDrawdownRented = null;

        if (period > StackallocThreshold)
        {
            closeRented = ArrayPool<double>.Shared.Rent(period);
            sqDrawdownRented = ArrayPool<double>.Shared.Rent(period);
        }

        try
        {
            scoped Span<double> closeBuffer = period <= StackallocThreshold
                ? stackalloc double[period]
                : closeRented.AsSpan(0, period);

            scoped Span<double> sqDrawdownBuffer = period <= StackallocThreshold
                ? stackalloc double[period]
                : sqDrawdownRented.AsSpan(0, period);

            closeBuffer.Clear();
            sqDrawdownBuffer.Clear();

            double lastValidClose = 0;
            double sumSquaredDrawdown = 0;
            int bufferCount = 0;
            int bufferIndex = 0;

            for (int i = 0; i < len; i++)
            {
                double close = source[i];

                // Handle non-finite values
                if (!double.IsFinite(close))
                {
                    close = lastValidClose;
                }
                else
                {
                    lastValidClose = close;
                }

                // Add to circular buffer
                closeBuffer[bufferIndex] = close;

                // Find highest close in buffer
                int currentCount = Math.Min(bufferCount + 1, period);
                double highestClose = close;
                for (int j = 0; j < currentCount; j++)
                {
                    int idx = (bufferIndex - j + period) % period;
                    if (closeBuffer[idx] > highestClose)
                    {
                        highestClose = closeBuffer[idx];
                    }
                }

                // Calculate percent drawdown
                double percentDrawdown = highestClose > 0 ? ((close - highestClose) / highestClose) * 100.0 : 0;
                double squaredDrawdown = percentDrawdown * percentDrawdown;

                // Update running sum
                if (bufferCount >= period)
                {
                    sumSquaredDrawdown -= sqDrawdownBuffer[bufferIndex];
                }
                sumSquaredDrawdown += squaredDrawdown;
                sqDrawdownBuffer[bufferIndex] = squaredDrawdown;

                // Calculate UI
                int count = Math.Min(bufferCount + 1, period);
                double avgSquaredDrawdown = count > 0 ? sumSquaredDrawdown / count : 0;
                double ui = Math.Sqrt(avgSquaredDrawdown);

                if (!double.IsFinite(ui) || ui < 0)
                {
                    ui = i > 0 ? output[i - 1] : 0;
                }

                output[i] = ui;

                // Advance buffer index
                bufferIndex = (bufferIndex + 1) % period;
                if (bufferCount < period)
                {
                    bufferCount++;
                }
            }
        }
        finally
        {
            if (closeRented != null)
            {
                ArrayPool<double>.Shared.Return(closeRented);
            }
            if (sqDrawdownRented != null)
            {
                ArrayPool<double>.Shared.Return(sqDrawdownRented);
            }
        }
    }

    public static (TSeries Results, Ui Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Ui(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
