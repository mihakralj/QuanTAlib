using Xunit;

namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class DemaTests
{
    [Fact]
    public void Dema_Matches_ManualCalculation()
    {
        // Arrange
        int period = 10;
        var dema = new Dema(period);
        var ema1 = new Ema(period);
        var ema2 = new Ema(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var dVal = dema.Update(tVal);
            
            var e1Val = ema1.Update(tVal);
            var e2Val = ema2.Update(e1Val);
            double expected = 2 * e1Val.Value - e2Val.Value;

            Assert.Equal(expected, dVal.Value, 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_ObjectUpdate()
    {
        // Arrange
        int period = 10;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Act
        var demaSeries = Dema.Calculate(source, period);
        var demaObj = new Dema(period);
        
        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            var val = demaObj.Update(source[i]);
            Assert.Equal(val.Value, demaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Matches_ObjectUpdate()
    {
        // Arrange
        int period = 10;
        int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Act
        Dema.Calculate(source, output, period);
        var demaObj = new Dema(period);

        // Assert
        for (int i = 0; i < count; i++)
        {
            var val = demaObj.Update(new TValue(DateTime.Now, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Alpha_Constructor_Matches_Period_Constructor()
    {
        // Arrange
        int period = 10;
        double alpha = 2.0 / (period + 1);
        var demaPeriod = new Dema(period);
        var demaAlpha = new Dema(alpha);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var pVal = demaPeriod.Update(tVal);
            var aVal = demaAlpha.Update(tVal);

            Assert.Equal(pVal.Value, aVal.Value, 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Alpha_Matches_ObjectUpdate()
    {
        // Arrange
        double alpha = 0.15;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Act
        var demaSeries = Dema.Calculate(source, alpha);
        var demaObj = new Dema(alpha);
        
        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            var val = demaObj.Update(source[i]);
            Assert.Equal(val.Value, demaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Alpha_Matches_ObjectUpdate()
    {
        // Arrange
        double alpha = 0.15;
        int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Act
        Dema.Calculate(source, output, alpha);
        var demaObj = new Dema(alpha);

        // Assert
        for (int i = 0; i < count; i++)
        {
            var val = demaObj.Update(new TValue(DateTime.Now, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }
    [Fact]
    public void Dema_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Dema.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Dema.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Dema(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Dema(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
