using Xunit;

namespace QuanTAlib.Tests;

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
        var r = new Random(123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            double val = r.NextDouble() * 100;
            var tVal = new TValue(DateTime.Now.AddMinutes(i), val);

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
        var r = new Random(123);
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.Now.AddMinutes(i), r.NextDouble() * 100));
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
        var r = new Random(123);
        for (int i = 0; i < count; i++)
        {
            source[i] = r.NextDouble() * 100;
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
        var r = new Random(123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            double val = r.NextDouble() * 100;
            var tVal = new TValue(DateTime.Now.AddMinutes(i), val);

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
        var r = new Random(123);
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.Now.AddMinutes(i), r.NextDouble() * 100));
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
        var r = new Random(123);
        for (int i = 0; i < count; i++)
        {
            source[i] = r.NextDouble() * 100;
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
}
