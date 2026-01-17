using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ROC (Rate of Change) against Tulip MOM (Momentum).
/// Tulip's MOM calculates absolute change: current - past
/// </summary>
public class RocValidationTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.05, seed: 60200);
    private const int TestPeriod = 9;
    private const int DataPoints = 500;
    private const double TulipTolerance = 1e-9;

    #region Tulip MOM Validation

    [Fact]
    public void Roc_MatchesTulipMom_Batch()
    {
        var bars = _gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tulipInput = source.Values.ToArray();

        // Get QuanTAlib ROC result
        var quantResult = Roc.Calculate(source, TestPeriod);

        // Calculate Tulip MOM (momentum = current - past)
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        // Compare (accounting for Tulip's offset due to lookback)
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], quantResult[qIdx].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Roc_MatchesTulipMom_Streaming()
    {
        var bars = _gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tulipInput = source.Values.ToArray();

        // Get QuanTAlib ROC result via streaming
        var roc = new Roc(TestPeriod);
        var streamingResults = new List<double>();

        for (int i = 0; i < source.Count; i++)
        {
            var tv = roc.Update(new TValue(source[i].Time, source[i].Value), true);
            streamingResults.Add(tv.Value);
        }

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        // Compare after warmup
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], streamingResults[qIdx], TulipTolerance);
        }
    }

    [Fact]
    public void Roc_MatchesTulipMom_Span()
    {
        var bars = _gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tulipInput = source.Values.ToArray();

        // Get QuanTAlib ROC result via span
        var quantOutput = new double[DataPoints];
        Roc.Calculate(source.Values, quantOutput, TestPeriod);

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [TestPeriod];
        int lookback = TestPeriod;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        // Compare after warmup
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], quantOutput[qIdx], TulipTolerance);
        }
    }

    #endregion

    #region Different Periods

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Roc_MatchesTulipMom_DifferentPeriods(int period)
    {
        var bars = _gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tulipInput = source.Values.ToArray();

        var quantResult = Roc.Calculate(source, period);

        // Calculate Tulip MOM
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [period];
        int lookback = period;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], quantResult[qIdx].Value, TulipTolerance);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Roc_HandlesConstantValues()
    {
        var constantData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            constantData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        var result = Roc.Calculate(constantData, TestPeriod);

        // Constant values should produce 0 change after warmup
        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(0.0, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Roc_HandlesLinearlyIncreasing()
    {
        var linearData = new TSeries(100);
        for (int i = 0; i < 100; i++)
        {
            linearData.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        var result = Roc.Calculate(linearData, TestPeriod);

        // Linear increase by 1 per bar means ROC = period after warmup
        for (int i = TestPeriod; i < 100; i++)
        {
            Assert.Equal(TestPeriod, result[i].Value, TulipTolerance);
        }
    }

    [Fact]
    public void Roc_Period1_MatchesTulipMom()
    {
        var bars = _gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;
        double[] tulipInput = source.Values.ToArray();

        var quantResult = Roc.Calculate(source, 1);

        // Calculate Tulip MOM with period 1
        var momIndicator = Tulip.Indicators.mom;
        double[][] inputs = [tulipInput];
        double[] options = [1];
        int lookback = 1;
        double[][] outputs = [new double[tulipInput.Length - lookback]];

        momIndicator.Run(inputs, options, outputs);
        var tulipResult = outputs[0];

        // Period 1 is single-bar change
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], quantResult[qIdx].Value, TulipTolerance);
        }
    }

    #endregion
}
