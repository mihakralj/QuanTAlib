using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace QuanTAlib;

/// <summary>
/// HTIT: Ehlers Hilbert Transform Instantaneous Trend
/// A trend-following indicator that uses the Hilbert Transform to measure the dominant cycle period
/// and compute an instantaneous trendline. It adapts to market cycles to reduce lag while maintaining smoothness.
/// </summary>
/// <remarks>
/// Sources:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/trends_IIR/htit.md
/// https://dotnet.stockindicators.dev/indicators/HtTrendline/
/// </remarks>
[SkipLocalsInit]
public sealed class Htit : AbstractBase
{
    private readonly RingBuffer _priceBuffer;
    private readonly RingBuffer _smoothBuffer;
    private readonly RingBuffer _detrenderBuffer;
    private readonly RingBuffer _i1Buffer;
    private readonly RingBuffer _q1Buffer;
    private readonly RingBuffer _periodBuffer;
    private readonly RingBuffer _smoothPeriodBuffer;
    private readonly RingBuffer _itBuffer;

    private record struct State(double I2, double Q2, double Re, double Im, double LastValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _priceBuffer.Count >= WarmupPeriod;

    public Htit()
    {
        Name = "Htit";
        WarmupPeriod = 12; // Based on logic: _priceBuffer.Count >= 12
        _priceBuffer = new RingBuffer(50);
        _smoothBuffer = new RingBuffer(7);
        _detrenderBuffer = new RingBuffer(7);
        _i1Buffer = new RingBuffer(7);
        _q1Buffer = new RingBuffer(7);
        _periodBuffer = new RingBuffer(2);
        _smoothPeriodBuffer = new RingBuffer(2);
        _itBuffer = new RingBuffer(4);
        Init();
    }

    public Htit(ITValuePublisher source) : this()
    {
        source.Pub += (item) => Update(item);
    }

    private void Init()
    {
        _priceBuffer.Clear();
        _smoothBuffer.Clear();
        _detrenderBuffer.Clear();
        _i1Buffer.Clear();
        _q1Buffer.Clear();
        _periodBuffer.Clear();
        _smoothPeriodBuffer.Clear();
        _itBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        ManageState(isNew);
        double price = ValidateInput(input.Value);
        UpdateBuffer(_priceBuffer, price, isNew);

        if (_priceBuffer.Count < 7)
            return ProcessWarmup(input, price, isNew);

        // 1. Smooth Price
        double smooth = (4 * _priceBuffer[^1] + 3 * _priceBuffer[^2] + 2 * _priceBuffer[^3] + _priceBuffer[^4]) / 10.0;
        UpdateBuffer(_smoothBuffer, smooth, isNew);

        // 2. Detrender
        double prevPeriod = _periodBuffer[isNew ? ^1 : ^2];
        double adj = (0.075 * prevPeriod) + 0.54;
        double detrender = (0.0962 * _smoothBuffer[^1] + 0.5769 * _smoothBuffer[^3] - 0.5769 * _smoothBuffer[^5] - 0.0962 * _smoothBuffer[^7]) * adj;
        UpdateBuffer(_detrenderBuffer, detrender, isNew);

        // 3. In-Phase and Quadrature
        double q1 = (0.0962 * _detrenderBuffer[^1] + 0.5769 * _detrenderBuffer[^3] - 0.5769 * _detrenderBuffer[^5] - 0.0962 * _detrenderBuffer[^7]) * adj;
        double i1 = _detrenderBuffer[^4];
        UpdateBuffer(_q1Buffer, q1, isNew);
        UpdateBuffer(_i1Buffer, i1, isNew);

        // 4. Advance phases by 90 degrees
        double jI = (0.0962 * _i1Buffer[^1] + 0.5769 * _i1Buffer[^3] - 0.5769 * _i1Buffer[^5] - 0.0962 * _i1Buffer[^7]) * adj;
        double jQ = (0.0962 * _q1Buffer[^1] + 0.5769 * _q1Buffer[^3] - 0.5769 * _q1Buffer[^5] - 0.0962 * _q1Buffer[^7]) * adj;

        // 5. Phasor addition & 6. Homodyne Discriminator
        ProcessPhasorAndHomodyne(i1, q1, jI, jQ);

        // 7. Calculate Period
        double period = CalculatePeriod(prevPeriod);
        UpdateBuffer(_periodBuffer, period, isNew);

        // Smooth dominant cycle period
        double prevSmoothPeriod = _smoothPeriodBuffer[isNew ? ^1 : ^2];
        double smoothPeriod = (0.33 * period) + (0.67 * prevSmoothPeriod);
        UpdateBuffer(_smoothPeriodBuffer, smoothPeriod, isNew);

        // 8. Instantaneous Trend
        double it = CalculateInstantaneousTrend(smoothPeriod, price);
        UpdateBuffer(_itBuffer, it, isNew);

        // 9. Final Trendline
        double trendline = _priceBuffer.Count >= 12
            ? (4 * _itBuffer[^1] + 3 * _itBuffer[^2] + 2 * _itBuffer[^3] + _itBuffer[^4]) / 10.0
            : price;

        Last = new TValue(input.Time, trendline);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying last 50 bars
        Init();
        int startIndex = Math.Max(0, len - 50);
        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManageState(bool isNew)
    {
        if (isNew) _p_state = _state;
        else _state = _p_state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ValidateInput(double value)
    {
        double price = double.IsFinite(value) ? value : _state.LastValidValue;
        _state.LastValidValue = price;
        return price;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateBuffer(RingBuffer buffer, double val, bool isNew)
    {
        if (isNew) buffer.Add(val);
        else buffer.UpdateNewest(val);
    }

    private TValue ProcessWarmup(TValue input, double price, bool isNew)
    {
        UpdateBuffer(_smoothBuffer, price, isNew);
        UpdateBuffer(_detrenderBuffer, 0, isNew);
        UpdateBuffer(_i1Buffer, 0, isNew);
        UpdateBuffer(_q1Buffer, 0, isNew);
        UpdateBuffer(_periodBuffer, 0, isNew);
        UpdateBuffer(_smoothPeriodBuffer, 0, isNew);
        UpdateBuffer(_itBuffer, price, isNew);

        Last = new TValue(input.Time, price);
        PubEvent(Last);
        return Last;
    }

    private void ProcessPhasorAndHomodyne(double i1, double q1, double jI, double jQ)
    {
        // 5. Phasor addition
        double i2_raw = i1 - jQ;
        double q2_raw = q1 + jI;

        // Smoothing
        _state.I2 = (0.2 * i2_raw) + (0.8 * _p_state.I2);
        _state.Q2 = (0.2 * q2_raw) + (0.8 * _p_state.Q2);

        // 6. Homodyne Discriminator
        double re_raw = (_state.I2 * _p_state.I2) + (_state.Q2 * _p_state.Q2);
        double im_raw = (_state.I2 * _p_state.Q2) - (_state.Q2 * _p_state.I2);

        // Smoothing
        _state.Re = (0.2 * re_raw) + (0.8 * _p_state.Re);
        _state.Im = (0.2 * im_raw) + (0.8 * _p_state.Im);
    }

    private double CalculatePeriod(double prevPeriod)
    {
        double period = 0;
        if (Math.Abs(_state.Im) > 1e-9 && Math.Abs(_state.Re) > 1e-9)
        {
            period = 2 * Math.PI / Math.Atan(_state.Im / _state.Re);
        }

        // Adjust period to thresholds
        if (prevPeriod > 0)
        {
            if (period > 1.5 * prevPeriod) period = 1.5 * prevPeriod;
            if (period < 0.67 * prevPeriod) period = 0.67 * prevPeriod;
        }
        if (period < 6) period = 6;
        if (period > 50) period = 50;

        // Smooth the period
        return (0.2 * period) + (0.8 * prevPeriod);
    }

    private double CalculateInstantaneousTrend(double smoothPeriod, double price)
    {
        int dcPeriods = (int)(double.IsNaN(smoothPeriod) ? 0 : smoothPeriod + 0.5);
        double sumPr = 0;
        int count = 0;

        // Sum price over dcPeriods
        for (int d = 0; d < dcPeriods; d++)
        {
            if (d < _priceBuffer.Count)
            {
                sumPr += _priceBuffer[^(d + 1)];
                count++;
            }
        }

        return count > 0 ? sumPr / count : price;
    }

    public static TSeries Batch(TSeries source)
    {
        var htit = new Htit();
        return htit.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        int len = source.Length;
        if (len == 0) return;

        // Buffers
        Span<double> priceBuffer = stackalloc double[50];
        Span<double> smoothBuffer = stackalloc double[7];
        Span<double> detrenderBuffer = stackalloc double[7];
        Span<double> i1Buffer = stackalloc double[7];
        Span<double> q1Buffer = stackalloc double[7];
        Span<double> periodBuffer = stackalloc double[2];
        Span<double> smoothPeriodBuffer = stackalloc double[2];
        Span<double> itBuffer = stackalloc double[4];

        int pIdx = 0, sIdx = 0, dIdx = 0, i1Idx = 0, q1Idx = 0, pdIdx = 0, sdIdx = 0, itIdx = 0;
        int pCount = 0;

        double i2 = 0, q2 = 0, re = 0, im = 0;
        double p_i2 = 0, p_q2 = 0, p_re = 0, p_im = 0;
        double lastValid = 0;

        for (int i = 0; i < len; i++)
        {
            double price = source[i];
            if (double.IsFinite(price)) lastValid = price; else price = lastValid;

            // Add to price buffer
            priceBuffer[pIdx] = price;
            pCount++;

            if (pCount < 7)
            {
                smoothBuffer[sIdx] = price;
                detrenderBuffer[dIdx] = 0;
                i1Buffer[i1Idx] = 0;
                q1Buffer[q1Idx] = 0;
                periodBuffer[pdIdx] = 0;
                smoothPeriodBuffer[sdIdx] = 0;
                itBuffer[itIdx] = price;
                output[i] = price;
            }
            else
            {
                // 1. Smooth Price
                double p0 = priceBuffer[pIdx];
                double p1 = priceBuffer[(pIdx - 1 + 50) % 50];
                double p2 = priceBuffer[(pIdx - 2 + 50) % 50];
                double p3 = priceBuffer[(pIdx - 3 + 50) % 50];
                double smooth = (4 * p0 + 3 * p1 + 2 * p2 + p3) / 10.0;
                smoothBuffer[sIdx] = smooth;

                // 2. Detrender
                double prevPeriod = periodBuffer[(pdIdx - 1 + 2) % 2];
                double adj = (0.075 * prevPeriod) + 0.54;

                double s0 = smoothBuffer[sIdx];
                double s2 = smoothBuffer[(sIdx - 2 + 7) % 7];
                double s4 = smoothBuffer[(sIdx - 4 + 7) % 7];
                double s6 = smoothBuffer[(sIdx - 6 + 7) % 7];

                double detrender = (0.0962 * s0 + 0.5769 * s2 - 0.5769 * s4 - 0.0962 * s6) * adj;
                detrenderBuffer[dIdx] = detrender;

                // 3. In-Phase and Quadrature
                double d0 = detrenderBuffer[dIdx];
                double d2 = detrenderBuffer[(dIdx - 2 + 7) % 7];
                double d4 = detrenderBuffer[(dIdx - 4 + 7) % 7];
                double d6 = detrenderBuffer[(dIdx - 6 + 7) % 7];

                double q1 = (0.0962 * d0 + 0.5769 * d2 - 0.5769 * d4 - 0.0962 * d6) * adj;
                double i1 = detrenderBuffer[(dIdx - 3 + 7) % 7];

                q1Buffer[q1Idx] = q1;
                i1Buffer[i1Idx] = i1;

                // 4. Advance phases
                double i1_0 = i1Buffer[i1Idx];
                double i1_2 = i1Buffer[(i1Idx - 2 + 7) % 7];
                double i1_4 = i1Buffer[(i1Idx - 4 + 7) % 7];
                double i1_6 = i1Buffer[(i1Idx - 6 + 7) % 7];
                double jI = (0.0962 * i1_0 + 0.5769 * i1_2 - 0.5769 * i1_4 - 0.0962 * i1_6) * adj;

                double q1_0 = q1Buffer[q1Idx];
                double q1_2 = q1Buffer[(q1Idx - 2 + 7) % 7];
                double q1_4 = q1Buffer[(q1Idx - 4 + 7) % 7];
                double q1_6 = q1Buffer[(q1Idx - 6 + 7) % 7];
                double jQ = (0.0962 * q1_0 + 0.5769 * q1_2 - 0.5769 * q1_4 - 0.0962 * q1_6) * adj;

                // 5. Phasor addition
                double i2_raw = i1 - jQ;
                double q2_raw = q1 + jI;

                i2 = (0.2 * i2_raw) + (0.8 * p_i2);
                q2 = (0.2 * q2_raw) + (0.8 * p_q2);

                // 6. Homodyne Discriminator
                double re_raw = (i2 * p_i2) + (q2 * p_q2);
                double im_raw = (i2 * p_q2) - (q2 * p_i2);

                re = (0.2 * re_raw) + (0.8 * p_re);
                im = (0.2 * im_raw) + (0.8 * p_im);

                // 7. Calculate Period
                double period = 0;
                if (Math.Abs(im) > 1e-9 && Math.Abs(re) > 1e-9)
                {
                    period = 2 * Math.PI / Math.Atan(im / re);
                }

                if (prevPeriod > 0)
                {
                    if (period > 1.5 * prevPeriod) period = 1.5 * prevPeriod;
                    if (period < 0.67 * prevPeriod) period = 0.67 * prevPeriod;
                }
                if (period < 6) period = 6;
                if (period > 50) period = 50;

                period = (0.2 * period) + (0.8 * prevPeriod);
                periodBuffer[pdIdx] = period;

                double prevSmoothPeriod = smoothPeriodBuffer[(sdIdx - 1 + 2) % 2];
                double smoothPeriod = (0.33 * period) + (0.67 * prevSmoothPeriod);
                smoothPeriodBuffer[sdIdx] = smoothPeriod;

                // 8. Instantaneous Trend
                int dcPeriods = (int)(double.IsNaN(smoothPeriod) ? 0 : smoothPeriod + 0.5);
                double sumPr = 0;
                int count = 0;

                for (int d = 0; d < dcPeriods; d++)
                {
                    if (d < pCount)
                    {
                        sumPr += priceBuffer[(pIdx - d + 50) % 50];
                        count++;
                    }
                }

                double it = count > 0 ? sumPr / count : price;
                itBuffer[itIdx] = it;

                // 9. Final Trendline
                if (pCount >= 12)
                {
                    double it0 = itBuffer[itIdx];
                    double it1 = itBuffer[(itIdx - 1 + 4) % 4];
                    double it2 = itBuffer[(itIdx - 2 + 4) % 4];
                    double it3 = itBuffer[(itIdx - 3 + 4) % 4];
                    output[i] = (4 * it0 + 3 * it1 + 2 * it2 + it3) / 10.0;
                }
                else
                {
                    output[i] = price;
                }

                // Update state
                p_i2 = i2;
                p_q2 = q2;
                p_re = re;
                p_im = im;
            }

            // Advance indices
            pIdx = (pIdx + 1) % 50;
            sIdx = (sIdx + 1) % 7;
            dIdx = (dIdx + 1) % 7;
            i1Idx = (i1Idx + 1) % 7;
            q1Idx = (q1Idx + 1) % 7;
            pdIdx = (pdIdx + 1) % 2;
            sdIdx = (sdIdx + 1) % 2;
            itIdx = (itIdx + 1) % 4;
        }
    }

    public override void Reset()
    {
        Init();
    }
}
