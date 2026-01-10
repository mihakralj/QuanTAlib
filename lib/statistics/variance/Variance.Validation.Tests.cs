using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using MathNet.Numerics.Statistics;

namespace QuanTAlib.Validation;

public class VarianceValidationTests
{
    private readonly ValidationTestData _data = new();

    [Fact]
    public void Variance_Matches_Skender_StdDev_Squared()
    {
        // Skender StdDev uses Population Standard Deviation (N) for calculation,
        // despite documentation often implying Sample (N-1).
        // Variance(isPopulation: true) should match StdDev^2.

        int period = 20;
        var variance = new Variance(period, isPopulation: true);
        var skenderStdDev = _data.SkenderQuotes.GetStdDev(period);

        var skenderList = skenderStdDev.ToList();
        var quotes = _data.SkenderQuotes.ToList();

        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = variance.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));
            var skenderVal = skenderList[i].StdDev;

            if (i >= period && skenderVal.HasValue)
            {
                double expectedVariance = skenderVal.Value * skenderVal.Value;
                Assert.Equal(expectedVariance, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Variance_Matches_Talib_Var()
    {
        // TA-Lib VAR uses Population Variance (N)
        int period = 20;
        var variance = new Variance(period, isPopulation: true);

        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();
        double[] output = new double[input.Length];

        // TA-Lib calculation
        // VAR(real, timeperiod=5, nbdev=1)
        var retCode = TALib.Functions.Var(input, 0..^0, output, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = variance.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));

            if (i >= outRange.Start.Value)
            {
                double talibVal = output[i - outRange.Start.Value];
                Assert.Equal(talibVal, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Variance_Matches_Tulip_Var()
    {
        // Tulip VAR uses Population Variance (N)
        int period = 20;
        var variance = new Variance(period, isPopulation: true);

        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();

        // Tulip calculation
        var varInd = Tulip.Indicators.var;
        double[][] inputs = { input };
        double[] options = { period };
        double[][] outputs = { new double[input.Length - varInd.Start(options)] };

        varInd.Run(inputs, options, outputs);

        double[] output = outputs[0];
        int lookback = varInd.Start(options);

        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = variance.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));

            if (i >= lookback)
            {
                double tulipVal = output[i - lookback];
                Assert.Equal(tulipVal, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Variance_Matches_MathNet()
    {
        int period = 20;
        var variance = new Variance(period, isPopulation: false);
        var popVariance = new Variance(period, isPopulation: true);

        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = variance.Update(new TValue(DateTime.UtcNow, input[i]));
            var popVal = popVariance.Update(new TValue(DateTime.UtcNow, input[i]));

            if (i >= input.Length - 100)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = window.Variance();
                double expectedPop = window.PopulationVariance();

                Assert.Equal(expected, val.Value, ValidationHelper.DefaultTolerance);
                Assert.Equal(expectedPop, popVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }
}
