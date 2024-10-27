using System;
namespace QuanTAlib;

/// <summary>
/// AFIRMA: Adaptive FIR Moving Average
/// A finite impulse response (FIR) filter that combines windowing functions with sinc-based filtering.
/// Provides superior noise reduction while maintaining signal fidelity through adaptive filtering.
/// </summary>
/// <remarks>
/// Implementation:
///     Original implementation based on FIR filter design principles
/// </remarks>

public class Afirma : AbstractBase
{
    public enum WindowType
    {
        Rectangular,
        Hanning1,
        Hanning2,
        Blackman,
        BlackmanHarris
    }

    private readonly int Periods;
    private readonly int Taps;
    private readonly WindowType Window;
    private readonly CircularBuffer _buffer;
    private readonly double[] _weights;
    private readonly double _wsum;
    private readonly double[] _armaBuffer;
    private readonly int _n;
    private readonly double _sx2, _sx3, _sx4, _sx5, _sx6, _den;

    /// <param name="periods">The number of periods for the sinc filter calculation.</param>
    /// <param name="taps">The number of filter taps (filter length). Must be odd number.</param>
    /// <param name="window">The type of window function to apply (Rectangular, Hanning1, Hanning2, Blackman, or BlackmanHarris).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when periods or taps is less than 1.</exception>
    public Afirma(int periods, int taps, WindowType window)
    {
        if (periods < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(periods), "Periods must be greater than or equal to 1.");
        }
        if (taps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(taps), "Taps must be greater than or equal to 1.");
        }
        Periods = periods;
        Taps = taps;
        Window = window;
        WarmupPeriod = taps;
        _buffer = new CircularBuffer(taps);
        _weights = new double[taps];
        _wsum = CalculateWeights();
        _armaBuffer = new double[taps];
        _n = (Taps - 1) / 2;

        // Calculate least squares coefficients in the constructor
        _sx2 = (2 * _n + 1) / 3.0;
        _sx3 = _n * (_n + 1) / 2.0;
        _sx4 = _sx2 * (3 * _n * _n + 3 * _n - 1) / 5.0;
        _sx5 = _sx3 * (2 * _n * _n + 2 * _n - 1) / 3.0;
        _sx6 = _sx2 * (3 * Math.Pow(_n, 3) * (_n + 2) - 3 * _n + 1) / 7.0;
        _den = _sx6 * _sx4 / _sx5 - _sx5;

        Name = "Afirma";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="periods">The number of periods for the sinc filter calculation.</param>
    /// <param name="taps">The number of filter taps (filter length). Must be odd number.</param>
    /// <param name="window">The type of window function to apply (Rectangular, Hanning1, Hanning2, Blackman, or BlackmanHarris).</param>
    public Afirma(object source, int periods, int taps, WindowType window) : this(periods, taps, window)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        if (_index >= Taps)
        {
            double a0 = _buffer[_n];
            double a1 = _buffer[_n] - _buffer[_n + 1];
            double sx2y = 0.0;
            double sx3y = 0.0;

            for (int i = 0; i <= _n; i++)
            {
                sx2y += i * i * _buffer[_n - i];
                sx3y += i * i * i * _buffer[_n - i];
            }

            sx2y = 2.0 * sx2y / _n / (_n + 1);
            sx3y = 2.0 * sx3y / _n / (_n + 1);
            double p = sx2y - a0 * _sx2 - a1 * _sx3;
            double q = sx3y - a0 * _sx3 - a1 * _sx4;
            double a2 = (p * _sx6 / _sx5 - q) / _den;
            double a3 = (q * _sx4 / _sx5 - p) / _den;

            for (int k = 0; k <= _n; k++)
            {
                _armaBuffer[_n - k] = a0 + k * a1 + k * k * a2 + k * k * k * a3;
            }
        }

        double result = 0.0;
        for (int k = 0; k < Taps; k++)
        {
            result += _buffer[k] * _weights[k] / _wsum;
        }

        IsHot = _index >= WarmupPeriod;
        return result;
    }

    private double CalculateWeights()
    {
        double wsum = 0.0;
        double centerTap = (Taps - 1) / 2.0;
        for (int k = 0; k < Taps; k++)
        {
            double windowWeight;
            switch (Window)
            {
                case WindowType.Rectangular:
                    windowWeight = 1.0;
                    break;
                case WindowType.Hanning1:
                    windowWeight = 0.50 - 0.50 * Math.Cos(2.0 * Math.PI * k / (Taps - 1));
                    break;
                case WindowType.Hanning2:
                    windowWeight = 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * k / (Taps - 1));
                    break;
                case WindowType.Blackman:
                    windowWeight = 0.42 - 0.50 * Math.Cos(2.0 * Math.PI * k / (Taps - 1)) + 0.08 * Math.Cos(4.0 * Math.PI * k / (Taps - 1));
                    break;
                case WindowType.BlackmanHarris:
                    windowWeight = 0.35875 - 0.48829 * Math.Cos(2.0 * Math.PI * k / (Taps - 1)) + 0.14128 * Math.Cos(4.0 * Math.PI * k / (Taps - 1)) - 0.01168 * Math.Cos(6.0 * Math.PI * k / (Taps - 1));
                    break;
                default:
                    windowWeight = 1.0;
                    break;
            }

            double sincWeight;
            sincWeight = Math.Abs(k - centerTap) < 1e-10 ? 1.0 : Math.Sin(Math.PI * (k - centerTap) / Periods) / (Math.PI * (k - centerTap) / Periods);

            _weights[k] = windowWeight * sincWeight;
            wsum += _weights[k];
        }
        return wsum;
    }
}
