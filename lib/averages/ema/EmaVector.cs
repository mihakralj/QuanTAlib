using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Multi-Alpha Exponential Moving Average (EMA) - SIMD optimized.
/// Calculates multiple EMAs with different periods/alphas for the same input series in parallel.
/// Uses last-value substitution for invalid inputs (NaN/Infinity).
/// </summary>
[SkipLocalsInit]
public class EmaVector
{
    private readonly double[] _alphas;
    private readonly double[] _emas;
    private readonly double[] _Es;
    private readonly double[] _p_emas;
    private readonly double[] _p_Es;
    private readonly int _count;
    private double _lastValidValue;

    /// <summary>
    /// Current EMA values for all periods.
    /// </summary>
    public TValue[] Values { get; private set; }

    /// <summary>
    /// Initializes EmaVector with specified periods.
    /// </summary>
    /// <param name="periods">Array of periods</param>
    public EmaVector(int[] periods)
    {
        _count = periods.Length;
        _alphas = new double[_count];
        _emas = new double[_count];
        _Es = new double[_count];
        _p_emas = new double[_count];
        _p_Es = new double[_count];
        Values = new TValue[_count];

        for (int i = 0; i < _count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(periods[i], 0);
            _alphas[i] = 2.0 / (periods[i] + 1);
            ResetAt(i);
        }
    }

    /// <summary>
    /// Initializes EmaVector with specified alphas.
    /// </summary>
    /// <param name="alphas">Array of alphas</param>
    public EmaVector(double[] alphas)
    {
        _count = alphas.Length;
        _alphas = new double[_count];
        _emas = new double[_count];
        _Es = new double[_count];
        _p_emas = new double[_count];
        _p_Es = new double[_count];
        Values = new TValue[_count];

        for (int i = 0; i < _count; i++)
        {
            if (alphas[i] <= 0 || alphas[i] > 1)
                throw new ArgumentOutOfRangeException(nameof(alphas), alphas[i], "Alpha must be between 0 (exclusive) and 1 (inclusive)");
            _alphas[i] = alphas[i];
            ResetAt(i);
        }
    }

    private void ResetAt(int index)
    {
        _emas[index] = 0.0;
        _Es[index] = 1.0;
    }

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    /// <summary>
    /// Resets all EMA states.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _count; i++)
        {
            ResetAt(i);
        }
        _lastValidValue = 0;
        Array.Clear(Values);
    }

    /// <summary>
    /// Updates EMAs with the given value.
    /// Uses last-value substitution: invalid inputs (NaN/Infinity) are replaced with
    /// the last known good value, providing continuity in the output series.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Array of compensated EMA values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue[] Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            Array.Copy(_emas, _p_emas, _count);
            Array.Copy(_Es, _p_Es, _count);
        }
        else
        {
            Array.Copy(_p_emas, _emas, _count);
            Array.Copy(_p_Es, _Es, _count);
        }

        // Last-value substitution: replace non-finite inputs with last valid value
        double val = GetValidValue(input.Value);

        // SIMD Loop
        int vecCount = Vector<double>.Count;
        int i = 0;

        if (Vector.IsHardwareAccelerated && _count >= vecCount)
        {
            var vecInput = new Vector<double>(val);
            var vecOne = Vector<double>.One;
            var vecEpsilon = new Vector<double>(1e-10);

            for (; i <= _count - vecCount; i += vecCount)
            {
                // Load state
                var vecAlpha = new Vector<double>(_alphas, i);
                var vecEma = new Vector<double>(_emas, i);
                var vecE = new Vector<double>(_Es, i);

                // Update EMA
                // ema += alpha * (input - ema)
                vecEma += vecAlpha * (vecInput - vecEma);

                // Update E (warmup factor)
                // E *= (1 - alpha)
                vecE *= (vecOne - vecAlpha);

                // Calculate compensated result
                // res = ema / (1 - E)
                var vecCompensated = vecEma / (vecOne - vecE);

                // Check warmup condition: E <= 1e-10 means "hot" (use raw EMA)
                // Vector.LessThanOrEqual returns Vector<long> with all-1s for true, all-0s for false
                // We reinterpret as Vector<double> for use with ConditionalSelect
                var isHotMask = Vector.LessThanOrEqual(vecE, vecEpsilon);

                // Select result: if hot (E <= epsilon), use raw EMA; otherwise use compensated
                // ConditionalSelect: mask=true -> first arg, mask=false -> second arg
                var vecResult = Vector.ConditionalSelect(
                    Vector.AsVectorDouble(isHotMask),
                    vecEma,          // Hot: use raw EMA
                    vecCompensated   // Cold: use compensated
                );

                // Store state
                vecEma.CopyTo(_emas, i);
                vecE.CopyTo(_Es, i);

                // Store result
                for (int j = 0; j < vecCount; j++)
                {
                    Values[i + j] = new TValue(input.Time, vecResult[j]);
                }
            }
        }

        // Scalar fallback for remaining items
        for (; i < _count; i++)
        {
            double alpha = _alphas[i];
            _emas[i] += alpha * (val - _emas[i]);

            double result = _emas[i];
            if (_Es[i] > 1e-10)
            {
                _Es[i] *= (1.0 - alpha);
                if (_Es[i] > 1e-10)
                {
                    result = _emas[i] / (1.0 - _Es[i]);
                }
            }

            Values[i] = new TValue(input.Time, result);
        }

        return Values;
    }

    /// <summary>
    /// Calculates EMAs for the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Array of EMA series</returns>
    public TSeries[] Calculate(TSeries source)
    {
        int len = source.Count;
        var resultSeries = new TSeries[_count];

        // Pre-allocate lists
        var tLists = new List<long>[_count];
        var vLists = new List<double>[_count];

        for (int i = 0; i < _count; i++)
        {
            tLists[i] = new List<long>(len);
            vLists[i] = new List<double>(len);
            CollectionsMarshal.SetCount(tLists[i], len);
            CollectionsMarshal.SetCount(vLists[i], len);
        }

        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        int vecCount = Vector<double>.Count;
        var vecOne = Vector<double>.One;
        var vecEpsilon = new Vector<double>(1e-10);

        for (int t = 0; t < len; t++)
        {
            double val = sourceValues[t];
            long time = sourceTimes[t];

            // Last-value substitution: replace non-finite inputs with last valid value
            val = GetValidValue(val);

            var vecInput = new Vector<double>(val);

            int i = 0;
            if (Vector.IsHardwareAccelerated && _count >= vecCount)
            {
                for (; i <= _count - vecCount; i += vecCount)
                {
                    var vecAlpha = new Vector<double>(_alphas, i);
                    var vecEma = new Vector<double>(_emas, i);
                    var vecE = new Vector<double>(_Es, i);

                    vecEma += vecAlpha * (vecInput - vecEma);
                    vecE *= (vecOne - vecAlpha);

                    var vecCompensated = vecEma / (vecOne - vecE);

                    // Check warmup condition: E <= 1e-10 means "hot" (use raw EMA)
                    var isHotMask = Vector.LessThanOrEqual(vecE, vecEpsilon);

                    // Select result: if hot, use raw EMA; otherwise use compensated
                    var vecResult = Vector.ConditionalSelect(
                        Vector.AsVectorDouble(isHotMask),
                        vecEma,          // Hot: use raw EMA
                        vecCompensated   // Cold: use compensated
                    );

                    vecEma.CopyTo(_emas, i);
                    vecE.CopyTo(_Es, i);

                    // Scatter results to lists
                    for (int j = 0; j < vecCount; j++)
                    {
                        CollectionsMarshal.AsSpan(tLists[i + j])[t] = time;
                        CollectionsMarshal.AsSpan(vLists[i + j])[t] = vecResult[j];
                    }
                }
            }

            for (; i < _count; i++)
            {
                double alpha = _alphas[i];
                _emas[i] += alpha * (val - _emas[i]);

                double result = _emas[i];
                if (_Es[i] > 1e-10)
                {
                    _Es[i] *= (1.0 - alpha);
                    if (_Es[i] > 1e-10)
                    {
                        result = _emas[i] / (1.0 - _Es[i]);
                    }
                }

                CollectionsMarshal.AsSpan(tLists[i])[t] = time;
                CollectionsMarshal.AsSpan(vLists[i])[t] = result;
            }
        }

        // Create TSeries and update Values
        for (int i = 0; i < _count; i++)
        {
            resultSeries[i] = new TSeries(tLists[i], vLists[i]);
            var lastT = CollectionsMarshal.AsSpan(tLists[i])[len - 1];
            var lastV = CollectionsMarshal.AsSpan(vLists[i])[len - 1];
            Values[i] = new TValue(lastT, lastV);
        }

        return resultSeries;
    }

    /// <summary>
    /// Calculates EMAs for the entire series using specified periods.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="periods">Array of periods</param>
    /// <returns>Array of EMA series</returns>
    public static TSeries[] Calculate(TSeries source, int[] periods)
    {
        var emaVector = new EmaVector(periods);
        return emaVector.Calculate(source);
    }
}
