/*
Reference:
    Donald Dorsey, who introduced the concept in the 1993 issue of Technical Analysis
    of Stocks & Commodities Magazine. He designed the RVI to focus on the direction of
    price movements in relation to volatility. Dorsey’s methodology is often cited in
    technical analysis literature and further elaborated on in various technical analysis
    guides and platforms.
*/


using System;

namespace QuanTAlib
{
    public class Rvi : AbstractBase
    {
        private readonly int Period;
        private Stddev _upStdDev, _downStdDev;
        private Sma _upSma, _downSma;
        private double _previousClose;

        public Rvi(int period) : base()
        {
            if (period < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
            }
            Period = period;
            WarmupPeriod = period;
            Name = $"RVI(period={period})";
            _upStdDev = new Stddev(Period);
            _downStdDev = new Stddev(Period);
            _upSma = new(Period);
            _downSma = new(Period);
            Init();
        }

        public Rvi(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _previousClose = 0;
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _lastValidValue = Value;
                _index++;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);

            double close = Input.Value;
            double change = close - _previousClose;

            double upMove = Math.Max(change, 0);
            double downMove = Math.Max(-change, 0);

            _upSma.Calc(_upStdDev.Calc(new TValue(Input.Time, upMove, Input.IsNew)));
            _downSma.Calc(_downStdDev.Calc(new TValue(Input.Time, downMove, Input.IsNew)));

            double rvi;
            if (_upSma.Value + _downSma.Value != 0)
            {
                rvi = 100 * _upSma.Value / (_upSma.Value + _downSma.Value);
            }
            else
            {
                rvi = 0;
            }

            _previousClose = close;
            IsHot = _index >= WarmupPeriod;
            return rvi;
        }
    }
}