using System;
using Xunit;

namespace QuanTAlib.Tests;

public class CovarianceValidationTests
{
    [Fact]
    public void Covariance_Matches_ManualCalculation()
    {
        // Arrange
        int period = 10;
        var cov = new Covariance(period, isPopulation: false);
        var r = new Random(123);
        
        double[] x = new double[100];
        double[] y = new double[100];
        for (int i = 0; i < 100; i++)
        {
            x[i] = r.NextDouble() * 100;
            y[i] = r.NextDouble() * 100;
            cov.Update(x[i], y[i]);
            
            if (i >= period - 1)
            {
                // Manual calculation for last 'period' items
                double sumX = 0;
                double sumY = 0;
                for (int j = 0; j < period; j++)
                {
                    sumX += x[i - j];
                    sumY += y[i - j];
                }
                double meanX = sumX / period;
                double meanY = sumY / period;
                
                double sumProd = 0;
                for (int j = 0; j < period; j++)
                {
                    sumProd += (x[i - j] - meanX) * (y[i - j] - meanY);
                }
                
                double expected = sumProd / (period - 1);
                Assert.Equal(expected, cov.Last.Value, precision: 8);
            }
        }
    }

    [Fact]
    public void Covariance_Population_Matches_ManualCalculation()
    {
        // Arrange
        int period = 10;
        var cov = new Covariance(period, isPopulation: true);
        var r = new Random(456);
        
        double[] x = new double[100];
        double[] y = new double[100];
        for (int i = 0; i < 100; i++)
        {
            x[i] = r.NextDouble() * 100;
            y[i] = r.NextDouble() * 100;
            cov.Update(x[i], y[i]);
            
            if (i >= period - 1)
            {
                // Manual calculation for last 'period' items
                double sumX = 0;
                double sumY = 0;
                for (int j = 0; j < period; j++)
                {
                    sumX += x[i - j];
                    sumY += y[i - j];
                }
                double meanX = sumX / period;
                double meanY = sumY / period;
                
                double sumProd = 0;
                for (int j = 0; j < period; j++)
                {
                    sumProd += (x[i - j] - meanX) * (y[i - j] - meanY);
                }
                
                double expected = sumProd / period;
                Assert.Equal(expected, cov.Last.Value, precision: 8);
            }
        }
    }
}
