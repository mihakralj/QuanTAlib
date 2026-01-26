namespace QuanTAlib.Tests;

public class DsmaTests
{
    [Fact]
    public void Dsma_ConstructorValidation_ThrowsOnInvalidPeriod()
    {
        // Arrange & Act & Assert
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(1));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(0));
        Assert.Equal("period", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(-5));
        Assert.Equal("period", ex3.ParamName);
    }

    [Fact]
    public void Dsma_ConstructorValidation_ThrowsOnInvalidScaleFactor()
    {
        // Arrange & Act & Assert
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(10, 0.005));
        Assert.Equal("scaleFactor", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(10, 0.95));
        Assert.Equal("scaleFactor", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(10, -0.1));
        Assert.Equal("scaleFactor", ex3.ParamName);

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsma(10, 1.5));
        Assert.Equal("scaleFactor", ex4.ParamName);
    }

    [Fact]
    public void Dsma_ConstructorValidation_AcceptsValidParameters()
    {
        // Arrange & Act
        var dsma1 = new Dsma(2, 0.01);
        var dsma2 = new Dsma(100, 0.9);
        var dsma3 = new Dsma(25, 0.5);

        // Assert
        Assert.NotNull(dsma1);
        Assert.NotNull(dsma2);
        Assert.NotNull(dsma3);
        Assert.Equal("Dsma(2,0.01)", dsma1.Name);
        Assert.Equal("Dsma(100,0.90)", dsma2.Name);
        Assert.Equal("Dsma(25,0.50)", dsma3.Name);
    }

    [Fact]
    public void Dsma_BasicCalculation_ReturnsExpectedValues()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Act
        TValue result = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = dsma.Update(new TValue(bar.Time, bar.Close));
        }

        // Assert
        Assert.NotEqual(0.0, result.Value);
        Assert.True(double.IsFinite(result.Value));
        Assert.True(dsma.IsHot);
    }

    [Fact]
    public void Dsma_Properties_AccessibleAndCorrect()
    {
        // Arrange
        var dsma = new Dsma(period: 10, scaleFactor: 0.6);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 100);

        // Act
        for (int i = 0; i < 15; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close));
        }

        // Assert
        Assert.NotEqual(default, dsma.Last);
        Assert.True(dsma.IsHot);
        Assert.Equal(10, dsma.WarmupPeriod);
        Assert.Equal("Dsma(10,0.60)", dsma.Name);
    }

    [Fact]
    public void Dsma_StateAndBarCorrection_IsNewTrue()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 50);

        // Act - Add values with isNew=true
        TValue last = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            last = dsma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Assert
        Assert.True(double.IsFinite(last.Value));
    }

    [Fact]
    public void Dsma_StateAndBarCorrection_IsNewFalse()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 60);

        // Act - Add first 9 values normally
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        var beforeCorrection = dsma.Last;

        // Update last bar multiple times
        var lastBar = gbm.Next(isNew: true);
        dsma.Update(new TValue(lastBar.Time, lastBar.Close), isNew: true);
        var firstUpdate = dsma.Last;

        dsma.Update(new TValue(lastBar.Time, lastBar.Close * 1.1), isNew: false);
        var corrected = dsma.Last;

        // Assert
        Assert.NotEqual(beforeCorrection.Value, firstUpdate.Value);
        Assert.NotEqual(firstUpdate.Value, corrected.Value);
    }

    [Fact]
    public void Dsma_IterativeCorrection_RestoresState()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 70);

        // Act - Process first 9 bars
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Process bar 10 with multiple corrections
        var lastBar = gbm.Next(isNew: true);
        var lastInput = new TValue(lastBar.Time, lastBar.Close);
        dsma.Update(lastInput, isNew: true);
        var original = dsma.Last.Value;

        dsma.Update(new TValue(lastBar.Time, lastBar.Close * 1.2), isNew: false);
        dsma.Update(new TValue(lastBar.Time, lastBar.Close * 0.8), isNew: false);
        dsma.Update(lastInput, isNew: false); // Restore to original

        var restored = dsma.Last.Value;

        // Assert - Should be very close to original
        Assert.Equal(original, restored, precision: 6);
    }

    [Fact]
    public void Dsma_Reset_ClearsState()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 80);

        // Act - Process data
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(dsma.IsHot);

        // Reset
        dsma.Reset();

        // Assert
        Assert.False(dsma.IsHot);
        Assert.Equal(default, dsma.Last);
    }

    [Fact]
    public void Dsma_WarmupPeriod_IsHotTransition()
    {
        // Arrange
        var period = 10;
        var dsma = new Dsma(period, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 90);

        // Act & Assert
        for (int i = 0; i < period - 1; i++)
        {
            var bar = gbm.Next(isNew: true);
            dsma.Update(new TValue(bar.Time, bar.Close));
            Assert.False(dsma.IsHot, $"Should not be hot at bar {i + 1}");
        }

        var lastBar = gbm.Next(isNew: true);
        dsma.Update(new TValue(lastBar.Time, lastBar.Close));
        Assert.True(dsma.IsHot, $"Should be hot at bar {period}");
    }

    [Fact]
    public void Dsma_RobustnessNaN_UsesLastValidValue()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 100);

        // Act - Process normal data
        TBar lastBar;
        for (int i = 0; i < 5; i++)
        {
            lastBar = gbm.Next(isNew: true);
            dsma.Update(new TValue(lastBar.Time, lastBar.Close));
        }

        // Get the last bar again after loop
        lastBar = gbm.Next(isNew: false);

        // Inject NaN
        var nanResult = dsma.Update(new TValue(lastBar.Time, double.NaN));

        // Assert - Should use last valid value (not propagate NaN)
        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Dsma_RobustnessInfinity_UsesLastValidValue()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 110);

        // Act - Process normal data
        TBar lastBar;
        for (int i = 0; i < 5; i++)
        {
            lastBar = gbm.Next(isNew: true);
            dsma.Update(new TValue(lastBar.Time, lastBar.Close));
        }

        // Get the last bar again after loop
        lastBar = gbm.Next(isNew: false);

        // Inject Infinity
        var infResult = dsma.Update(new TValue(lastBar.Time, double.PositiveInfinity));
        var negInfResult = dsma.Update(new TValue(lastBar.Time, double.NegativeInfinity));

        // Assert - Should use last valid value
        Assert.True(double.IsFinite(infResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Dsma_RobustnessBatchNaN_Handles()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 120);
        var series = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            double value;
            if (i == 10)
            {
                value = double.NaN;
            }
            else if (i == 15)
            {
                value = double.PositiveInfinity;
            }
            else
            {
                value = bar.Close;
            }
            series.Add(bar.Time, value);
        }

        // Act
        var result = Dsma.Batch(series, period: 5, scaleFactor: 0.5);

        // Assert
        Assert.Equal(20, result.Count);
        Assert.All(result.Values.ToArray(), val => Assert.True(double.IsFinite(val)));
    }

    [Fact]
    public void Dsma_ConsistencyBatchVsStreaming()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 130);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }
        var period = 10;
        var scale = 0.6;

        // Act - Batch
        var batchResult = Dsma.Batch(series, period, scale);

        // Act - Streaming
        var dsma = new Dsma(period, scale);
        var streamResult = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResult.Add(dsma.Update(series[i]).Value);
        }

        // Assert - All values should match
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamResult[i], precision: 10);
        }
    }

    [Fact]
    public void Dsma_ConsistencyBatchVsSpan()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 140);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }
        var values = series.Values.ToArray();
        var period = 10;
        var scale = 0.6;

        // Act - Batch (TSeries)
        var batchResult = Dsma.Batch(series, period, scale);

        // Act - Span
        var spanOutput = new double[values.Length];
        Dsma.Calculate(values, spanOutput, period, scale);

        // Assert
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], precision: 10);
        }
    }

    [Fact]
    public void Dsma_ConsistencyStreamingVsSpan()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 150);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }
        var values = series.Values.ToArray();
        var period = 10;
        var scale = 0.6;

        // Act - Streaming
        var dsma = new Dsma(period, scale);
        var streamResult = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResult.Add(dsma.Update(series[i]).Value);
        }

        // Act - Span
        var spanOutput = new double[values.Length];
        Dsma.Calculate(values, spanOutput, period, scale);

        // Assert
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(streamResult[i], spanOutput[i], precision: 10);
        }
    }

    [Fact]
    public void Dsma_ConsistencyEventing()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 160);
        var source = new TSeries();
        var period = 10;
        var scale = 0.6;

        var eventResults = new List<TValue>();
        var dsma = new Dsma(source, period, scale);
        dsma.Pub += (sender, in args) => eventResults.Add(args.Value);

        // Act
        var series = new TSeries();
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tval = new TValue(bar.Time, bar.Close);
            series.Add(tval);
            source.Add(tval);
        }

        // Assert
        Assert.Equal(30, eventResults.Count);

        // Compare with direct calculation
        var directDsma = new Dsma(period, scale);
        for (int i = 0; i < series.Count; i++)
        {
            var expected = directDsma.Update(series[i]).Value;
            Assert.Equal(expected, eventResults[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Dsma_SpanValidation_ThrowsOnShortOutput()
    {
        // Arrange
        var source = new double[100];
        var shortOutput = new double[50];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Dsma.Calculate(source, shortOutput, period: 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Dsma_SpanValidation_AcceptsEqualLength()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 170);
        var values = new double[50];
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            values[i] = bar.Close;
        }
        var output = new double[50];

        // Act
        Dsma.Calculate(values, output, period: 10, scaleFactor: 0.5);

        // Assert
        Assert.All(output, val => Assert.True(double.IsFinite(val)));
    }

    [Fact]
    public void Dsma_SpanValidation_AcceptsLongerOutput()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 180);
        var values = new double[50];
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            values[i] = bar.Close;
        }
        var output = new double[100];

        // Act
        Dsma.Calculate(values, output, period: 10, scaleFactor: 0.5);

        // Assert
        Assert.All(output.Take(50), val => Assert.True(double.IsFinite(val)));
    }

    [Fact]
    public void Dsma_SpanHandlesNaN()
    {
        // Arrange
        var values = new double[20];
        Array.Fill(values, 100.0);
        values[10] = double.NaN;
        var output = new double[20];

        // Act
        Dsma.Calculate(values, output, period: 5, scaleFactor: 0.5);

        // Assert
        Assert.All(output, val => Assert.True(double.IsFinite(val)));
    }

    [Fact]
    public void Dsma_Chainability_WorksWithPub()
    {
        // Arrange
        var source = new TSeries();
        var dsma = new Dsma(source, period: 5, scaleFactor: 0.5);
        var receivedEvents = 0;

        dsma.Pub += (sender, in args) => receivedEvents++;

        // Act
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 190);
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        // Assert
        Assert.Equal(10, receivedEvents);
    }

    [Fact]
    public void Dsma_DifferentScaleFactors_ProduceDifferentResults()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 200);
        var dsmaLow = new Dsma(period: 10, scaleFactor: 0.1);
        var dsmaHigh = new Dsma(period: 10, scaleFactor: 0.8);

        // Act
        TValue resultLow = default, resultHigh = default;
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tval = new TValue(bar.Time, bar.Close);
            resultLow = dsmaLow.Update(tval);
            resultHigh = dsmaHigh.Update(tval);
        }

        // Assert - Different scale factors should produce different results
        Assert.NotEqual(resultLow.Value, resultHigh.Value);
    }

    [Fact]
    public void Dsma_FirstBarInitialization()
    {
        // Arrange
        var dsma = new Dsma(period: 5, scaleFactor: 0.5);

        // Act
        var result = dsma.Update(new TValue(DateTime.UtcNow, 100.0));

        // Assert - First bar should equal input
        Assert.Equal(100.0, result.Value, precision: 10);
        Assert.False(dsma.IsHot);
    }

    [Fact]
    public void Dsma_Prime_PopulatesIndicator()
    {
        // Arrange
        var dsma = new Dsma(period: 10, scaleFactor: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 210);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            values[i] = bar.Close;
        }

        // Act
        dsma.Prime(values);

        // Assert
        Assert.True(dsma.IsHot);
        Assert.NotEqual(default, dsma.Last);
    }
}
