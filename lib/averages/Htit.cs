//not working yet
//TODO consistency test

using QuanTAlib;

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

    private double _lastPd = 0;
    private double _p_lastPd = 0;

    public Htit() : base()
    {
        Name = "Htit";
        WarmupPeriod = 12;
    }

    public Htit(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

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
        double sp = ((4 * _priceBuffer[0]) + (3 * _priceBuffer[1]) + (2 * _priceBuffer[2]) + _priceBuffer[3]) / 10;
        _spBuffer.Add(sp, Input.IsNew);

        double dt = ((0.0962 * _spBuffer[0]) + (0.5769 * _spBuffer[2]) - (0.5769 * _spBuffer[4]) - (0.0962 * _spBuffer[6])) * adj;
        _dtBuffer.Add(dt, Input.IsNew);

        // In-phase and quadrature
        double q1 = ((0.0962 * _dtBuffer[0]) + (0.5769 * _dtBuffer[2]) - (0.5769 * _dtBuffer[4]) - (0.0962 * _dtBuffer[6])) * adj;
        _q1Buffer.Add(q1, Input.IsNew);

        double i1 = _dtBuffer[3];
        _i1Buffer.Add(i1, Input.IsNew);

        // Advance the phases by 90 degrees
        double jI = ((0.0962 * _i1Buffer[0]) + (0.5769 * _i1Buffer[2]) - (0.5769 * _i1Buffer[4]) - (0.0962 * _i1Buffer[6])) * adj;
        double jQ = ((0.0962 * _q1Buffer[0]) + (0.5769 * _q1Buffer[2]) - (0.5769 * _q1Buffer[4]) - (0.0962 * _q1Buffer[6])) * adj;

        // Phasor addition for 3-bar averaging
        double i2 = i1 - jQ;
        double q2 = q1 + jI;

        i2 = (0.2 * i2) + (0.8 * _i2Buffer[0]);
        q2 = (0.2 * q2) + (0.8 * _q2Buffer[0]);

        _i2Buffer.Add(i2, Input.IsNew);
        _q2Buffer.Add(q2, Input.IsNew);

        // Homodyne discriminator
        double re = (i2 * _i2Buffer[1]) + (q2 * _q2Buffer[1]);
        double im = (i2 * _q2Buffer[1]) - (q2 * _i2Buffer[1]);

        re = (0.2 * re) + (0.8 * _reBuffer[0]);
        im = (0.2 * im) + (0.8 * _imBuffer[0]);

        _reBuffer.Add(re, Input.IsNew);
        _imBuffer.Add(im, Input.IsNew);

        // Calculate period
        double pd = (im != 0 && re != 0) ? 2 * Math.PI / Math.Atan(im / re) : 0;

        // Adjust period to thresholds
        pd = (pd > 1.5 * _lastPd) ? 1.5 * _lastPd : pd;
        pd = (pd < 0.67 * _lastPd) ? 0.67 * _lastPd : pd;
        pd = (pd < 6) ? 6 : pd;
        pd = (pd > 50) ? 50 : pd;

        // Smooth the period
        pd = (0.2 * pd) + (0.8 * _lastPd);
        _pdBuffer.Add(pd, Input.IsNew);

        double sd = (0.33 * pd) + (0.67 * _sdBuffer[0]);
        _sdBuffer.Add(sd, Input.IsNew);

        // Smooth dominant cycle period
        int dcPeriods = (int)(sd + 0.5);
        double sumPr = _priceBuffer.GetSpan().Slice(0, Math.Min(dcPeriods, _priceBuffer.Count)).ToArray().Sum();
        double it = dcPeriods > 0 ? sumPr / dcPeriods : pr;
        _itBuffer.Add(it, Input.IsNew);

        _p_lastPd = _lastPd;
        _lastPd = pd;

        // Final indicator
        if (_index >= 11) // 12th bar
        {
            return ((4 * _itBuffer[0]) + (3 * _itBuffer[1]) + (2 * _itBuffer[2]) + _itBuffer[3]) / 10;
        }
        else
        {
            return pr;
        }
    }
}