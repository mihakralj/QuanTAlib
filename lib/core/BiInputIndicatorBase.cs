using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Delegate for bi-input batch calculation methods.
/// This custom delegate is required because Action&lt;T1,T2,T3,T4&gt; cannot accept
/// ref struct types (Span, ReadOnlySpan) as generic parameters in .NET 8.0.
/// </summary>
/// <param name="actual">Actual values span</param>
/// <param name="predicted">Predicted values span</param>
/// <param name="output">Output span for results</param>
/// <param name="period">Calculation period</param>
public delegate void BiInputBatchDelegate(
    ReadOnlySpan<double> actual,
    ReadOnlySpan<double> predicted,
    Span<double> output,
    int period);

/// <summary>
/// Abstract base class for bi-input indicators (indicators that require two inputs like error metrics).
/// Provides common infrastructure for RingBuffer-based sliding window calculations with O(1) updates.
/// </summary>
/// <remarks>
/// This base class eliminates code duplication across error indicators (MAE, MSE, RMSE, MAPE, etc.)
/// by providing:
/// - Common state management with bar correction (isNew semantics)
/// - RingBuffer-based sliding window with running sum
/// - Periodic resync for floating-point drift correction
/// - NaN/Infinity handling with last-valid-value substitution
/// - Template Method pattern: subclasses only implement ComputeError and optionally PostProcess
/// </remarks>
[SkipLocalsInit]
public abstract class BiInputIndicatorBase : AbstractBase
{
    protected readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    protected record struct BiInputState(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);

    protected BiInputState _state;
    protected BiInputState _p_state;

    protected const int ResyncInterval = 1000;

    /// <summary>
    /// Creates a bi-input indicator with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    /// <param name="name">Indicator name</param>
    protected BiInputIndicatorBase(int period, string name)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = name;
        WarmupPeriod = period;
    }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _buffer.Capacity;

    /// <summary>
    /// Computes the error value from actual and predicted values.
    /// Subclasses implement this to define their specific error computation.
    /// </summary>
    /// <param name="actual">Actual value</param>
    /// <param name="predicted">Predicted value</param>
    /// <returns>Error value to be averaged</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract double ComputeError(double actual, double predicted);

    /// <summary>
    /// Optional post-processing of the mean result.
    /// Default implementation returns the mean unchanged.
    /// Override for indicators like RMSE that need sqrt of mean.
    /// </summary>
    /// <param name="mean">The mean of error values</param>
    /// <returns>Post-processed result</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual double PostProcess(double mean) => mean;

    /// <summary>
    /// Sanitizes input value, substituting last valid value for NaN/Infinity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeActual(double value)
    {
        if (double.IsFinite(value))
        {
            _state.LastValidActual = value;
            return value;
        }
        return double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
    }

    /// <summary>
    /// Sanitizes predicted value, substituting last valid value for NaN/Infinity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizePredicted(double value)
    {
        if (double.IsFinite(value))
        {
            _state.LastValidPredicted = value;
            return value;
        }
        return double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
    }

    /// <summary>
    /// Gets the value to be removed from the running sum (oldest value or 0 if buffer not full).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetRemovedValue() =>
        _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;

    /// <summary>
    /// Processes a new bar update.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessNewBar(double error)
    {
        _p_state = _state;
        // Snapshot buffer state BEFORE Add so Restore can undo it
        _buffer.Snapshot();
        _state.Sum = _state.Sum - GetRemovedValue() + error;
        _buffer.Add(error);
        _state.TickCount++;

        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.Sum = _buffer.RecalculateSum();
        }
    }

    /// <summary>
    /// Processes a bar correction (same bar update).
    /// Uses O(1) differential update: restores buffer and scalar state, then applies the new error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double error)
    {
        // Restore scalar state
        _state = _p_state;
        // Restore buffer to state before last Add (undoes the Add completely)
        _buffer.Restore();
        // Now add the new correction value (this overwrites the same slot)
        _buffer.Add(error);
        // Update sum from buffer (Add already updated it correctly)
        _state.Sum = _buffer.Sum;
    }

    /// <summary>
    /// Updates the indicator with new actual and predicted values.
    /// </summary>
    /// <param name="actual">Actual value</param>
    /// <param name="predicted">Predicted value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The calculated indicator value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = SanitizeActual(actual.Value);
        double predictedVal = SanitizePredicted(predicted.Value);
        double error = ComputeError(actualVal, predictedVal);

        if (isNew)
            ProcessNewBar(error);
        else
            ProcessBarCorrection(error);

        double mean = _buffer.Count > 0 ? _state.Sum / _buffer.Count : error;
        double result = PostProcess(mean);

        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with raw double values.
    /// Uses DateTime.MinValue as a sentinel timestamp for performance in high-frequency scenarios.
    /// For time-sensitive applications, use Update(TValue, TValue, bool) with explicit timestamps.
    /// </summary>
    /// <param name="actual">Actual value</param>
    /// <param name="predicted">Predicted value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The calculated indicator value (with DateTime.MinValue as timestamp)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.MinValue, actual), new TValue(DateTime.MinValue, predicted), isNew);
    }

    /// <summary>
    /// Single-input Update is not supported for bi-input indicators.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException($"{Name} requires two inputs. Use Update(actual, predicted).");
    }

    /// <summary>
    /// Single-series Update is not supported for bi-input indicators.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException($"{Name} requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    /// <summary>
    /// Single-series Prime is not supported for bi-input indicators.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException($"{Name} requires two inputs.");
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Helper method for subclasses to implement static Calculate with TSeries.
    /// </summary>
    protected static TSeries CalculateImpl(
        TSeries actual,
        TSeries predicted,
        int period,
        BiInputBatchDelegate batchMethod)
    {
        if (actual.Count != predicted.Count)
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        batchMethod(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Common validation for Batch methods.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ValidateBatchInputs(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
    }
}