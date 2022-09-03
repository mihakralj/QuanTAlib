namespace QuanTAlib;
using System;
using System.Text.Json;

/* <summary>
Alphavantage - Free API to collect quotes for stock, Forex and crypto. It requires a (free) API key
    Get API key at https://www.alphavantage.co/support/#api-key
    Parameters:
        Symbol: stock ("AAPL"), crypto ("BTC") or forex pair (divided by dash: "USD-EUR")
        Extended: if true, return 2,000 rows. if false, return 100 rows
        Interval: enum with options of Month, Week, Day, Hour, Min30, Min15, Min5, Min1
        APIkey: unique Alphavantage API key

</summary> */

public class Alphavantage_Feed : TBars
{
    public enum Interval { Month, Week, Day, Hour, Min30, Min15, Min5, Min1}
    public Alphavantage_Feed(string Symbol = "IBM", bool Extended = false, Interval Interval = Interval.Day, string APIkey = "demo")
    {

        string outputsize = "compact";
        if (Extended) { outputsize = "full"; }
        System.Net.Http.HttpClient client = new();
        JsonElement json = new();
        var tokens = Symbol.Split('-');
        if (tokens.Length > 1)
        {
            string req = "https://www.alphavantage.co/query?function=FX" + GetInterval(Interval) + "&from_symbol=" + tokens[0] + "&to_symbol=" + tokens[1] + "&outputsize=" + outputsize + "&apikey=" + APIkey;
            var msg = client.GetStringAsync(req).Result;
            var jres = JsonSerializer.Deserialize<JsonDocument>(msg).RootElement;
            switch (Interval)
            {
                case Interval.Month: jres.TryGetProperty("Time Series FX (Monthly)", out json); break;
                case Interval.Week: jres.TryGetProperty("Time Series FX (Weekly)", out json); break;
                case Interval.Day: jres.TryGetProperty("Time Series FX (Daily)", out json); break;
                case Interval.Hour: jres.TryGetProperty("Time Series FX (60min)", out json); break;
                case Interval.Min30: jres.TryGetProperty("Time Series FX (30min)", out json); break;
                case Interval.Min15: jres.TryGetProperty("Time Series FX (15min)", out json); break;
                case Interval.Min5: jres.TryGetProperty("Time Series FX (5min)", out json); break;
                case Interval.Min1: jres.TryGetProperty("Time Series FX (1min)", out json); break;
            }

        }
        if (json.ValueKind == JsonValueKind.Undefined)
        {
            string req = "https://www.alphavantage.co/query?function=TIME_SERIES" + GetInterval(Interval) + "&symbol=" + Symbol + "&outputsize=" + outputsize + "&apikey=" + APIkey;
            var msg = client.GetStringAsync(req).Result;
            var jres = JsonSerializer.Deserialize<JsonDocument>(msg).RootElement;
            switch (Interval)
            {
                case Interval.Month: jres.TryGetProperty("Monthly Time Series", out json); break;
                case Interval.Week: jres.TryGetProperty("Weekly Time Series", out json); break;
                case Interval.Day: jres.TryGetProperty("Time Series (Daily)", out json); break;
                case Interval.Hour: jres.TryGetProperty("Time Series (60min)", out json); break;
                case Interval.Min30: jres.TryGetProperty("Time Series (30min)", out json); break;
                case Interval.Min15: jres.TryGetProperty("Time Series (15min)", out json); break;
                case Interval.Min5: jres.TryGetProperty("Time Series (5min)", out json); break;
                case Interval.Min1: jres.TryGetProperty("Time Series (1min)", out json); break;
            }
        }
        if (json.ValueKind == JsonValueKind.Undefined)
        {
            string req;
            if ((int)Interval < 3) { req = "https://www.alphavantage.co/query?function=DIGITAL_CURRENCY" + GetInterval(Interval) + "&symbol=" + Symbol + "&market=USD&&outputsize=" + outputsize + "&apikey=" + APIkey; }
            else { req = "https://www.alphavantage.co/query?function=CRYPTO" + GetInterval(Interval) + "&symbol=" + Symbol + "&market=USD&&outputsize=" + outputsize + "&apikey=" + APIkey; }
            var msg = client.GetStringAsync(req).Result;
            var jres = JsonSerializer.Deserialize<JsonDocument>(msg).RootElement;
            switch (Interval)
            {
                case Interval.Month: jres.TryGetProperty("Time Series (Digital Currency Monthly)", out json); break;
                case Interval.Week: jres.TryGetProperty("Time Series (Digital Currency Weekly)", out json); break;
                case Interval.Day: jres.TryGetProperty("Time Series (Digital Currency Daily)", out json); break;
                case Interval.Hour: jres.TryGetProperty("Time Series Crypto (60min)", out json); break;
                case Interval.Min30: jres.TryGetProperty("Time Series Crypto (30min)", out json); break;
                case Interval.Min15: jres.TryGetProperty("Time Series Crypto (15min)", out json); break;
                case Interval.Min5: jres.TryGetProperty("Time Series Crypto (5min)", out json); break;
                case Interval.Min1: jres.TryGetProperty("Time Series Crypto (1min)", out json); break;
            }
        }
        if (json.ValueKind != JsonValueKind.Undefined)
        {
            foreach (var val in json.EnumerateObject()) { base.Add(GetOHLC(val)); }
        }

    }
    private static (DateTime t, double o, double h, double l, double c, double v) GetOHLC(JsonProperty json)
    {
        double o, h, l, c, v;
        o = h = l = c = v = 0;
        DateTime date = Convert.ToDateTime(json.Name);
        foreach (var val in json.Value.EnumerateObject())
        {
            switch (val.Name)
            {
                case "1. open": o = Convert.ToDouble(val.Value.ToString()); break;
                case "1b. open (USD)": o = Convert.ToDouble(val.Value.ToString()); break;
                case "2. high": h = Convert.ToDouble(val.Value.ToString()); break;
                case "2b. high (USD)": h = Convert.ToDouble(val.Value.ToString()); break;
                case "3. low": l = Convert.ToDouble(val.Value.ToString()); break;
                case "3b. low (USD)": l = Convert.ToDouble(val.Value.ToString()); break;
                case "4. close": c = Convert.ToDouble(val.Value.ToString()); break;
                case "4b. close (USD)": c = Convert.ToDouble(val.Value.ToString()); break;
                case "5. adjusted close": c = Convert.ToDouble(val.Value.ToString()); break;
                case "5. volume": v = Convert.ToDouble(val.Value.ToString()); break;
                case "6. volume": v = Convert.ToDouble(val.Value.ToString()); break;
            }
        }
        return (date, o, h, l, c, v);
    }

	private static string GetInterval(Interval interval = Interval.Day) => interval switch
	{
		Interval.Month => "_MONTHLY",
		Interval.Week => "_WEEKLY",
		Interval.Day => "_DAILY",
		Interval.Hour => "_INTRADAY&interval=60min",
		Interval.Min30 => "_INTRADAY&interval=30min",
		Interval.Min15 => "_INTRADAY&interval=15min",
		Interval.Min5 => "_INTRADAY&interval=5min",
		Interval.Min1 => "_INTRADAY&interval=1min",
		_ => "_DAILY"
	};
}

