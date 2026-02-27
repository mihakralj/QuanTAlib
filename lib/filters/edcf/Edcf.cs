using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EDCF: Ehlers Distance Coefficient Filter
/// A nonlinear adaptive FIR filter where coefficients are computed as the sum of
/// squared price differences across the observation window. When prices are flat,
/// all coefficients are equal (degenerates to SMA). When prices shift rapidly,
/// higher weights are assigned to samples with greater price movement.
/// </summary>
/// <remarks>
/// Reference: John F. Ehlers, "Ehlers Filters" (MESA Software)
///            John F. Ehlers, "Nonlinear Ehlers Filters" (S&amp;C V.19:4, pp.25-34)
///
/// Algorithm:
///   For each sample position i in [0, Length-1]:
///     Distance2[i] = Σ (Price[i] - Price[i + k])² for k = 1 to Length-1
///   Coef[i] = Distance2[i]
///   Filter = Σ(Coef[i] × Price[i]) / Σ(Coef[i])
///
/// Complexity: O(n²) per update where n = Length (nested loop over window)
/// </remarks>
[SkipLocalsInit]
public sealed class Edcf : AbstractBase
{
    private readonly int _length;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid, int Count);
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes an Ehlers Distance Coefficient Filter.
    /// </summary>
    /// <param name="length">Filter window length (≥ 2). Default: 15.</param>
    public Edcf(int length = 15)
    {
        if (length < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than or equal to 2.");
        }

        _length = length;
        // Need length samples for the window + (length-1) lookback = 2*length - 1 total
        // But the inner loop looks back within the same window, so we only need 'length' samples
        // However, the EasyLanguage code accesses Price[count + LookBack] where count goes to Length-1
        // and LookBack goes to Length-1, so max index = 2*(Length-1). We need 2*Length - 1 in the buffer.
        _buffer = new RingBuffer(2 * length - 1);
        WarmupPeriod = length;
        Name = $"Edcf({_length})";
    }

    /// <summary>
    /// Initializes an EDCF indicator and subscribes it to a source publisher.
    /// </summary>
    /// <param name="source">Input data source for event-based chaining.</param>
    /// <param name="length">Filter window length (≥ 2). Default: 15.</param>
    public Edcf(ITValuePublisher source, int length = 15) : this(length)
    {
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // State management: save/restore for bar correction
        // skipcq: CS-R1140 - EDCF reads individual buffer positions so cannot use
        // Snapshot/Restore (which only saves one slot); UpdateNewest replaces in-place
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // NaN/Infinity guard: substitute last-valid
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = value;
        }

        if (isNew)
        {
            _buffer.Add(value);
            s.Count++;
        }
        else
        {
            _buffer.UpdateNewest(value);
        }

        double result;
        int available = Math.Min(s.Count, _length);

        if (available < 2)
        {
            result = value;
        }
        else
        {
            result = CalcDistanceFilter(available);
        }

        _s = s;

        var ret = new TValue(input.Time, result);
        Last = ret;
        PubEvent(ret, isNew);
        return ret;
    }

    public override TSeries Update(TSeries source)
    {
        TSeries result = [];
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(Update(source[i]));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalcDistanceFilter(int windowLen)
    {
        // Ehlers Distance Coefficient Filter
        // For each sample position i in [0, windowLen-1]:
        //   Distance2[i] = Σ(Price[i] - Price[i + k])² for k = 1 to windowLen-1
        //   Coef[i] = Distance2[i]
        // Filter = Σ(Coef[i] * Price[i]) / Σ(Coef[i])
        //
        // Buffer indexing: _buffer[^1] = newest (Price[0] in EasyLanguage)
        //                  _buffer[^2] = Price[1], etc.

        double sumCoef = 0.0;
        double num = 0.0;

        int bufCount = _buffer.Count;

        for (int i = 0; i < windowLen; i++)
        {
            double dist2 = 0.0;
            double priceI = _buffer[bufCount - 1 - i];

            for (int k = 1; k < windowLen; k++)
            {
                int idx = i + k;
                if (idx >= bufCount)
                {
                    break;
                }
                double priceK = _buffer[bufCount - 1 - idx];
                double diff = priceI - priceK;
                dist2 += diff * diff;
            }

            sumCoef += dist2;
            num += dist2 * priceI;
        }

        return sumCoef > 1e-10 ? num / sumCoef : _buffer[bufCount - 1];
    }

    public static TSeries Batch(TSeries source, int length = 15)
    {
        var indicator = new Edcf(length);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int length = 15)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination span is shorter than source span.", nameof(destination));
        }
        if (length < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than or equal to 2.");
        }

        var filter = new Edcf(length);
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = filter.Update(new TValue(0, source[i])).Value;
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _ps = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        long initialTicks = DateTime.UtcNow.Ticks - source.Length * (step?.Ticks ?? TimeSpan.FromSeconds(1).Ticks);
        TimeSpan increment = step ?? TimeSpan.FromSeconds(1);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(initialTicks + i * increment.Ticks, source[i]));
        }
    }

    public static (TSeries Results, Edcf Indicator) Calculate(TSeries source, int length = 15)
    {
        var indicator = new Edcf(length);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Clear();
        }
        base.Dispose(disposing);
    }
}
