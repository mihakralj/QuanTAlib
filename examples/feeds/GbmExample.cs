using System;
using QuanTAlib;

namespace FeedsExample;

class GbmExample
{
    static void Main(string[] args)
    {
        Console.WriteLine("QuanTAlib GBM Feed Example");
        Console.WriteLine("===========================\n");

        // Create GBM generator
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        var series = new TBarSeries();

        // 1. Batch generation: 20 bars with 1-hour interval, starting 24 hours ago
        Console.WriteLine("1. Batch Generation (20 bars, 1-hour interval)");
        Console.WriteLine("------------------------------------------------");
        
        long startTime = DateTime.UtcNow.AddHours(-24).Ticks;
        var interval = TimeSpan.FromHours(1);
        var batchBars = gbm.Fetch(20, startTime, interval);
        
        // Add batch to series
        for (int i = 0; i < batchBars.Count; i++)
        {
            series.Add(batchBars[i], isNew: true);
        }
        
        Console.WriteLine($"Generated {batchBars.Count} bars");
        Console.WriteLine($"First bar:  Time={new DateTime(batchBars[0].Time):yyyy-MM-dd HH:mm:ss}, Close={batchBars[0].Close:F2}");
        Console.WriteLine($"Last bar:   Time={new DateTime(batchBars[19].Time):yyyy-MM-dd HH:mm:ss}, Close={batchBars[19].Close:F2}");
        Console.WriteLine($"Series has {series.Count} bars\n");

        // 2. Streaming: Add 4 more bars (1 new + 3 intra-bar updates each)
        Console.WriteLine("2. Streaming Generation (4 new bars with intra-bar updates)");
        Console.WriteLine("------------------------------------------------------------");
        
        for (int barNum = 1; barNum <= 4; barNum++)
        {
            Console.WriteLine($"\nBar #{barNum + 20}:");
            
            // New bar
            var bar = gbm.Next(isNew: true);
            series.Add(bar, isNew: true);
            Console.WriteLine($"  New:      Time={new DateTime(bar.Time):HH:mm:ss}, O={bar.Open:F2}, H={bar.High:F2}, L={bar.Low:F2}, C={bar.Close:F2}");
            
            // Three intra-bar updates
            for (int update = 1; update <= 3; update++)
            {
                bar = gbm.Next(isNew: false);
                series.Add(bar, isNew: false);
                Console.WriteLine($"  Update {update}: Time={new DateTime(bar.Time):HH:mm:ss}, O={bar.Open:F2}, H={bar.High:F2}, L={bar.Low:F2}, C={bar.Close:F2}");
            }
        }

        // 3. Summary
        Console.WriteLine("\n3. Final Summary");
        Console.WriteLine("----------------");
        Console.WriteLine($"Total bars in series: {series.Count}");
        Console.WriteLine($"Expected: 24 bars (20 batch + 4 streaming)");
        Console.WriteLine($"\nFirst bar:  Time={series[0].AsDateTime:yyyy-MM-dd HH:mm:ss}, Close={series[0].Close:F2}");
        Console.WriteLine($"Last bar:   Time={series.Last.AsDateTime:yyyy-MM-dd HH:mm:ss}, Close={series.Last.Close:F2}");
        Console.WriteLine($"\nPrice change: {series.Last.Close - series[0].Close:F2} ({(series.Last.Close / series[0].Close - 1) * 100:F2}%)");
        
        // Statistics using SIMD
        var closeValues = series.Close.Values;
        Console.WriteLine($"\nStatistics (Close prices):");
        Console.WriteLine($"  Average: {closeValues.AverageSIMD():F2}");
        Console.WriteLine($"  Min:     {closeValues.MinSIMD():F2}");
        Console.WriteLine($"  Max:     {closeValues.MaxSIMD():F2}");
        Console.WriteLine($"  StdDev:  {closeValues.StdDevSIMD():F2}");

        Console.WriteLine("\nExample complete.");
    }
}
