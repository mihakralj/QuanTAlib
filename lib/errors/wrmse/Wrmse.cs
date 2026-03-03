using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WRMSE: Weighted Root Mean Squared Error
/// </summary>
/// <remarks>
/// WRMSE extends RMSE by allowing each error to be weighted differently,
/// enabling emphasis on certain data points (e.g., recent observations,
/// high-volume periods, or critical price levels).
///
/// Formula:
/// WRMSE = √(Σ(w_i * (actual_i - predicted_i)²) / Σ(w_i))
///
/// Uses dual RingBuffers for O(1) streaming updates with running sums.
///
/// Key properties:
/// - Always non-negative (WRMSE ≥ 0)
/// - Same units as the original data
/// - Weights allow emphasizing important observations
/// - Reduces to RMSE when all weights are equal
/// - WRMSE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Wrmse : AbstractBase
{
    private readonly RingBuffer _weightedErrorBuffer;
    private readonly RingBuffer _weightBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double WeightedErrorSum,
        double WeightSum,
        double LastValidActual,
        double LastValidPredicted,
        double LastValidWeight,
        int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private const double DefaultWeight = 1.0;

    /// <summary>
    /// Creates WRMSE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Wrmse(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _weightedErrorBuffer = new RingBuffer(period);
        _weightBuffer = new RingBuffer(period);
        Name = $"Wrmse({period})";
        WarmupPeriod = period;
        _state.LastValidWeight = DefaultWeight;
        _p_state.LastValidWeight = DefaultWeight;
    }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _weightedErrorBuffer.IsFull;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _weightedErrorBuffer.Capacity;

    /// <summary>
    /// Updates the indicator with actual, predicted, and weight values.
    /// </summary>
    /// <param name="actual">Actual value</param>
    /// <param name="predicted">Predicted value</param>
    /// <param name="weight">Weight for this observation (default 1.0)</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The calculated WRMSE value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, double weight, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Sanitize inputs
        if (!double.IsFinite(actualVal))
        {
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        }
        else
        {
            _state.LastValidActual = actualVal;
        }

        if (!double.IsFinite(predictedVal))
        {
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        }
        else
        {
            _state.LastValidPredicted = predictedVal;
        }

        if (!double.IsFinite(weight) || weight < 0)
        {
            weight = _state.LastValidWeight;
        }
        else
        {
            _state.LastValidWeight = weight;
        }

        // Compute weighted squared error
        double diff = actualVal - predictedVal;
        double weightedError = weight * diff * diff;

        if (isNew)
        {
            _p_state = _state;

            double removedWeightedError = _weightedErrorBuffer.Count == _weightedErrorBuffer.Capacity
                ? _weightedErrorBuffer.Oldest : 0.0;
            _state.WeightedErrorSum = _state.WeightedErrorSum - removedWeightedError + weightedError;
            _weightedErrorBuffer.Add(weightedError);

            double removedWeight = _weightBuffer.Count == _weightBuffer.Capacity
                ? _weightBuffer.Oldest : 0.0;
            _state.WeightSum = _state.WeightSum - removedWeight + weight;
            _weightBuffer.Add(weight);

            _state.TickCount++;
            if (_weightedErrorBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.WeightedErrorSum = _weightedErrorBuffer.RecalculateSum();
                _state.WeightSum = _weightBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            _weightedErrorBuffer.UpdateNewest(weightedError);
            _state.WeightedErrorSum = _weightedErrorBuffer.RecalculateSum();

            _weightBuffer.UpdateNewest(weight);
            _state.WeightSum = _weightBuffer.RecalculateSum();
        }

        // WRMSE = sqrt(Σ(w*e²) / Σ(w))
        double result = _state.WeightSum > 1e-10
            ? Math.Sqrt(_state.WeightedErrorSum / _state.WeightSum)
            : 0.0;

        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with actual and predicted values using default weight of 1.0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        return Update(actual, predicted, DefaultWeight, isNew);
    }

    /// <summary>
    /// Updates the indicator with raw double values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, double weight, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, actual), new TValue(DateTime.UtcNow, predicted), weight, isNew);
    }

    /// <summary>
    /// Updates the indicator with raw double values using default weight of 1.0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(actual, predicted, DefaultWeight, isNew);
    }
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("WRMSE requires two inputs. Use Update(actual, predicted) or Update(actual, predicted, weight).");
    }
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("WRMSE requires two inputs. Use Batch(actualSeries, predictedSeries, period) or Batch(actualSeries, predictedSeries, weightsSeries, period).");
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("WRMSE requires two inputs.");
    }
    public override void Reset()
    {
        _weightedErrorBuffer.Clear();
        _weightBuffer.Clear();
        _state = default;
        _state.LastValidWeight = DefaultWeight;
        _p_state = default;
        _p_state.LastValidWeight = DefaultWeight;
        Last = default;
    }

    /// <summary>
    /// Calculates WRMSE for entire series with uniform weights.
    /// </summary>
    public static TSeries Batch(TSeries actual, TSeries predicted, int period)
    {
        if (actual.Count != predicted.Count)
        {
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));
        }

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates WRMSE for entire series with custom weights.
    /// </summary>
    public static TSeries Batch(TSeries actual, TSeries predicted, TSeries weights, int period)
    {
        if (actual.Count != predicted.Count || actual.Count != weights.Count)
        {
            throw new ArgumentException("All series must have the same length", nameof(weights));
        }

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(actual.Values, predicted.Values, weights.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch calculation with uniform weights (reduces to RMSE behavior).
    /// Uses SIMD-accelerated computation via ErrorHelpers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        // With uniform weights, WRMSE = RMSE
        const int StackAllocThreshold = 256;
        Span<double> sqErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sqErrors);
        ErrorHelpers.ApplyRollingMeanSqrt(sqErrors, output, period);
    }

    /// <summary>
    /// Batch calculation with custom weights.
    /// Uses SIMD-accelerated computation via ErrorHelpers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, ReadOnlySpan<double> weights, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != weights.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;
        Span<double> weightedErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ErrorHelpers.ComputeWeightedErrors(actual, predicted, weights, weightedErrors);
        ErrorHelpers.ApplyRollingWeightedMeanSqrt(weightedErrors, weights, output, period);
    }

    public static (TSeries Results, Wrmse Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Wrmse(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}