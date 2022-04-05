using Xunit;
using System;
using QuantLib;
using Skender.Stock.Indicators;
using TALib;

public class EMA_Validation
{
    [Fact]
    public void EMASeries_Validation()
    {
        // generate 200 random bars
        RND_Feed bars = new(500);

        // generate random period between 2 and 50
        Random ran = new Random();
        int period = ran.Next(48)+2;

        // Calculate QuantLib EMA
        EMA_Series QLema = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators EMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKema = quotes.GetEma(period, CandlePart.Close);

        // Calculate TALib.NETCore EMA
        int outBegIdx, outNbElement;
        double[] TALIBema = new double[bars.Count];
        double[] input = bars.Close.v.ToArray();
        Core.Ema(input, 0, bars.Count-1, TALIBema, out outBegIdx, out outNbElement, period);

        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKema.Last().Ema, 7);
        double s2 = Math.Round(TALIBema[TALIBema.Length-outBegIdx-1], 7);
        double s3 = Math.Round(QLema.Last().v, 7);

        Assert.Equal(s1, s3);
        Assert.Equal(s2, s3);
    }

}
