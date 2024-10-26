extern alias volatility;
extern alias averages;
extern alias statistics;

using Xunit;
using System.Reflection;
using TradingPlatform.BusinessLayer;
using statistics::QuanTAlib;
using averages::QuanTAlib;
using volatility::QuanTAlib;

namespace QuanTAlib
{
    public class QuantowerTests
    {
        private static void TestIndicator<T>(string fieldName = "ma") where T : Indicator, new()
        {
            var indicator = new T();
            try
            {
                var onInitMethod = typeof(T).GetMethod("OnInit", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(onInitMethod);
                onInitMethod.Invoke(indicator, null);
                var onUpdateMethod = typeof(T).GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(onUpdateMethod);

                var field = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                var fieldValue = field.GetValue(indicator);
                Assert.NotNull(fieldValue);

                Assert.NotNull(indicator.ShortName);
                Assert.NotEmpty(indicator.ShortName);
                Assert.NotNull(indicator.Name);
                Assert.NotEmpty(indicator.Name);
                Assert.NotNull(indicator.Description);
                Assert.NotEmpty(indicator.Description);
                Assert.IsAssignableFrom<Indicator>(indicator);
            }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"Test failed for {typeof(T).Name}: {ex.Message}");
            }
        }

        // Averages Indicators
        [Fact] public void Afirma() => TestIndicator<AfirmaIndicator>();
        [Fact] public void Alma() => TestIndicator<AlmaIndicator>();
        [Fact] public void Dema() => TestIndicator<DemaIndicator>();
        [Fact] public void Dsma() => TestIndicator<DsmaIndicator>();
        [Fact] public void Dwma() => TestIndicator<DwmaIndicator>();
        [Fact] public void Ema() => TestIndicator<EmaIndicator>();
        [Fact] public void Epma() => TestIndicator<EpmaIndicator>();
        [Fact] public void Frama() => TestIndicator<FramaIndicator>();
        [Fact] public void Fwma() => TestIndicator<FwmaIndicator>();
        [Fact] public void Gma() => TestIndicator<GmaIndicator>();
        [Fact] public void Hma() => TestIndicator<HmaIndicator>();
        [Fact] public void Htit() => TestIndicator<HtitIndicator>();
        [Fact] public void Hwma() => TestIndicator<HwmaIndicator>();
        [Fact] public void Jma() => TestIndicator<JmaIndicator>();
        [Fact] public void Kama() => TestIndicator<KamaIndicator>();
        [Fact] public void Ltma() => TestIndicator<LtmaIndicator>();
        [Fact] public void Maaf() => TestIndicator<MaafIndicator>();
        [Fact] public void Mama() => TestIndicator<MamaIndicator>();
        [Fact] public void Mgdi() => TestIndicator<MgdiIndicator>();
        [Fact] public void Mma() => TestIndicator<MmaIndicator>();
        [Fact] public void Pwma() => TestIndicator<PwmaIndicator>();
        [Fact] public void Qema() => TestIndicator<QemaIndicator>();
        [Fact] public void Rema() => TestIndicator<RemaIndicator>();
        [Fact] public void Rma() => TestIndicator<RmaIndicator>();
        [Fact] public void Sinema() => TestIndicator<SinemaIndicator>();
        [Fact] public void Sma() => TestIndicator<SmaIndicator>();
        [Fact] public void Smma() => TestIndicator<SmmaIndicator>();
        [Fact] public void T3() => TestIndicator<T3Indicator>();
        [Fact] public void Tema() => TestIndicator<TemaIndicator>();
        [Fact] public void Trima() => TestIndicator<TrimaIndicator>();
        [Fact] public void Vidya() => TestIndicator<VidyaIndicator>();
        [Fact] public void Wma() => TestIndicator<WmaIndicator>();
        [Fact] public void Zlema() => TestIndicator<ZlemaIndicator>();

        // Statistics Indicators
        [Fact] public void Curvature() => TestIndicator<CurvatureIndicator>("curvature");
        [Fact] public void Entropy() => TestIndicator<EntropyIndicator>("entropy");
        [Fact] public void Kurtosis() => TestIndicator<KurtosisIndicator>("kurtosis");
        [Fact] public void Max() => TestIndicator<MaxIndicator>("ma");
        [Fact] public void Median() => TestIndicator<MedianIndicator>("med");
        [Fact] public void Min() => TestIndicator<MinIndicator>("mi");
        [Fact] public void Mode() => TestIndicator<ModeIndicator>("mode");
        [Fact] public void Percentile() => TestIndicator<PercentileIndicator>("percentile");
        [Fact] public void Skew() => TestIndicator<SkewIndicator>("skew");
        [Fact] public void Slope() => TestIndicator<SlopeIndicator>("slope");
        [Fact] public void Stddev() => TestIndicator<StddevIndicator>("stddev");
        [Fact] public void Variance() => TestIndicator<VarianceIndicator>("variance");
        [Fact] public void Zscore() => TestIndicator<ZscoreIndicator>("zScore");

        // Volatility Indicators
        [Fact] public void Atr() => TestIndicator<AtrIndicator>("atr");

        [Fact] public void Historical() => TestIndicator<HistoricalIndicator>("historical");
        [Fact] public void Realized() => TestIndicator<RealizedIndicator>("realized");
        [Fact] public void Rvi() => TestIndicator<RviIndicator>("rvi");
    }
}
