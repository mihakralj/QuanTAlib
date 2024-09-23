using System;
using System.Linq;

namespace QuanTAlib
{
    public class Median : AbstractBase
    {
        public readonly int Period;
        private CircularBuffer _buffer;

        public Median(int period) : base()
        {
            if (period < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
            }
            Period = period;
            WarmupPeriod = period;
            _buffer = new CircularBuffer(period);
            Name = $"Median(period={period})";
            Init();
        }

        public Median(object source, int period) : this(period)
        {
            var pubEvent = source.GetType().GetEvent("Pub");
            pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
        }

        public override void Init()
        {
            base.Init();
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

            double median;
            if (_index >= Period)
            {
                var sortedValues = _buffer.GetSpan().ToArray();
                Array.Sort(sortedValues);
                int middleIndex = sortedValues.Length / 2;

                if (sortedValues.Length % 2 == 0)
                {
                    median = (sortedValues[middleIndex - 1] + sortedValues[middleIndex]) / 2.0;
                }
                else
                {
                    median = sortedValues[middleIndex];
                }
            }
            else
            {
                median = _buffer.Average(); // Use average until we have enough data points
            }

            IsHot = _index >= WarmupPeriod;
            return median;
        }
    }
}