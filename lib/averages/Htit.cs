using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// HTIT: Hilbert Transform Instantaneous Trendline
/// A sophisticated moving average that uses the Hilbert Transform to identify the dominant cycle
/// period in price data and create a smooth trend line. It adapts to the market's natural cycles
/// and provides a dynamic moving average.
/// </summary>
/// <remarks>
/// The HTIT calculation process:
/// 1. Uses a Hilbert Transform to decompose price into in-phase and quadrature components
/// 2. Employs a homodyne discriminator to determine the dominant cycle period
/// 3. Applies smoothing based on the detected cycle period
/// 4. Creates a trend line that automatically adapts to market cycles
///
/// Key characteristics:
/// - Automatically adapts to market cycles
/// - Reduces lag by using cycle analysis
/// - Complex signal processing for better trend identification
/// - Combines multiple digital signal processing techniques
///
/// Sources:
///     John Ehlers - "Cycle Analytics for Traders"
///
/// Note: This implementation is currently under development and may not pass
/// all consistency tests.
/// </remarks>
public class Htit : AbstractBase
{
    private readonly CircularBuffer _priceBuffer = new(7);
    private readonly CircularBuffer _spBuffer = new(7);
    private readonly CircularBuffer _dtBuffer = new(7);
    private readonly CircularBuffer _i1Buffer = new(7);
    private readonly CircularBuffer _q1Buffer = new(7);
    private readonly CircularBuffer _i2Buffer = new(2);
    private readonly CircularBuffer _q2Buffer = new(2);
    private readonly CircularBuffer _reBuffer = new(2);
    private readonly CircularBuffer _imBuffer = new(2);
    private readonly CircularBuffer _pdBuffer = new(2);
    private readonly CircularBuffer _sdBuffer = new(2);
    private readonly CircularBuffer _itBuffer = new(4);

    private const double ALPHA = 0.2;
    private const double BETA = 0.8;
    private const double TWO_PI = 2.0 * System.Math.PI;
    private const double MIN_PERIOD = 6.0;
    private const double MAX_PERIOD = 50.0;
    private const double PERIOD_UPPER_LIMIT = 1.5;
    private const double PERIOD_LOWER_LIMIT = 0.67;

    private double _lastPd = 0;
    private double _p_lastPd = 0;

    public Htit()
    {
        Name = "Htit";
        WarmupPeriod = 12;
    }

    public Htit(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastPd = _lastPd;
            _index++;
        }
        else
        {
            _lastPd = _p_lastPd;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateSmoothedPrice(double p0, double p1, double p2, double p3)
    {
        return ((4.0 * p0) + (3.0 * p1) + (2.0 * p2) + p3) * 0.1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateHilbertTransform(double b0, double b2, double b4, double b6, double adj)
    {
        return ((0.0962 * (b0 - b6)) + (0.5769 * (b2 - b4))) * adj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ClampPeriod(double pd, double lastPd)
    {
        pd = pd > PERIOD_UPPER_LIMIT * lastPd ? PERIOD_UPPER_LIMIT * lastPd : pd;
        pd = pd < PERIOD_LOWER_LIMIT * lastPd ? PERIOD_LOWER_LIMIT * lastPd : pd;
        return System.Math.Clamp(pd, MIN_PERIOD, MAX_PERIOD);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double pr = Input.Value;
        _priceBuffer.Add(pr, Input.IsNew);

        if (_index <= 5)
        {
            _spBuffer.Add(0, Input.IsNew);
            _dtBuffer.Add(0, Input.IsNew);
            _i1Buffer.Add(0, Input.IsNew);
            _q1Buffer.Add(0, Input.IsNew);
            _i2Buffer.Add(0, Input.IsNew);
            _q2Buffer.Add(0, Input.IsNew);
            _reBuffer.Add(0, Input.IsNew);
            _imBuffer.Add(0, Input.IsNew);
            _pdBuffer.Add(0, Input.IsNew);
            _sdBuffer.Add(0, Input.IsNew);
            _itBuffer.Add(pr, Input.IsNew);
            return pr;
        }

        double adj = (0.075 * _lastPd) + 0.54;

        // Smooth and detrender
        double sp = CalculateSmoothedPrice(_priceBuffer[0], _priceBuffer[1], _priceBuffer[2], _priceBuffer[3]);
        _spBuffer.Add(sp, Input.IsNew);

        double dt = CalculateHilbertTransform(_spBuffer[0], _spBuffer[2], _spBuffer[4], _spBuffer[6], adj);
        _dtBuffer.Add(dt, Input.IsNew);

        // In-phase and quadrature
        double q1 = CalculateHilbertTransform(_dtBuffer[0], _dtBuffer[2], _dtBuffer[4], _dtBuffer[6], adj);
        _q1Buffer.Add(q1, Input.IsNew);

        double i1 = _dtBuffer[3];
        _i1Buffer.Add(i1, Input.IsNew);

        // Advance the phases by 90 degrees
        double jI = CalculateHilbertTransform(_i1Buffer[0], _i1Buffer[2], _i1Buffer[4], _i1Buffer[6], adj);
        double jQ = CalculateHilbertTransform(_q1Buffer[0], _q1Buffer[2], _q1Buffer[4], _q1Buffer[6], adj);

        // Phasor addition for 3-bar averaging
        double i2 = (ALPHA * (i1 - jQ)) + (BETA * _i2Buffer[0]);
        double q2 = (ALPHA * (q1 + jI)) + (BETA * _q2Buffer[0]);

        _i2Buffer.Add(i2, Input.IsNew);
        _q2Buffer.Add(q2, Input.IsNew);

        // Homodyne discriminator
        double re = (ALPHA * ((i2 * _i2Buffer[1]) + (q2 * _q2Buffer[1]))) + (BETA * _reBuffer[0]);
        double im = (ALPHA * ((i2 * _q2Buffer[1]) - (q2 * _i2Buffer[1]))) + (BETA * _imBuffer[0]);

        _reBuffer.Add(re, Input.IsNew);
        _imBuffer.Add(im, Input.IsNew);

        // Calculate period
        double pd = (im != 0 && re != 0) ? TWO_PI / System.Math.Atan(im / re) : 0;
        pd = ClampPeriod(pd, _lastPd);
        pd = (ALPHA * pd) + (BETA * _lastPd);
        _pdBuffer.Add(pd, Input.IsNew);

        double sd = (0.33 * pd) + (0.67 * _sdBuffer[0]);
        _sdBuffer.Add(sd, Input.IsNew);

        // Smooth dominant cycle period
        int dcPeriods = (int)(sd + 0.5);
        double sumPr = _priceBuffer.GetSpan().Slice(0, System.Math.Min(dcPeriods, _priceBuffer.Count)).ToArray().Sum();
        double it = dcPeriods > 0 ? sumPr / dcPeriods : pr;
        _itBuffer.Add(it, Input.IsNew);

        _p_lastPd = _lastPd;
        _lastPd = pd;

        // Final indicator
        if (_index >= 11)
        {
            return CalculateSmoothedPrice(_itBuffer[0], _itBuffer[1], _itBuffer[2], _itBuffer[3]);
        }

        return pr;
    }
}
