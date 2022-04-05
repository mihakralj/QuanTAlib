using Xunit;
using System;
using QuantLib;
using Skender.Stock.Indicators;
using TALib;

public class SMA_Validation
{
    [Fact]
    public void SMASeries_Validation()
    {
        // generate 200 random bars
        RND_Feed bars = new(200);

        // generate random period between 2 and 50
        Random ran = new Random();
        int period = ran.Next(48)+2;

        // Calculate QuantLib SMA
        SMA_Series QLsma = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators SMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKsma = quotes.GetSma(period, CandlePart.Close);

        // Calculate TALib.NETCore SMA
        int outBegIdx, outNbElement;
        double[] TALIBsma = new double[bars.Count];
        double[] input = bars.Close.v.ToArray();
        Core.Sma(input, 0, bars.Count-1, TALIBsma, out outBegIdx, out outNbElement, period);

        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKsma.Last().Sma, 7);
        double s2 = Math.Round( TALIBsma[TALIBsma.Length-outBegIdx-1], 7);
        double s3 = Math.Round(QLsma.Last().v, 7);

        Assert.Equal(s1, s3);
        Assert.Equal(s2, s3);
    }

}
