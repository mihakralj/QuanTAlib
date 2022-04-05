using System;
using YahooFinanceApi;
using QuantLib;

public class YAHOO_Feed : TBars {
    public YAHOO_Feed(int days, string security) {
        var history = Yahoo.GetHistoricalAsync(security, DateTime.Today.AddDays(-days), DateTime.Now, Period.Daily).Result;
        for(int i=0; i < history.Count; i++) {
            this.Add((history[i].DateTime, (double)history[i].Open, (double)history[i].High, (double)history[i].Low, (double)history[i].Close, (double)history[i].Volume));
        }
    }
}