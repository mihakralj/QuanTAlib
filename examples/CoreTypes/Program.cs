using System;
using QuanTAlib;

namespace CoreTypesExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("QuanTAlib Core Types Example");
            Console.WriteLine("============================");

            // 1. TValue Example
            Console.WriteLine("\n1. TValue Usage");
            long now = DateTime.UtcNow.Ticks;
            var val1 = new TValue(now, 100.5);
            Console.WriteLine($"Created TValue: Time={val1.AsDateTime}, Value={val1.Value}");

            // 2. TSeries Example (Streaming)
            Console.WriteLine("\n2. TSeries Usage (Streaming)");
            var series = new TSeries();

            // Add new values
            series.Add(now, 10.0, isNew: true);
            Console.WriteLine($"Added 10.0 (New): Count={series.Count}, Last={series.Last.Value}");

            // Update last value (streaming update)
            series.Add(now, 11.0, isNew: false);
            Console.WriteLine($"Updated to 11.0 (Update): Count={series.Count}, Last={series.Last.Value}");

            // Add another new value
            series.Add(now + TimeSpan.TicksPerMinute, 12.0, isNew: true);
            Console.WriteLine($"Added 12.0 (New): Count={series.Count}, Last={series.Last.Value}");

            // 3. TBar Example
            Console.WriteLine("\n3. TBar Usage");
            var bar1 = new TBar(now, 100, 105, 95, 102, 1000);
            Console.WriteLine($"Created TBar: {bar1}");
            Console.WriteLine($"Computed HL2: {bar1.HL2}");
            Console.WriteLine($"TValue Access: O={bar1.O}, H={bar1.H}, L={bar1.L}, C={bar1.C}, V={bar1.V}");

            // 4. TBarSeries Example
            Console.WriteLine("\n4. TBarSeries Usage");
            var bars = new TBarSeries();

            // Add a bar
            bars.Add(bar1, isNew: true);
            Console.WriteLine($"Added Bar 1: Count={bars.Count}, Close={bars.Last.Close}");

            // Update the bar (e.g. price changed within the same minute)
            var bar1Update = new TBar(now, 100, 106, 95, 104, 1500);
            bars.Add(bar1Update, isNew: false);
            Console.WriteLine($"Updated Bar 1: Count={bars.Count}, Close={bars.Last.Close}, High={bars.Last.High}");

            // Accessing Views (Zero-Copy)
            Console.WriteLine("\n5. TBarSeries Views (Zero-Copy)");
            Console.WriteLine($"Bars Count: {bars.Count}");
            Console.WriteLine($"Close Series Count: {bars.Close.Count}");
            Console.WriteLine($"Close Series Last: {bars.Close.Last.Value}");

            // Verify view updates automatically
            Console.WriteLine("Adding new bar...");
            bars.Add(now + TimeSpan.TicksPerMinute, 104, 108, 103, 107, 2000, isNew: true);
            Console.WriteLine($"Bars Count: {bars.Count}");
            Console.WriteLine($"Close Series Count: {bars.Close.Count} (Should match Bars Count)");
            Console.WriteLine($"Close Series Last: {bars.Close.Last.Value} (Should be 107)");
            Console.WriteLine($"Alias Access: C.Last={bars.C.Last.Value}");
            Console.WriteLine($"Direct Last Access: LastClose={bars.LastClose}, LastTime={bars.LastTime}");

            // 6. GBM Generator Example
            Console.WriteLine("\n6. GBM Generator Usage");
            var gbm = new GBM(startPrice: 100.0);

            // 6a. Batch generation
            long startTime = DateTime.UtcNow.Ticks;
            var interval = TimeSpan.FromMinutes(1);
            var randomBars = gbm.Fetch(5, startTime, interval);
            Console.WriteLine($"Generated {randomBars.Count} bars in batch:");
            for (int i = 0; i < randomBars.Count; i++)
                Console.WriteLine($"  Bar {i}: C={randomBars[i].Close:F2}");

            // 6b. Individual streaming bars
            Console.WriteLine("\n6b. Streaming individual bars:");
            var streamBar = gbm.Next(isNew: true);
            Console.WriteLine($"  New bar: C={streamBar.Close:F2}");

            // Simulate 3 intra-bar updates
            for (int i = 0; i < 3; i++)
            {
                streamBar = gbm.Next(isNew: false);
                Console.WriteLine($"  Update {i + 1}: C={streamBar.Close:F2}, H={streamBar.High:F2}, L={streamBar.Low:F2}");
            }

            // Finalize and start new bar
            streamBar = gbm.Next(isNew: true);
            Console.WriteLine($"  New bar: C={streamBar.Close:F2}");

            // 6c. Manual streaming into TBarSeries
            Console.WriteLine("\n6c. Manual streaming into TBarSeries:");
            var streamSeries = new TBarSeries();
            var bar = gbm.Next(isNew: true);
            streamSeries.Add(bar, isNew: true);
            Console.WriteLine($"  Added bar: Count={streamSeries.Count}, C={streamSeries.LastClose:F2}");

            for (int i = 0; i < 2; i++)
            {
                bar = gbm.Next(isNew: false);
                streamSeries.Add(bar, isNew: false);
                Console.WriteLine($"  Update {i + 1}: Count={streamSeries.Count}, C={streamSeries.LastClose:F2}");
            }

            bar = gbm.Next(isNew: true);
            streamSeries.Add(bar, isNew: true);
            Console.WriteLine($"  Added bar: Count={streamSeries.Count}, C={streamSeries.LastClose:F2}");

            Console.WriteLine("\nExample complete.");
        }
    }
}
