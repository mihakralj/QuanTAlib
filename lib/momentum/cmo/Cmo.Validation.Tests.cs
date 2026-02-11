using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CMO against external libraries.
/// CMO = 100 × (SumUp - SumDown) / (SumUp + SumDown)
/// </summary>
public class CmoValidationTests
{
    private const double Epsilon = 1e-9;

    // ═══════════════════════════════════════════════════════════════════════════
    // Tulip Indicators Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cmo_MatchesTulip_StandardData()
    {
        // Generate test data
        double[] prices = new double[50];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + Math.Sin(i * 0.3) * 10 + i * 0.1;
        }

        int period = 14;

        // Calculate using Tulip
        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [prices];
        double[] options = [period];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[prices.Length - lookback]];
        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipOutput = outputs[0];

        // Calculate using our CMO
        double[] ourOutput = new double[prices.Length];
        Cmo.Batch(prices, ourOutput, period);

        // Compare results - Tulip outputs from index 0 corresponding to our index period
        for (int i = 0; i < tulipOutput.Length; i++)
        {
            Assert.Equal(tulipOutput[i], ourOutput[i + lookback], Epsilon);
        }
    }

    [Fact]
    public void Cmo_MatchesTulip_UpwardTrend()
    {
        // Steadily increasing prices
        double[] prices = new double[30];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + i * 2;
        }

        int period = 10;

        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [prices];
        double[] options = [period];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[prices.Length - lookback]];
        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipOutput = outputs[0];

        double[] ourOutput = new double[prices.Length];
        Cmo.Batch(prices, ourOutput, period);

        for (int i = 0; i < tulipOutput.Length; i++)
        {
            Assert.Equal(tulipOutput[i], ourOutput[i + lookback], Epsilon);
        }
    }

    [Fact]
    public void Cmo_MatchesTulip_DownwardTrend()
    {
        // Steadily decreasing prices
        double[] prices = new double[30];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 200 - i * 2;
        }

        int period = 10;

        var cmoIndicator = Tulip.Indicators.cmo;
        double[][] inputs = [prices];
        double[] options = [period];
        int lookback = cmoIndicator.Start(options);
        double[][] outputs = [new double[prices.Length - lookback]];
        cmoIndicator.Run(inputs, options, outputs);
        double[] tulipOutput = outputs[0];

        double[] ourOutput = new double[prices.Length];
        Cmo.Batch(prices, ourOutput, period);

        for (int i = 0; i < tulipOutput.Length; i++)
        {
            Assert.Equal(tulipOutput[i], ourOutput[i + lookback], Epsilon);
        }
    }

    [Fact]
    public void Cmo_MatchesTulip_MultiplePeriods()
    {
        double[] prices = new double[100];
        var random = new Random(42);
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + (random.NextDouble() - 0.5) * 20 + i * 0.05;
        }

        int[] periods = [5, 10, 14, 20, 30];

        foreach (int period in periods)
        {
            var cmoIndicator = Tulip.Indicators.cmo;
            double[][] inputs = [prices];
            double[] options = [period];
            int lookback = cmoIndicator.Start(options);
            double[][] outputs = [new double[prices.Length - lookback]];
            cmoIndicator.Run(inputs, options, outputs);
            double[] tulipOutput = outputs[0];

            double[] ourOutput = new double[prices.Length];
            Cmo.Batch(prices, ourOutput, period);

            for (int i = 0; i < tulipOutput.Length; i++)
            {
                Assert.Equal(tulipOutput[i], ourOutput[i + lookback], Epsilon);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Manual Calculation Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cmo_ManualCalculation_AllUpMoves()
    {
        // All upward moves
        double[] prices = [100, 101, 102, 103, 104, 105];
        int period = 5;

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, period);

        // After 5 periods: SumUp = 5, SumDown = 0
        // CMO = 100 * (5-0)/(5+0) = 100
        Assert.Equal(100.0, output[5], Epsilon);
    }

    [Fact]
    public void Cmo_ManualCalculation_AllDownMoves()
    {
        // All downward moves
        double[] prices = [105, 104, 103, 102, 101, 100];
        int period = 5;

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, period);

        // After 5 periods: SumUp = 0, SumDown = 5
        // CMO = 100 * (0-5)/(0+5) = -100
        Assert.Equal(-100.0, output[5], Epsilon);
    }

    [Fact]
    public void Cmo_ManualCalculation_EqualMoves()
    {
        // Equal up and down moves
        double[] prices = [100, 102, 100, 102, 100]; // up 2, down 2, up 2, down 2
        int period = 4;

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, period);

        // SumUp = 4, SumDown = 4
        // CMO = 100 * (4-4)/(4+4) = 0
        Assert.Equal(0.0, output[4], Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Streaming vs Batch Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cmo_StreamingMatchesBatch()
    {
        double[] prices = new double[100];
        var random = new Random(12345);
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + (random.NextDouble() - 0.5) * 30 + Math.Sin(i * 0.2) * 5;
        }

        int period = 14;

        // Batch calculation
        double[] batchOutput = new double[prices.Length];
        Cmo.Batch(prices, batchOutput, period);

        // Streaming calculation
        var cmo = new Cmo(period);
        for (int i = 0; i < prices.Length; i++)
        {
            var result = cmo.Update(new TValue(DateTime.Now.Ticks + i, prices[i]));
            Assert.Equal(batchOutput[i], result.Value, Epsilon);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Edge Case Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cmo_NoChange_ReturnsZero()
    {
        double[] prices = [100, 100, 100, 100, 100, 100];
        int period = 5;

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, period);

        // No movement = 0
        Assert.Equal(0.0, output[5]);
    }

    [Fact]
    public void Cmo_RangeIsBounded()
    {
        double[] prices = new double[100];
        var random = new Random(54321);
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + (random.NextDouble() - 0.5) * 50;
        }

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, 14);

        // All values should be in [-100, 100] range
        for (int i = 14; i < output.Length; i++)
        {
            Assert.True(output[i] >= -100.0 && output[i] <= 100.0,
                $"CMO at index {i} = {output[i]} is out of range [-100, 100]");
        }
    }

    [Fact]
    public void Cmo_AlternatingMoves_ConvergesToZero()
    {
        // Alternating pattern with equal magnitude
        double[] prices = new double[50];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100 + (i % 2 == 0 ? 0 : 2); // 100, 102, 100, 102, ...
        }

        double[] output = new double[prices.Length];
        Cmo.Batch(prices, output, 10);

        // Result should be close to 0 for balanced oscillation
        Assert.True(Math.Abs(output[^1]) < 20,
            $"CMO for alternating pattern should be near zero, got {output[^1]}");
    }
}
