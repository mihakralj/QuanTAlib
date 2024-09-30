using Xunit;
using System.Reflection;

namespace QuanTAlib
{
    public class IndicatorTests
    {
        private Random rnd;
        private const int SeriesLen = 1000;
        private const int Corrections = 100;

        public IndicatorTests()
        {
            rnd = new Random((int)DateTime.Now.Ticks);
        }

        private static readonly iTValue[] indicators =
        [
            new Ema(period: 10, useSma: true),
            new Alma(period: 14, offset: 0.85, sigma: 6),
            new Convolution(new double[] { 1.0, 2, 3, 2, 1 }),
            new Dema(period: 14),
            new Dsma(period: 14),
            new Dwma(period: 14),
            new Epma(period: 14),
            new Frama(period: 14),
            new Fwma(period: 14),
            new Gma(period: 14),
            new Hma(period: 14),
            new Hwma(period: 14),
            new Kama(period: 14),
            new Mama(fastLimit: 0.5, slowLimit: 0.05),
            new Mgdi(period: 14),
            new Mma(period: 14),
            new Qema(),
            new Rema(period: 14),
            new Rma(period: 14),
            new Sinema(period: 14),
            new Sma(period: 14),
            new Smma(period: 14),
            new T3(period: 14),
            new Tema(period: 14),
            new Trima(period: 14),
            new Vidya(shortPeriod: 14, longPeriod: 30, alpha: 0.2),
            new Wma(period: 14),
            new Zlema(period: 14),
            new Entropy(period: 14),
            new Kurtosis(period: 14),
            new Max(period: 14, decay: 0.01),
            new Min(period: 14, decay: 0.01),
            new Median(period: 14),
            new Mode(period: 14),
            new Percentile(period: 14, percent: 50),
            new Skew(period: 14),
            new Stddev(period: 14),
            new Variance(period: 14),
            new Zscore(period: 14)

        ];

        [Theory]
        [MemberData(nameof(GetIndicators))]
        public void IndicatorIsNew(iTValue indicator)
        {
            var indicator1 = indicator;
            var indicator2 = indicator;

            MethodInfo calcMethod = indicator.GetType().GetMethod("Calc")!;
            if (calcMethod == null)
            {
                throw new Exception($"Calc method not found for indicator type: {indicator.GetType().Name}");
            }

            for (int i = 0; i < SeriesLen; i++)
            {
                TValue item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: true);
                calcMethod.Invoke(indicator1, new object[] { item1 });

                for (int j = 0; j < Corrections; j++)
                {
                    item1 = new(Time: DateTime.Now, Value: rnd.Next(-100, 100), IsNew: false);
                    calcMethod.Invoke(indicator1, new object[] { item1 });
                }

                var item2 = new TValue(item1.Time, item1.Value, IsNew: true);
                calcMethod.Invoke(indicator2, new object[] { item2 });

                Assert.Equal(indicator1.Value, indicator2.Value);
            }
        }

        public static IEnumerable<object[]> GetIndicators()
        {
            return indicators.Select(indicator => new object[] { indicator });
        }
    }
}