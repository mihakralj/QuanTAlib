using System.Runtime.CompilerServices;
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
    private readonly double _twoPi = 2.0 * Math.PI;
    private readonly double _fourPi = 4.0 * Math.PI;
    private readonly double _sixPi = 6.0 * Math.PI;

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

        // Precalculate least squares coefficients
        _sx2 = ((2 * _n) + 1) / 3.0;
        _sx3 = _n * (_n + 1) / 2.0;
        _sx4 = _sx2 * ((3 * _n * _n) + (3 * _n) - 1) / 5.0;
        _sx5 = _sx3 * ((2 * _n * _n) + (2 * _n) - 1) / 3.0;
        _sx6 = _sx2 * ((3 * Math.Pow(_n, 3) * (_n + 2)) - (3 * _n) + 1) / 7.0;
        _den = (_sx6 * _sx4 / _sx5) - _sx5;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateSincWeight(double x)
    {
        return Math.Abs(x) < 1e-10 ? 1.0 : Math.Sin(x) / x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetWindowWeight(int k, int tapsMinusOne)
    {
        switch (Window)
        {
            case WindowType.Rectangular:
                return 1.0;
            case WindowType.Hanning1:
                return 0.50 - (0.50 * Math.Cos(_twoPi * k / tapsMinusOne));
            case WindowType.Hanning2:
                return 0.54 - (0.46 * Math.Cos(_twoPi * k / tapsMinusOne));
            case WindowType.Blackman:
                return 0.42 - (0.50 * Math.Cos(_twoPi * k / tapsMinusOne)) + (0.08 * Math.Cos(_fourPi * k / tapsMinusOne));
            case WindowType.BlackmanHarris:
                return 0.35875 - (0.48829 * Math.Cos(_twoPi * k / tapsMinusOne)) +
                       (0.14128 * Math.Cos(_fourPi * k / tapsMinusOne)) -
                       (0.01168 * Math.Cos(_sixPi * k / tapsMinusOne));
            default:
                return 1.0;
        }
    }

    protected override double Calculation()
    {
        ManageState(IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        if (_index >= Taps)
        {
            CalculateAdaptiveCoefficients();
        }

        double result = 0.0;
        for (int k = 0; k < Taps; k++)
        {
            result += _buffer[k] * _weights[k];
        }

        IsHot = _index >= WarmupPeriod;
        return result / _wsum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateAdaptiveCoefficients()
    {
        double a0 = _buffer[_n];
        double a1 = _buffer[_n] - _buffer[_n + 1];
        double sx2y = 0.0;
        double sx3y = 0.0;

        for (int i = 0; i <= _n; i++)
        {
            double i2 = i * i;
            sx2y += i2 * _buffer[_n - i];
            sx3y += i2 * i * _buffer[_n - i];
        }

        sx2y = 2.0 * sx2y / _n / (_n + 1);
        sx3y = 2.0 * sx3y / _n / (_n + 1);
        double p = sx2y - (a0 * _sx2) - (a1 * _sx3);
        double q = sx3y - (a0 * _sx3) - (a1 * _sx4);
        double a2 = ((p * _sx6 / _sx5) - q) / _den;
        double a3 = ((q * _sx4 / _sx5) - p) / _den;

        for (int k = 0; k <= _n; k++)
        {
            double k2 = k * k;
            _armaBuffer[_n - k] = a0 + (k * a1) + (k2 * a2) + (k2 * k * a3);
        }
    }

    private double CalculateWeights()
    {
        double wsum = 0.0;
        double centerTap = (Taps - 1) / 2.0;
        int tapsMinusOne = Taps - 1;

        for (int k = 0; k < Taps; k++)
        {
            double windowWeight = GetWindowWeight(k, tapsMinusOne);
            double x = Math.PI * (k - centerTap) / Periods;
            double sincWeight = CalculateSincWeight(x);

            _weights[k] = windowWeight * sincWeight;
            wsum += _weights[k];
        }
        return wsum;
    }
}
