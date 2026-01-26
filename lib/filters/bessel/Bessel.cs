using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BESSEL: 2nd-order Bessel Low-pass Filter with maximally flat group delay.
/// </summary>
/// <remarks>
/// <para>
/// The Bessel filter is a 2nd-order IIR low-pass filter designed to preserve signal shape
/// and timing. Unlike sharper filters (Butterworth, Chebyshev) that prioritize steep roll-off,
/// the Bessel family is engineered for <b>maximally flat group delay</b>: signals are delayed
/// uniformly across frequencies, preserving waveform integrity without overshoot or ringing.
/// </para>
///
/// <para><b>Coefficient Derivation (for cutoff length L):</b></para>
/// <code>
/// a  = exp(-π / L)
/// b  = 2 · a · cos(1.738 · π / L)     // 1.738 ≈ √3 for 2nd-order Bessel characteristics
/// c₂ = b
/// c₃ = -a²
/// c₁ = 1 - c₂ - c₃
/// </code>
///
/// <para><b>Recursive IIR Form:</b></para>
/// <code>
/// F[n] = c₁ · Src[n] + c₂ · F[n-1] + c₃ · F[n-2]
/// </code>
///
/// <para><b>Complexity:</b></para>
/// <list type="bullet">
///   <item><description>Time: O(1) per update - constant 3 multiplications + 2 additions</description></item>
///   <item><description>Space: O(1) - only 2 previous filter values stored</description></item>
///   <item><description>SIMD: Not applicable due to IIR recursive data dependency</description></item>
/// </list>
///
/// <para><b>Numerical Considerations:</b></para>
/// <list type="bullet">
///   <item><description>Uses <see cref="Math.FusedMultiplyAdd"/> for improved precision and potential performance</description></item>
///   <item><description>NaN/Infinity inputs are substituted with last valid value to prevent state corruption</description></item>
///   <item><description>Minimum length of 2 required for 2nd-order filter numerical stability</description></item>
/// </list>
///
/// <para><b>Sources:</b></para>
/// <list type="bullet">
///   <item><description>John Ehlers - "Cybernetic Analysis for Stocks and Futures"</description></item>
///   <item><description>Friedrich Bessel - Bessel polynomials and filter theory</description></item>
/// </list>
/// </remarks>
[SkipLocalsInit]
public sealed class Bessel : AbstractBase
{
    /// <summary>
    /// Internal state for the Bessel filter, stored as a value type for performance.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="LayoutKind.Auto"/> for optimal memory layout.
    /// Record struct provides value semantics for safe state rollback on bar corrections.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double F1, double F2, double LastValidValue, int Count, bool IsHot)
    {
        /// <summary>Creates a new default state instance.</summary>
        public static State New() => new()
        {
            F1 = 0,
            F2 = 0,
            LastValidValue = 0,
            Count = 0,
            IsHot = false,
        };
    }

    /// <summary>Filter coefficient for current input: c₁ = 1 - c₂ - c₃.</summary>
    private readonly double _c1;

    /// <summary>Filter coefficient for F[n-1]: c₂ = 2a·cos(1.738π/L).</summary>
    private readonly double _c2;

    /// <summary>Filter coefficient for F[n-2]: c₃ = -a².</summary>
    private readonly double _c3;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state = State.New();
    private State _p_state = State.New();

    /// <summary>
    /// Initializes a new Bessel filter with the specified cutoff length.
    /// </summary>
    /// <param name="length">
    /// Cutoff period in bars. Larger values produce smoother output with more lag.
    /// Must be at least 2 for 2nd-order filter numerical stability.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="length"/> is less than 2.</exception>
    /// <remarks>
    /// Coefficient computation: O(1) - performed once at construction using exp/cos.
    /// </remarks>
    public Bessel(int length)
    {
        if (length < 2)
        {
            throw new ArgumentException("Length must be at least 2 for 2nd-order Bessel filter", nameof(length));
        }

        double a = Math.Exp(-Math.PI / length);
        double b = 2.0 * a * Math.Cos(1.738 * Math.PI / length);
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1.0 - _c2 - _c3;

        Name = $"Bessel({length})";
        WarmupPeriod = length;
    }

    /// <summary>
    /// Initializes a Bessel filter subscribed to a source publisher for reactive updates.
    /// </summary>
    /// <param name="source">The data source to subscribe to. Updates are received via the Pub event.</param>
    /// <param name="length">Cutoff period in bars (must be >= 2).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="length"/> is less than 2.</exception>
    /// <remarks>
    /// The filter subscribes directly to the source's Pub event for zero-copy reactive updates.
    /// Call <see cref="Dispose"/> to unsubscribe when the filter is no longer needed.
    /// </remarks>
    public Bessel(ITValuePublisher source, int length) : this(length)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    /// <summary>
    /// Initializes a Bessel filter pre-primed with historical data and subscribed for future updates.
    /// </summary>
    /// <param name="source">
    /// The TSeries containing historical data for priming and future updates.
    /// All existing values are processed immediately via <see cref="Prime"/>.
    /// </param>
    /// <param name="length">Cutoff period in bars (must be >= 2).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="length"/> is less than 2.</exception>
    /// <remarks>
    /// <para>Complexity: O(n) for initial priming where n = source.Count, then O(1) per update.</para>
    /// <para>After construction, the filter is ready to produce valid output if source.Count >= WarmupPeriod.</para>
    /// </remarks>
    public Bessel(TSeries source, int length) : this(length)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }

        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    /// <inheritdoc />
    public override bool IsHot => _state.IsHot;

    /// <summary>
    /// Initializes the filter state using historical data without producing output.
    /// </summary>
    /// <param name="source">Historical values to process for state initialization.</param>
    /// <param name="step">Time interval between values (unused for Bessel, included for API compatibility).</param>
    /// <remarks>
    /// <para><b>Complexity:</b> O(n) where n = source.Length</para>
    /// <para>After priming, the filter's <see cref="IsHot"/> property reflects whether enough data was provided.</para>
    /// <para>NaN values in source are handled via last-valid-value substitution.</para>
    /// </remarks>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        Reset();

        int len = source.Length;
        int i = 0;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _state.LastValidValue = source[k];
                _state.F1 = _state.LastValidValue;
                _state.F2 = _state.LastValidValue;
                _state.Count = 1;
                i = k + 1;
                break;
            }
        }

        // Handle case where all inputs are NaN
        if (_state.Count == 0)
        {
            _state.LastValidValue = double.NaN;
            _state.F1 = double.NaN;
            _state.F2 = double.NaN;
            _state.IsHot = false;
            Last = new TValue(DateTime.MinValue, double.NaN);
            _p_state = _state;
            return;
        }

        // Warmup phase: pass-through until enough history (Count >= 2)
        for (; i < len && _state.Count < 2; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                _state.LastValidValue = val;
            }
            else
            {
                val = _state.LastValidValue;
            }

            _state.F2 = _state.F1;
            _state.F1 = val;
            _state.Count++;
        }

        // Hot phase: main filtering loop (no warmup check)
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                _state.LastValidValue = val;
            }
            else
            {
                val = _state.LastValidValue;
            }

            double filt = Math.FusedMultiplyAdd(_c3, _state.F2,
                Math.FusedMultiplyAdd(_c2, _state.F1, _c1 * val));

            _state.F2 = _state.F1;
            _state.F1 = filt;
            _state.Count++;
        }

        if (_state.Count >= WarmupPeriod)
        {
            _state.IsHot = true;
        }

        Last = new TValue(DateTime.MinValue, _state.F1);

        _p_state = _state;
    }

    /// <summary>
    /// Returns a finite value for calculation, substituting last valid value for NaN/Infinity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }

        return _state.LastValidValue;
    }

    /// <summary>
    /// Updates the filter with a single input value.
    /// </summary>
    /// <param name="input">The input value containing timestamp and price data.</param>
    /// <param name="isNew">
    /// True if this is a new bar (advances state), False if updating current bar (rolls back then recomputes).
    /// </param>
    /// <returns>The filtered output value with the same timestamp as input.</returns>
    /// <remarks>
    /// <para><b>Complexity:</b> O(1) - constant time regardless of filter length or history.</para>
    /// <para><b>Operations:</b> 3 multiplications + 2 additions using FMA for precision.</para>
    /// <para><b>Allocations:</b> Zero heap allocations on hot path.</para>
    /// <para>
    /// Bar correction: When <paramref name="isNew"/> is false, the filter rolls back to the
    /// previous state before applying the update, enabling intra-bar recalculation.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);

        if (_state.Count == 0)
        {
            _state.F1 = val;
            _state.F2 = val;
        }

        // 2nd-order filter needs 2 history points (Count >= 2)
        double filt = _state.Count < 2
            ? val
            : Math.FusedMultiplyAdd(_c3, _state.F2,
                Math.FusedMultiplyAdd(_c2, _state.F1, _c1 * val));

        _state.F2 = _state.F1;
        _state.F1 = filt;

        if (isNew)
        {
            _state.Count++;
        }

        if (!_state.IsHot && _state.Count >= WarmupPeriod)
        {
            _state.IsHot = true;
        }

        Last = new TValue(input.Time, filt);
        PubEvent(Last);
        return Last;
    }

    /// <summary>
    /// Processes an entire time series and returns filtered results.
    /// </summary>
    /// <param name="source">The input time series to filter.</param>
    /// <returns>A new TSeries containing filtered values with preserved timestamps.</returns>
    /// <remarks>
    /// <para><b>Complexity:</b> O(n) where n = source.Count</para>
    /// <para>Uses optimized span-based batch processing internally.</para>
    /// <para>Updates internal state to match the end of the processed series.</para>
    /// </remarks>
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
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        State state = _state;

        CalculateCore(sourceValues, vSpan, _c1, _c2, _c3, WarmupPeriod, ref state);

        _state = state;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core calculation loop shared by all batch processing methods.
    /// </summary>
    /// <remarks>
    /// <para><b>Complexity:</b> O(n) where n = source.Length</para>
    /// <para>Handles warmup, NaN substitution, and state management in a single pass.</para>
    /// <para>Uses FMA for the IIR calculation: F = c1*val + c2*F1 + c3*F2</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCore(
        ReadOnlySpan<double> source,
        Span<double> output,
        double c1,
        double c2,
        double c3,
        int warmupPeriod,
        ref State state)
    {
        int len = source.Length;
        int i = 0;

        // If starting from scratch (count == 0), find first valid value
        if (state.Count == 0)
        {
            for (; i < len; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    state.LastValidValue = source[i];
                    state.F1 = state.LastValidValue;
                    state.F2 = state.LastValidValue;
                    output[i] = state.LastValidValue;
                    state.Count = 1;
                    i++;
                    break;
                }

                output[i] = double.NaN;
            }
        }

        // Warmup phase: pass-through until enough history (Count >= 2)
        for (; i < len && state.Count < 2; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                state.LastValidValue = val;
            }
            else
            {
                val = state.LastValidValue;
            }

            state.F2 = state.F1;
            state.F1 = val;
            output[i] = val;
            state.Count++;
        }

        // Hot phase: main filtering loop (no warmup check)
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                state.LastValidValue = val;
            }
            else
            {
                val = state.LastValidValue;
            }

            double filt = Math.FusedMultiplyAdd(c3, state.F2,
                Math.FusedMultiplyAdd(c2, state.F1, c1 * val));

            state.F2 = state.F1;
            state.F1 = filt;
            output[i] = filt;
            state.Count++;
        }

        if (!state.IsHot && state.Count >= warmupPeriod)
        {
            state.IsHot = true;
        }
    }

    /// <summary>
    /// Calculates filtered values for a time series and returns both results and a primed indicator.
    /// </summary>
    /// <param name="source">The input time series to filter.</param>
    /// <param name="length">Cutoff period in bars (must be >= 2).</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><description>Results: TSeries with filtered values</description></item>
    ///   <item><description>Indicator: A primed Bessel instance ready for streaming updates</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="length"/> is less than 2.</exception>
    /// <remarks>
    /// <para><b>Complexity:</b> O(n) where n = source.Count</para>
    /// <para>The returned indicator maintains state and can continue processing new values.</para>
    /// </remarks>
    public static (TSeries Results, Bessel Indicator) Calculate(TSeries source, int length)
    {
        var bessel = new Bessel(length);
        TSeries results = bessel.Update(source);
        return (results, bessel);
    }

    /// <summary>
    /// Calculates filtered values for a span of doubles (stateless batch processing).
    /// </summary>
    /// <param name="source">Input values to filter.</param>
    /// <param name="output">Output span to write filtered values (must be same length as source).</param>
    /// <param name="length">Cutoff period in bars (must be >= 2).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="length"/> is less than 2 or when source and output lengths differ.
    /// </exception>
    /// <remarks>
    /// <para><b>Complexity:</b> O(n) where n = source.Length</para>
    /// <para><b>Allocations:</b> Zero heap allocations (state is stack-allocated).</para>
    /// <para>This is the highest-performance API for batch processing without state persistence.</para>
    /// <para>SIMD optimization is not applicable due to IIR recursive data dependency.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int length)
    {
        if (length < 2)
        {
            throw new ArgumentException("Length must be at least 2 for 2nd-order Bessel filter", nameof(length));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double a = Math.Exp(-Math.PI / length);
        double b = 2.0 * a * Math.Cos(1.738 * Math.PI / length);
        double c2 = b;
        double c3 = -a * a;
        double c1 = 1.0 - c2 - c3;

        var state = State.New();

        CalculateCore(source, output, c1, c2, c3, length, ref state);
    }

    /// <summary>
    /// Event handler for reactive updates from subscribed publishers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Resets the filter to its initial state, clearing all history.
    /// </summary>
    /// <remarks>
    /// After reset, <see cref="IsHot"/> will be false and the filter will need to
    /// reaccumulate warmup data before producing valid filtered output.
    /// </remarks>
    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        Last = default;
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    /// <remarks>
    /// Call this method when the filter is no longer needed to prevent memory leaks
    /// from dangling event subscriptions. Safe to call multiple times.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
