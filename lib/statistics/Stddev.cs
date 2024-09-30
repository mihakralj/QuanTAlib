using System;
using System.Linq;

namespace QuanTAlib
{
    public class Stddev : AbstractBase
    {
        private readonly int Period;
        private readonly bool IsPopulation;
        private readonly CircularBuffer _buffer;

        public Stddev(int period, bool isPopulation = false)
        {
            if (period < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
            }
            Period = period;
            IsPopulation = isPopulation;
            WarmupPeriod = 0;
            _buffer = new CircularBuffer(period);
            Name = $"Stddev(period={period}, population={isPopulation})";
            Init();
        }

        public Stddev(object source, int period, bool isPopulation = false) : this(period, isPopulation)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
            _buffer.Clear();
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
            ManageState(Input.IsNew);

            _buffer.Add(Input.Value, Input.IsNew);

            double stddev = 0;
            if (_buffer.Count > 1)
            {
                var values = _buffer.GetSpan().ToArray();
                double mean = values.Average();
                double sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

                double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
                double variance = sumOfSquaredDifferences / divisor;
                stddev = Math.Sqrt(variance);
            }

            IsHot = true; // StdDev calc is valid from bar 1
            return stddev;
        }
    }
}