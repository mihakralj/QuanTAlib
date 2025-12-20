using System;
using System.Collections.Generic;
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
}
