using QuanTAlib;

// Example: Loading and streaming IBM daily data from CSV
var csvPath = "daily_IBM.csv";

// Create feed from CSV file
var feed = new CsvFeed(csvPath);

Console.WriteLine("=== CSV Feed Example: IBM Daily Data ===\n");

// Example 1: Stream through first 5 bars
Console.WriteLine("1. Streaming first 5 bars:");
for (int i = 0; i < 5; i++)
{
    var bar = feed.Next(isNew: true);
    Console.WriteLine($"   {bar.AsDateTime:yyyy-MM-dd}: O={bar.Open:F2}, H={bar.High:F2}, L={bar.Low:F2}, C={bar.Close:F2}, V={bar.Volume:F0}");
}

// Example 2: Fetch a specific date range
Console.WriteLine("\n2. Fetching 10 bars starting from July 2025:");
var startTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
var series = feed.Fetch(10, startTime, TimeSpan.FromDays(1));
Console.WriteLine($"   Retrieved {series.Count} bars");
foreach (var bar in series)
{
    Console.WriteLine($"   {bar.AsDateTime:yyyy-MM-dd}: Close={bar.Close:F2}");
}

// Example 3: Demonstrate isNew parameter
Console.WriteLine("\n3. Demonstrating isNew parameter (new bar vs update):");
var bar1 = feed.Next(isNew: true);
Console.WriteLine($"   New bar: {bar1.AsDateTime:yyyy-MM-dd} Close={bar1.Close:F2}");

var bar1Update = feed.Next(isNew: false);
Console.WriteLine($"   Update (same bar): {bar1Update.AsDateTime:yyyy-MM-dd} Close={bar1Update.Close:F2}");

var bar2 = feed.Next(isNew: true);
Console.WriteLine($"   Next bar: {bar2.AsDateTime:yyyy-MM-dd} Close={bar2.Close:F2}");

// Example 4: Working with TBarSeries views
Console.WriteLine("\n4. Accessing OHLCV components via TSeries:");
var batch = feed.Fetch(5, startTime, TimeSpan.FromDays(1));
Console.WriteLine($"   Close prices: [{string.Join(", ", batch.Close.Take(5).Select(c => c.Value.ToString("F2")))}]");
Console.WriteLine($"   High prices: [{string.Join(", ", batch.High.Take(5).Select(h => h.Value.ToString("F2")))}]");
Console.WriteLine($"   Volumes: [{string.Join(", ", batch.Volume.Take(5).Select(v => v.Value.ToString("F0")))}]");

Console.WriteLine("\n=== Example Complete ===");
