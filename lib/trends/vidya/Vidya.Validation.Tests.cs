using QuanTAlib;
using Xunit;

namespace Trends;

public class VidyaValidationTests
{
    [Fact]
    public void ValidateAgainstReference()
    {
        // Note: Tulip's VIDYA implementation uses Standard Deviation ratio (1992 version),
        // while QuanTAlib uses Chande Momentum Oscillator (1994 version).
        // Therefore, we cannot validate against Tulip.
        // We validate against a simple, readable reference implementation of the CMO-based VIDYA.
        
        var feed = new GBM();
        var data = feed.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var period = 14;
        
        // QuanTAlib
        var vidya = new Vidya(period);
        var qResults = new List<double>();
        foreach (var item in data)
        {
            qResults.Add(vidya.Update(new TValue(item.Time, item.Close)).Value);
        }
        
        // Reference Implementation
        var refResults = CalculateVidyaReference(data, period);
        
        // Compare
        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(refResults[i], qResults[i], 1e-9);
        }
    }
    
    private static List<double> CalculateVidyaReference(TBarSeries data, int period)
    {
        var results = new List<double>();
        var prices = data.Select(x => x.Close).ToList();
        double alpha = 2.0 / (period + 1);
        
        double prevVidya = 0;
        
        for (int i = 0; i < prices.Count; i++)
        {
            if (i == 0)
            {
                results.Add(prices[i]);
                prevVidya = prices[i];
                continue;
            }
            
            double sumUp = 0;
            double sumDown = 0;
            
            var changes = new List<double>();
            for (int j = 1; j <= i; j++)
            {
                changes.Add(prices[j] - prices[j-1]);
            }
            
            var recentChanges = changes.TakeLast(period).ToList();
            
            sumUp = recentChanges.Where(x => x > 0).Sum();
            sumDown = recentChanges.Where(x => x < 0).Select(x => -x).Sum();
            
            double sum = sumUp + sumDown;
            double vi = 0;
            if (sum > 0)
            {
                vi = Math.Abs(sumUp - sumDown) / sum;
            }
            
            double dynamicAlpha = alpha * vi;
            double currentVidya = dynamicAlpha * prices[i] + (1 - dynamicAlpha) * prevVidya;
            
            results.Add(currentVidya);
            prevVidya = currentVidya;
        }
        
        return results;
    }
}
