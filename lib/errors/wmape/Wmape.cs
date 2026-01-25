using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WMAPE: Weighted Mean Absolute Percentage Error
/// </summary>
/// <remarks>
/// WMAPE weights errors by the magnitude of actual values, making it more
/// suitable for intermittent demand forecasting where some periods have
/// zero or very low values.
///
/// Formula:
/// WMAPE = (Σ|actual - predicted| / Σ|actual|) * 100
///
/// Key properties:
/// - Scale-independent (expressed as percentage)
/// - Weights larger actual values more heavily
/// - More stable than MAPE for intermittent data
/// - Industry standard for demand forecasting
/// </remarks>
[SkipLocalsInit]
public sealed class Wmape : AbstractBase
{
    private readonly RingBuffer _absErrorBuffer;
    private readonly RingBuffer _absActualBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double AbsErrorSum, double AbsActualSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private const int StackAllocThreshold = 256;

    public Wmape(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _absErrorBuffer = new RingBuffer(period);
        _absActualBuffer = new RingBuffer(period);
        Name = $"Wmape({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _absErrorBuffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Snapshot BEFORE any mutations for correct rollback
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        double absError = Math.Abs(actualVal - predictedVal);
        double absActual = Math.Abs(actualVal);

        if (isNew)
        {
            double removedError = _absErrorBuffer.Count == _absErrorBuffer.Capacity ? _absErrorBuffer.Oldest : 0.0;
            _state.AbsErrorSum = _state.AbsErrorSum - removedError + absError;
            _absErrorBuffer.Add(absError);

            double removedActual = _absActualBuffer.Count == _absActualBuffer.Capacity ? _absActualBuffer.Oldest : 0.0;
            _state.AbsActualSum = _state.AbsActualSum - removedActual + absActual;
            _absActualBuffer.Add(absActual);

            _state.TickCount++;
            if (_absErrorBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.AbsErrorSum = _absErrorBuffer.RecalculateSum();
                _state.AbsActualSum = _absActualBuffer.RecalculateSum();
            }
        }
        else
        {
            // Bar correction: update buffer and recalculate sums
            // Note: _p_state was saved BEFORE the Add, but buffer still has the added value
            // So we update newest and recalculate to ensure consistency
            _absErrorBuffer.UpdateNewest(absError);
            _absActualBuffer.UpdateNewest(absActual);

            _state.AbsErrorSum = _absErrorBuffer.RecalculateSum();
            _state.AbsActualSum = _absActualBuffer.RecalculateSum();
        }

        // WMAPE = (Σ|error| / Σ|actual|) * 100
        double result = _state.AbsActualSum > 1e-10 ? (_state.AbsErrorSum / _state.AbsActualSum) * 100.0 : 0.0;

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
        throw new NotSupportedException("WMAPE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("WMAPE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("WMAPE requires two inputs.");
    }

    public override void Reset()
    {
        _absErrorBuffer.Clear();
        _absActualBuffer.Clear();
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

        // Use stackalloc for small periods, ArrayPool for larger
        scoped Span<double> absErrorBuffer;
        scoped Span<double> absActualBuffer;
        double[]? rentedError = null;
        double[]? rentedActual = null;

        if (period <= StackAllocThreshold)
        {
            absErrorBuffer = stackalloc double[period];
            absActualBuffer = stackalloc double[period];
        }
        else
        {
            rentedError = ArrayPool<double>.Shared.Rent(period);
            rentedActual = ArrayPool<double>.Shared.Rent(period);
            absErrorBuffer = rentedError.AsSpan(0, period);
            absActualBuffer = rentedActual.AsSpan(0, period);
        }

        try
        {
            double absErrorSum = 0;
            double absActualSum = 0;
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

                double absError = Math.Abs(act - pred);
                double absActual = Math.Abs(act);

                absErrorSum += absError;
                absActualSum += absActual;
                absErrorBuffer[i] = absError;
                absActualBuffer[i] = absActual;

                output[i] = absActualSum > 1e-10 ? (absErrorSum / absActualSum) * 100.0 : 0.0;
            }

            int tickCount = 0;
            for (; i < len; i++)
            {
                double act = actual[i];
                double pred = predicted[i];

                if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
                if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

                double absError = Math.Abs(act - pred);
                double absActual = Math.Abs(act);

                absErrorSum = absErrorSum - absErrorBuffer[bufferIndex] + absError;
                absActualSum = absActualSum - absActualBuffer[bufferIndex] + absActual;
                absErrorBuffer[bufferIndex] = absError;
                absActualBuffer[bufferIndex] = absActual;

                bufferIndex++;
                if (bufferIndex >= period) bufferIndex = 0;

                output[i] = absActualSum > 1e-10 ? (absErrorSum / absActualSum) * 100.0 : 0.0;

                tickCount++;
                if (tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    double recalcError = 0, recalcActual = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalcError += absErrorBuffer[k];
                        recalcActual += absActualBuffer[k];
                    }
                    absErrorSum = recalcError;
                    absActualSum = recalcActual;
                }
            }
        }
        finally
        {
            if (rentedError != null)
                ArrayPool<double>.Shared.Return(rentedError);
            if (rentedActual != null)
                ArrayPool<double>.Shared.Return(rentedActual);
        }
    }
}