namespace QuanTAlib;
using System;
using System.Text.Json;

/* <summary>
Yahoo Finance - Free API feed to collect daily market quotes
    Parameters:
        Symbol: stock symbol (default: "IBM")
        Period: number of days of collected history (default: 252)
    Usage:
        Yahoo_Feed ticker = new("MSFT", 20)

</summary> 
*/
public class Yahoo_Feed : TBars
{
    public Yahoo_Feed(string Symbol = "IBM", int Period = 252)
    {
        Period = (int)(Period * 1.45);
        string requestUrl = "https://query1.finance.yahoo.com/v8/finance/chart/" +
            Symbol + "?interval=1d&period1=" +
            (int)new DateTimeOffset(DateTime.UtcNow.AddDays(-Period + 1)).ToUnixTimeSeconds() + "&period2=" +
            (int)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        System.Net.Http.HttpClient client = new();
        var msg = client.GetStringAsync(requestUrl).Result;
        var jresult = JsonSerializer.Deserialize<JsonDocument>(msg).RootElement;

        jresult.TryGetProperty("chart", out JsonElement json);
        json.TryGetProperty("result", out json);
        json[0].TryGetProperty("timestamp", out JsonElement datetime);
        json[0].TryGetProperty("indicators", out json);
        json.TryGetProperty("quote", out json);
        json[0].TryGetProperty("open", out JsonElement open);
        json[0].TryGetProperty("high", out JsonElement high);
        json[0].TryGetProperty("low", out JsonElement low);
        json[0].TryGetProperty("close", out JsonElement close);
        json[0].TryGetProperty("volume", out JsonElement volume);

        for (int i = 0; i < datetime.GetArrayLength(); i++)
        {
            DateTime d = DateTimeOffset.FromUnixTimeSeconds(long.Parse(datetime[i].GetRawText())).DateTime;
            double o = Math.Round(double.Parse(open[i].GetRawText()), 3);
            double h = Math.Round(double.Parse(high[i].GetRawText()), 3);
            double l = Math.Round(double.Parse(low[i].GetRawText()), 3);
            double c = Math.Round(double.Parse(close[i].GetRawText()), 3);
            double v = Math.Round(double.Parse(volume[i].GetRawText()), 3);
            base.Add(d, o, h, l, c, v);
        }
    }
}
