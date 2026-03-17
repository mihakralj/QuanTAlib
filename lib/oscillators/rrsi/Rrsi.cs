using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RRSI: Rocket RSI (Ehlers)
/// </summary>
/// <remarks>
/// Combines a Super Smoother–filtered RSI with the Fisher Transform
/// to produce a zero-mean Gaussian-distributed oscillator with sharp
/// turning-point signals.
///
/// Pipeline:
/// <list type="number">
///   <item>Half-cycle momentum: <c>Mom = Close − Close[rsiLength−1]</c></item>
///   <item>Super Smoother (2-pole Butterworth IIR) on <c>(Mom + Mom[1])/2</c></item>
///   <item>Ehlers RSI: <c>RSI = (CU − CD)/(CU + CD)</c> over <c>rsiLength</c>
///         bars of filtered momentum differences (result already in [−1, 1])</item>
///   <item>Fisher Transform: <c>RocketRSI = arctanh(clamp(RSI, ±0.999))</c></item>
/// </list>
///
/// Reference: John F. Ehlers, "Rocket RSI", TASC May 2018.
/// </remarks>
[SkipLocalsInit]
public sealed class Rrsi : AbstractBase
{
    private readonly int _smoothLength;
    private readonly int _rsiLength;

    // Super Smoother coefficients (computed once)
    private readonly double _c1, _c2, _c3;

    // Close history for momentum lookback
    private readonly RingBuffer _closeBuf;

    // Filter history for RSI accumulation
    private readonly RingBuffer _filtBuf;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Mom,
        double MomPrev,
        double Filt,
        double FiltPrev,
        double LastValid,
        int Count);

    private State _s;
    private State _ps;

    /// <inheritdoc />
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>Smooth filter length.</summary>
    public int SmoothLength => _smoothLength;

    /// <summary>RSI accumulation length.</summary>
    public int RsiLength => _rsiLength;

    /// <summary>
    /// Creates a Rocket RSI indicator.
    /// </summary>
    /// <param name="smoothLength">Super Smoother period (must be &gt; 0, default 10).</param>
    /// <param name="rsiLength">RSI accumulation period (must be &gt; 0, default 10).</param>
    public Rrsi(int smoothLength = 10, int rsiLength = 10)
    {
        if (smoothLength <= 0)
        {
            throw new ArgumentException("Smooth length must be greater than 0", nameof(smoothLength));
        }
        if (rsiLength <= 0)
        {
            throw new ArgumentException("RSI length must be greater than 0", nameof(rsiLength));
        }

        _smoothLength = smoothLength;
        _rsiLength = rsiLength;

        // Super Smoother coefficients (Ehlers 2-pole Butterworth)
        double a1 = Math.Exp(-1.414 * Math.PI / smoothLength);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / smoothLength);
        _c2 = b1;
        _c3 = -(a1 * a1);
        _c1 = 1.0 - _c2 - _c3;

        _closeBuf = new RingBuffer(rsiLength);
        _filtBuf = new RingBuffer(rsiLength + 1);

        Name = $"Rrsi({smoothLength},{rsiLength})";
        WarmupPeriod = smoothLength + rsiLength;
    }

    /// <summary>
    /// Creates a Rocket RSI with a source publisher.
    /// </summary>
    public Rrsi(ITValuePublisher source, int smoothLength = 10, int rsiLength = 10) : this(smoothLength, rsiLength)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize NaN/Inf
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_s.LastValid) ? _s.LastValid : 0.0;
        }
        else
        {
            _s.LastValid = value;
        }

        if (isNew)
        {
            _ps = _s;
            _closeBuf.Add(value);
            _s.Count++;
        }
        else
        {
            _s = _ps;
            _closeBuf.UpdateNewest(value);
        }

        // Step 1: Half-cycle momentum
        double mom;
        if (_closeBuf.Count >= _rsiLength)
        {
            // Close - Close[rsiLength - 1]
            // _closeBuf[0] is oldest, _closeBuf[Count-1] is newest
            // Close[rsiLength-1] ago = _closeBuf[_closeBuf.Count - _rsiLength]
            mom = value - _closeBuf[_closeBuf.Count - _rsiLength];
        }
        else
        {
            mom = 0.0;
        }

        // Step 2: Super Smoother Filter on (Mom + MomPrev) / 2
        double filt;
        if (_s.Count <= 2)
        {
            // Not enough history for IIR — pass through
            filt = mom;
        }
        else
        {
            filt = (_c1 * (mom + _s.Mom) * 0.5) + (_c2 * _s.Filt) + (_c3 * _s.FiltPrev);
        }

        // Update state for next bar
        _s.FiltPrev = _s.Filt;
        _s.Filt = filt;
        _s.MomPrev = _s.Mom;
        _s.Mom = mom;

        // Step 3: Store Filt for RSI accumulation
        if (isNew)
        {
            _filtBuf.Add(filt);
        }
        else
        {
            _filtBuf.UpdateNewest(filt);
        }

        // Step 4: Ehlers RSI — accumulate CU/CD over rsiLength Filt differences
        double cu = 0.0;
        double cd = 0.0;
        int filtCount = _filtBuf.Count;
        int lookback = Math.Min(_rsiLength, filtCount - 1);

        for (int i = 0; i < lookback; i++)
        {
            // Filt[i] and Filt[i+1] in Ehlers notation (0 = newest)
            // In our buffer: newest = filtCount-1, so Filt[i] = _filtBuf[filtCount - 1 - i]
            double filtNewer = _filtBuf[filtCount - 1 - i];
            double filtOlder = _filtBuf[filtCount - 2 - i];
            double diff = filtNewer - filtOlder;

            if (diff > 0.0)
            {
                cu += diff;
            }
            else if (diff < 0.0)
            {
                cd -= diff; // accumulate absolute value
            }
        }

        // Step 5: Compute RSI in [-1, 1] range
        double myRsi;
        double cuCd = cu + cd;
        if (cuCd > 1e-10)
        {
            myRsi = (cu - cd) / cuCd;
        }
        else
        {
            myRsi = 0.0;
        }

        // Clamp to avoid arctanh singularity
        if (myRsi > 0.999)
        {
            myRsi = 0.999;
        }
        else if (myRsi < -0.999)
        {
            myRsi = -0.999;
        }

        // Step 6: Fisher Transform (arctanh)
        double rocketRsi = 0.5 * Math.Log((1.0 + myRsi) / (1.0 - myRsi));

        Last = new TValue(input.Time, rocketRsi);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return [];
        }

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _smoothLength, _rsiLength);
        source.Times.CopyTo(tSpan);

        // Replay for streaming state sync
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>Batch-process a series.</summary>
    public static TSeries Batch(TSeries source, int smoothLength = 10, int rsiLength = 10)
    {
        var ind = new Rrsi(smoothLength, rsiLength);
        return ind.Update(source);
    }

    /// <summary>Batch-process span data.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int smoothLength = 10, int rsiLength = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (smoothLength <= 0)
        {
            throw new ArgumentException("Smooth length must be greater than 0", nameof(smoothLength));
        }
        if (rsiLength <= 0)
        {
            throw new ArgumentException("RSI length must be greater than 0", nameof(rsiLength));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Super Smoother coefficients
        double a1 = Math.Exp(-1.414 * Math.PI / smoothLength);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / smoothLength);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        // Allocate momentum and filter arrays
        double[] momRented = ArrayPool<double>.Shared.Rent(len);
        double[] filtRented = ArrayPool<double>.Shared.Rent(len);
        Span<double> momArr = momRented.AsSpan(0, len);
        Span<double> filtArr = filtRented.AsSpan(0, len);

        try
        {
            // Pass 1: Momentum
            for (int i = 0; i < len; i++)
            {
                momArr[i] = (i >= rsiLength - 1)
                    ? source[i] - source[i - rsiLength + 1]
                    : 0.0;
            }

            // Pass 2: Super Smoother
            filtArr[0] = momArr[0];
            if (len > 1)
            {
                filtArr[1] = (c1 * (momArr[1] + momArr[0]) * 0.5) + (c2 * filtArr[0]);
            }
            for (int i = 2; i < len; i++)
            {
                filtArr[i] = (c1 * (momArr[i] + momArr[i - 1]) * 0.5)
                    + (c2 * filtArr[i - 1])
                    + (c3 * filtArr[i - 2]);
            }

            // Pass 3: RSI + Fisher
            for (int i = 0; i < len; i++)
            {
                double cu = 0.0;
                double cd = 0.0;
                int lookback = Math.Min(rsiLength, i);

                for (int j = 0; j < lookback; j++)
                {
                    double diff = filtArr[i - j] - filtArr[i - j - 1];
                    if (diff > 0.0)
                    {
                        cu += diff;
                    }
                    else if (diff < 0.0)
                    {
                        cd -= diff;
                    }
                }

                double cuCd = cu + cd;
                double myRsi = (cuCd > 1e-10) ? (cu - cd) / cuCd : 0.0;

                // Clamp
                if (myRsi > 0.999)
                {
                    myRsi = 0.999;
                }
                else if (myRsi < -0.999)
                {
                    myRsi = -0.999;
                }

                output[i] = 0.5 * Math.Log((1.0 + myRsi) / (1.0 - myRsi));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(momRented);
            ArrayPool<double>.Shared.Return(filtRented);
        }
    }

    /// <summary>Calculate and return both results and indicator.</summary>
    public static (TSeries Results, Rrsi Indicator) Calculate(TSeries source,
        int smoothLength = 10, int rsiLength = 10)
    {
        var ind = new Rrsi(smoothLength, rsiLength);
        return (ind.Update(source), ind);
    }

    public override void Reset()
    {
        _closeBuf.Clear();
        _filtBuf.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }
}
