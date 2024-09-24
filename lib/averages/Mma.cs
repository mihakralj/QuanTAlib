using System;
using System.Linq;

namespace QuanTAlib
{
    public class Mma : AbstractBase
    {
        private readonly int _period;
        private readonly CircularBuffer _buffer;
        private double _lastMma;

        public Mma(int period) 
        {
            if (period < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
            }
            _period = period;
            _buffer = new CircularBuffer(period);
            Name = "Mma";
            WarmupPeriod = period;
            Init();
        }

        public Mma(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _lastMma = 0;
            _buffer.Clear();
        }

        protected override void ManageState(bool isNew)
        {
            if (isNew)
            {
                _index++;
            }
        }

        protected override double Calculation()
        {
            ManageState(Input.IsNew);
            _buffer.Add(Input.Value, Input.IsNew);

            if (_index >= _period)
            {
                double T = _buffer.Sum();
                double S = CalculateWeightedSum();
                _lastMma = (T / _period) + (6 * S) / ((_period + 1) * _period);
            }
            else
            {
                // Use simple average until we have enough data points
                _lastMma = _buffer.Average();
            }

            IsHot = _index >= _period;
            return _lastMma;
        }

        private double CalculateWeightedSum()
        {
            double sum = 0;
            for (int i = 0; i < _period; i++)
            {
                double weight = (_period - (2 * i + 1)) / 2.0;
                sum += weight * _buffer[^(i + 1)];
            }
            return sum;
        }
    }
}