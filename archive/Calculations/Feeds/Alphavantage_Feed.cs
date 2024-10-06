namespace QuanTAlib;
using System;
using System.Text.Json;

/* <summary>
Alphavantage - Free API to collect 100 recent daily quotes. It requires a (free) API key
    Get API key at https://www.alphavantage.co/support/#api-key
    Parameters:
        Symbol: stock ("AAPL"),
        APIkey: unique Alphavantage API key

</summary> 
*/
public class Alphavantage_Feed : TBars {
    public enum Interval { Month, Week, Day, Hour, Min30, Min15, Min5, Min1 }
    public Alphavantage_Feed(string Symbol = "IBM", string APIkey = "demo") {
        System.Net.Http.HttpClient client = new();

        string req = "https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED" + "&symbol=" + Symbol + "&apikey=" + APIkey;
        var msg = client.GetStringAsync(req).Result;
        var jres = JsonSerializer.Deserialize<JsonDocument>(msg).RootElement;
        jres.TryGetProperty("Time Series (Daily)", out JsonElement json);

        if (json.ValueKind == JsonValueKind.Undefined) { throw new InvalidOperationException("Stock symbol " + Symbol + " not found"); }
        foreach (var val in json.EnumerateObject()) { base.Add(GetOHLC(val)); }
        base.Reverse();
    }
    private static (DateTime t, double o, double h, double l, double c, double v) GetOHLC(JsonProperty json) {
        double o, h, l, c, v;
        o = h = l = c = v = 0;
        DateTime date = Convert.ToDateTime(json.Name);
        foreach (var val in json.Value.EnumerateObject()) {
            switch (val.Name) {
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
                default: o = 0; h = 0; l = 0; c = 0; v = 0; break;
            }
        }
        return (date, o, h, l, c, v);
    }
}