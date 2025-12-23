using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

namespace QuanTAlib.Tests;

public class AroonOscOoplesReproTests
{
    [Fact(Skip = "Ooples implementation deviates significantly from standard (TA-Lib, Tulip, Skender, QuanTAlib)")]
    public void Ooples_AroonOsc_Convergence_Check()
    {
        // Generate a long series of data to check for convergence
        int barsCount = 5000;
        var gbm = new GBM();
        var bars = gbm.Fetch(barsCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // 1. QuanTAlib Calculation
        var aroonOsc = new AroonOsc(14);
        var qResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            qResults.Add(aroonOsc.Update(bars[i]).Value);
        }

        // 2. Ooples Calculation
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time),
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var ooplesResults = stockData.CalculateAroonOscillator(14).OutputValues["Aroon"].ToList();

        // Check count
        Assert.Equal(barsCount, ooplesResults.Count); // Verify if Ooples returns full length

        // 3. Compare at the end
        // We check the last 100 bars to see if they are close
        double maxDiff = 0;
        double sumDiff = 0;
        int count = 0;

        for (int i = barsCount - 100; i < barsCount; i++)
        {
            double qVal = qResults[i];
            double oVal = ooplesResults[i];
            double diff = Math.Abs(qVal - oVal);
            
            if (double.IsNaN(qVal) || double.IsNaN(oVal)) continue;

            maxDiff = Math.Max(maxDiff, diff);
            sumDiff += diff;
            count++;
        }

        double avgDiff = count > 0 ? sumDiff / count : 0;

        // If it converges, avgDiff should be very small (e.g. < 1e-6)
        // If it doesn't, it will be larger.
        // Based on previous findings ("deviates significantly"), we expect this to fail if we assert strict equality.
        // But the user asks "is it converging?".
        
        // We'll output the values to the test result message if it fails assertion
        Assert.True(avgDiff < 0.1, $"Aroon Oscillator did not converge after {barsCount} bars. Avg Diff: {avgDiff}, Max Diff: {maxDiff}");
    }
}
