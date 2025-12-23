using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdxOoplesReproTests
{
    [Fact]
    public void CalculateTrueRange_SimplifiedLogic()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var trList = new List<double>();
        double prevClose = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            double currentHigh = bars[i].High;
            double currentLow = bars[i].Low;
            double currentClose = bars[i].Close;

            // CalculateTrueRange
            // Ooples logic: prevClose is 0 for the first bar
            // TR = Max(H-L, |H-prevClose|, |L-prevClose|)
            // Simplified: Since prevClose is 0 at i=0, the formula works for all i.
            double tr = Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - prevClose), Math.Abs(currentLow - prevClose)));
            
            trList.Add(Math.Round(tr, 4));
            prevClose = currentClose;
        }
        
        Assert.NotEmpty(trList);
        Assert.Equal(bars.Count, trList.Count);
    }

    [Fact]
    public void Ooples_WWMA_Initialization_Causes_Deviation()
    {
        // This test reproduces the Ooples WWMA logic provided by the user
        // and demonstrates why it deviates from standard RMA (Wilder's Smoothing).
        
        int length = 14;
        var input = new List<double>();
        for (int i = 0; i < 100; i++) input.Add(100.0); // Constant input for clarity

        // 1. Ooples Implementation (from user feedback)
        var ooplesWwma = new List<double>();
        double k = 1.0 / length;
        double prevWwma = 0; // Ooples initializes with 0 (LastOrDefault on empty list)

        for (int i = 0; i < input.Count; i++)
        {
            double currentValue = input[i];
            // Ooples logic: wwma = (currentValue * k) + (prevWwma * (1 - k))
            double wwma = (currentValue * k) + (prevWwma * (1.0 - k));
            ooplesWwma.Add(wwma);
            prevWwma = wwma;
        }

        // 2. Standard RMA (QuanTAlib/TA-Lib)
        // Standard RMA usually initializes with SMA of first N periods
        var rma = new Rma(length);
        var standardRma = new List<double>();
        for (int i = 0; i < input.Count; i++)
        {
            standardRma.Add(rma.Update(new TValue(DateTime.UtcNow, input[i])).Value);
        }

        // Verification
        // At index 0:
        // Ooples: (100 * 1/14) + (0 * 13/14) = 7.14
        // Standard: 0 (or 100 if initialized with value, or SMA after N periods)
        // QuanTAlib RMA returns 0 until period N, then SMA, then RMA.
        
        // Let's check the value at index 50 (well past warmup)
        // Ooples should be slowly converging to 100 from 0.
        // Standard should be 100.
        
        double ooplesVal = ooplesWwma[50];
        double standardVal = standardRma[50];

        // Ooples value will be significantly less than 100 because it started at 0
        // and decays very slowly (alpha = 1/14).
        Assert.True(ooplesVal < 99.0, $"Ooples value {ooplesVal} should be significantly lower than input 100 due to 0-initialization");
        Assert.Equal(100.0, standardVal, 0.001); // Standard RMA of constant 100 is 100
        
        // This confirms why ADX (which uses RMA) is significantly different.
    }

    [Fact]
    public void Ooples_WWMA_Converges_With_Enough_Bars()
    {
        // Verify if Ooples WWMA eventually converges to the correct value
        int length = 14;
        int bars = 5000; // Try with a large number of bars
        var input = new List<double>();
        for (int i = 0; i < bars; i++) input.Add(100.0);

        // Ooples Implementation
        var ooplesWwma = new List<double>();
        double k = 1.0 / length;
        double prevWwma = 0;

        for (int i = 0; i < input.Count; i++)
        {
            double currentValue = input[i];
            double wwma = (currentValue * k) + (prevWwma * (1.0 - k));
            ooplesWwma.Add(wwma);
            prevWwma = wwma;
        }

        // Check convergence at the end
        double finalValue = ooplesWwma.Last();
        double expectedValue = 100.0;
        
        // After 5000 bars, the error should be negligible
        // Error decay is (13/14)^5000 which is effectively 0
        Assert.Equal(expectedValue, finalValue, 0.0001);
        
        // Check how long it takes to get within 1% (value > 99.0)
        int barsToConverge = ooplesWwma.FindIndex(x => x > 99.0);
        Assert.True(barsToConverge > 0);
        // It takes significant time to recover from 0-initialization
        // Formula: 100 * (1 - (13/14)^n) > 99 => (13/14)^n < 0.01
        // n > log(0.01) / log(13/14) ≈ -4.6 / -0.032 ≈ 143 bars
        Assert.InRange(barsToConverge, 60, 150); 
    }
}
