using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Price Momentum Oscillator (PMO), a double-smoothed rate of change
/// developed by Carl Swenlin (DecisionPoint).
/// </summary>
/// <remarks>
/// DecisionPoint PMO Algorithm:
/// <c>ROC = (Close / Close[1] - 1) × 100</c>  (always 1-bar),
/// <c>RocEma = CustomEMA(ROC, timePeriods) × 10</c>,
/// <c>PMO = CustomEMA(RocEma, smoothPeriods)</c>.
///
/// Custom EMA uses alpha = 2/N (not the standard 2/(N+1)), and is seeded with the SMA
/// of the first N values. This matches the original DecisionPoint specification and agrees
/// with both Skender.Stock.Indicators and OoplesFinance implementations.
///
/// PMO oscillates around zero; positive values indicate upward momentum, negative values
/// indicate downward momentum. Crossings of zero or a signal line suggest trend changes.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="pmo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pmo : AbstractBase
{
    private const int DefaultTimePeriods = 35;
    private const int DefaultSmoothPeriods = 20;
    private const int DefaultSignalPeriods = 10;

    private readonly int _timePeriods;
    private readonly int _smoothPeriods;
    private readonly double _alpha1;
    private readonly double _alpha2;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValid,
        double PrevClose,
        double RocEmaRaw,
        double Pmo,
        double RocSum,
        double RocEmaScaledSum,
        int RocCount,
        int RocEmaCount,
        bool HasPrevClose,
        bool RocEmaSeeded,
        bool PmoSeeded,
        int Bars);
    private State _state, _p_state;

    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// True when the indicator has enough data to produce meaningful PMO values.
    /// </summary>
    public override bool IsHot => _state.Bars > _timePeriods + _smoothPeriods;

    /// <summary>
    /// Initializes a new PMO indicator.
    /// </summary>
    /// <param name="timePeriods">First EMA smoothing period for 1-bar ROC (must be >= 2)</param>
    /// <param name="smoothPeriods">Second EMA smoothing period for PMO (must be >= 1)</param>
    /// <param name="signalPeriods">Signal line EMA period (reserved for future use, must be >= 1)</param>
    public Pmo(int timePeriods = DefaultTimePeriods, int smoothPeriods = DefaultSmoothPeriods, int signalPeriods = DefaultSignalPeriods)
    {
        if (timePeriods < 2)
        {
            throw new ArgumentException("Time periods must be >= 2", nameof(timePeriods));
        }

        if (smoothPeriods < 1)
        {
            throw new ArgumentException("Smooth periods must be >= 1", nameof(smoothPeriods));
        }

        if (signalPeriods < 1)
        {
            throw new ArgumentException("Signal periods must be >= 1", nameof(signalPeriods));
        }

        _timePeriods = timePeriods;
        _smoothPeriods = smoothPeriods;
        // DecisionPoint PMO uses custom smoothing: alpha = 2/N (not standard EMA 2/(N+1))
        _alpha1 = 2.0 / _timePeriods;
        _alpha2 = 2.0 / _smoothPeriods;

        Name = $"Pmo({timePeriods},{smoothPeriods},{signalPeriods})";
        WarmupPeriod = timePeriods + smoothPeriods;
    }

    /// <summary>
    /// Initializes a new PMO indicator with source for event-based chaining.
    /// </summary>
    public Pmo(ITValuePublisher source, int timePeriods = DefaultTimePeriods, int smoothPeriods = DefaultSmoothPeriods, int signalPeriods = DefaultSignalPeriods)
        : this(timePeriods, smoothPeriods, signalPeriods)
    {
        _source = source;
        _source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

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

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;
        _state.LastValid = value;
        _state.Bars++;

        // Step 1: Compute 1-bar percentage ROC
        double roc;
        if (!_state.HasPrevClose)
        {
            roc = 0.0;
            _state.HasPrevClose = true;
            _state.PrevClose = value;
        }
        else
        {
            roc = _state.PrevClose != 0.0
                ? ((value / _state.PrevClose) - 1.0) * 100.0
                : 0.0;
            _state.PrevClose = value;
        }

        // Step 2: First Custom EMA smoothing of 1-bar ROC (SMA-seeded, alpha = 2/timePeriods)
        // Skender seeds at index timePeriods (after timePeriods+1 bars), using SMA of timePeriods ROC values [1..timePeriods]
        // For streaming: accumulate first _timePeriods ROC values (skip index 0 which has no prev close)
        double rocEmaScaled;
        if (!_state.RocEmaSeeded)
        {
            if (_state.Bars == 1)
            {
                // First bar: ROC = 0, skip for SMA accumulation (Skender starts ROC at index 1)
                _state.RocEmaRaw = 0.0;
                rocEmaScaled = 0.0;
            }
            else
            {
                // Accumulate ROC values for SMA seed
                _state.RocSum += roc;
                _state.RocCount++;

                if (_state.RocCount >= _timePeriods)
                {
                    // SMA seed: average of first _timePeriods ROC values
                    _state.RocEmaRaw = _state.RocSum / _timePeriods;
                    _state.RocEmaSeeded = true;
                    rocEmaScaled = _state.RocEmaRaw * 10.0;
                }
                else
                {
                    _state.RocEmaRaw = 0.0;
                    rocEmaScaled = 0.0;
                }
            }
        }
        else
        {
            // Custom EMA: alpha = 2/N
            _state.RocEmaRaw = Math.FusedMultiplyAdd(roc - _state.RocEmaRaw, _alpha1, _state.RocEmaRaw);
            rocEmaScaled = _state.RocEmaRaw * 10.0;
        }

        // Step 3: Second Custom EMA smoothing → PMO (SMA-seeded, alpha = 2/smoothPeriods)
        double pmoValue;
        if (!_state.RocEmaSeeded)
        {
            // Not enough data for first EMA yet
            pmoValue = 0.0;
        }
        else if (!_state.PmoSeeded)
        {
            // Accumulate RocEma scaled values for SMA seed
            _state.RocEmaScaledSum += rocEmaScaled;
            _state.RocEmaCount++;

            if (_state.RocEmaCount >= _smoothPeriods)
            {
                // SMA seed: average of first _smoothPeriods scaled RocEma values
                _state.Pmo = _state.RocEmaScaledSum / _smoothPeriods;
                _state.PmoSeeded = true;
                pmoValue = _state.Pmo;
            }
            else
            {
                pmoValue = 0.0;
            }
        }
        else
        {
            // Custom EMA: alpha = 2/N
            _state.Pmo = Math.FusedMultiplyAdd(rocEmaScaled - _state.Pmo, _alpha2, _state.Pmo);
            pmoValue = _state.Pmo;
        }

        Last = new TValue(input.Time, pmoValue);
        PubEvent(Last, isNew);
        return Last;
    }

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

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), true);
            tSpan[i] = source.Times[i];
            vSpan[i] = Last.Value;
        }

        _p_state = _state;

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int timePeriods = DefaultTimePeriods, int smoothPeriods = DefaultSmoothPeriods, int signalPeriods = DefaultSignalPeriods)
    {
        var indicator = new Pmo(timePeriods, smoothPeriods, signalPeriods);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates PMO over a span of values.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int timePeriods = DefaultTimePeriods, int smoothPeriods = DefaultSmoothPeriods, int signalPeriods = DefaultSignalPeriods)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (timePeriods < 2)
        {
            throw new ArgumentException("Time periods must be >= 2", nameof(timePeriods));
        }

        if (smoothPeriods < 1)
        {
            throw new ArgumentException("Smooth periods must be >= 1", nameof(smoothPeriods));
        }

        if (signalPeriods < 1)
        {
            throw new ArgumentException("Signal periods must be >= 1", nameof(signalPeriods));
        }

        // DecisionPoint PMO custom smoothing: alpha = 2/N
        double alpha1 = 2.0 / timePeriods;
        double alpha2 = 2.0 / smoothPeriods;

        // Step 1: Compute 1-bar ROC for all bars
        // Step 2: First CustomEMA(ROC, timePeriods) with SMA seed, then ×10
        // Step 3: Second CustomEMA(scaled, smoothPeriods) with SMA seed → PMO

        double rocEmaRaw = 0.0;
        bool rocEmaSeeded = false;
        double rocSum = 0.0;
        int rocCount = 0;

        double pmo = 0.0;
        bool pmoSeeded = false;
        double scaledSum = 0.0;
        int scaledCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            // 1-bar ROC
            double roc = i > 0 && source[i - 1] != 0.0
                ? ((source[i] / source[i - 1]) - 1.0) * 100.0
                : 0.0;

            // First Custom EMA of ROC with SMA seed
            double rocEmaScaled;
            if (!rocEmaSeeded)
            {
                if (i == 0)
                {
                    // First bar: no previous close, ROC = 0, skip accumulation
                    rocEmaScaled = 0.0;
                }
                else
                {
                    rocSum += roc;
                    rocCount++;

                    if (rocCount >= timePeriods)
                    {
                        rocEmaRaw = rocSum / timePeriods;
                        rocEmaSeeded = true;
                        rocEmaScaled = rocEmaRaw * 10.0;
                    }
                    else
                    {
                        rocEmaScaled = 0.0;
                    }
                }
            }
            else
            {
                rocEmaRaw += alpha1 * (roc - rocEmaRaw);
                rocEmaScaled = rocEmaRaw * 10.0;
            }

            // Second Custom EMA of scaled RocEma with SMA seed → PMO
            if (!rocEmaSeeded)
            {
                output[i] = 0.0;
            }
            else if (!pmoSeeded)
            {
                scaledSum += rocEmaScaled;
                scaledCount++;

                if (scaledCount >= smoothPeriods)
                {
                    pmo = scaledSum / smoothPeriods;
                    pmoSeeded = true;
                    output[i] = pmo;
                }
                else
                {
                    output[i] = 0.0;
                }
            }
            else
            {
                pmo += alpha2 * (rocEmaScaled - pmo);
                output[i] = pmo;
            }
        }
    }

    public static (TSeries Results, Pmo Indicator) Calculate(TSeries source, int timePeriods = DefaultTimePeriods, int smoothPeriods = DefaultSmoothPeriods, int signalPeriods = DefaultSignalPeriods)
    {
        var indicator = new Pmo(timePeriods, smoothPeriods, signalPeriods);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= HandleUpdate;
                _source = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
