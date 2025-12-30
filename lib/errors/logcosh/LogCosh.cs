using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LogCosh: Log-Cosh Loss
/// </summary>
/// <remarks>
/// Log-Cosh is the logarithm of the hyperbolic cosine of the error. It is a
/// smooth approximation to the absolute error that is twice differentiable
/// everywhere, making it suitable for gradient-based optimization.
///
/// Formula:
/// LogCosh = (1/n) * Σ log(cosh(actual - predicted))
///
/// Key properties:
/// - Smooth and differentiable everywhere
/// - Approximates L1 loss for large errors
/// - Approximates L2 loss for small errors
/// - Less sensitive to outliers than MSE
/// - Numerically stable (uses stable computation for large values)
/// </remarks>
[SkipLocalsInit]
public sealed class LogCosh : AbstractBase
{
    private readonly RingBuffer _logCoshBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LogCoshSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public LogCosh(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _logCoshBuffer = new RingBuffer(period);
        Name = $"LogCosh({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _logCoshBuffer.IsFull;

    /// <summary>
    /// Computes log(cosh(x)) in a numerically stable way.
    /// For large |x|, cosh(x) ≈ exp(|x|)/2, so log(cosh(x)) ≈ |x| - log(2)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double StableLogCosh(double x)
    {
        double absX = Math.Abs(x);
        // For large values, use asymptotic approximation to avoid overflow
        if (absX > 20.0)
            return absX - 0.6931471805599453; // log(2)
        return Math.Log(Math.Cosh(x));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        double error = actualVal - predictedVal;
        double logCoshValue = StableLogCosh(error);

        if (isNew)
        {
            _p_state = _state;

            double removedLogCosh = _logCoshBuffer.Count == _logCoshBuffer.Capacity ? _logCoshBuffer.Oldest : 0.0;
            _state.LogCoshSum = _state.LogCoshSum - removedLogCosh + logCoshValue;
            _logCoshBuffer.Add(logCoshValue);

            _state.TickCount++;
            if (_logCoshBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.LogCoshSum = _logCoshBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedLogCosh = _logCoshBuffer.Count == _logCoshBuffer.Capacity ? _logCoshBuffer.Oldest : 0.0;
            _state.LogCoshSum = _state.LogCoshSum - removedLogCosh + logCoshValue;
            _logCoshBuffer.UpdateNewest(logCoshValue);
            _state.LogCoshSum = _logCoshBuffer.RecalculateSum();
        }

        // LogCosh = (1/n) * Σ log(cosh(error))
        double result = _logCoshBuffer.Count > 0 ? _state.LogCoshSum / _logCoshBuffer.Count : 0.0;

        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, actual), new TValue(DateTime.UtcNow, predicted), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("LogCosh requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("LogCosh requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("LogCosh requires two inputs.");
    }

    public override void Reset()
    {
        _logCoshBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
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

        Batch(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = actual.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        Span<double> logCoshBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double logCoshSum = 0;
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k])) { lastValidActual = actual[k]; break; }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k])) { lastValidPredicted = predicted[k]; break; }
        }

        int bufferIndex = 0;
        int i = 0;

        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = act - pred;
            double logCoshValue = StableLogCosh(error);

            logCoshSum += logCoshValue;
            logCoshBuffer[i] = logCoshValue;

            output[i] = logCoshSum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = act - pred;
            double logCoshValue = StableLogCosh(error);

            logCoshSum = logCoshSum - logCoshBuffer[bufferIndex] + logCoshValue;
            logCoshBuffer[bufferIndex] = logCoshValue;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = logCoshSum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                    recalcSum += logCoshBuffer[k];
                logCoshSum = recalcSum;
            }
        }
    }
}
