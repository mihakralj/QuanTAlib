using Xunit;
using System;
using Skender.Stock.Indicators;
using TALib;
using QuantLib;

namespace Validation;
public class WMA_Validation
{
    [Fact]
    public void WMASeries_Validation()
    {
        // generate 100 random bars
        RND_Feed bars = new(100);

        // generate random period between 2 and 50
        Random ran = new();
        int period = ran.Next(48)+2;

        // Calculate QuantLib WMA
        WMA_Series QLwma = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators WMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKwma = quotes.GetWma(period, CandlePart.Close);

        // Calculate TALib.NETCore WMA
        double[] TALIBwma = new double[bars.Count];
        double[] input = bars.Close.v.ToArray();
        Core.Wma(input, 0, bars.Count-1, TALIBwma, out int outBegIdx, out int outNbElement, period);

        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKwma.Last().Wma!, 7);
        double s2 = Math.Round(TALIBwma[TALIBwma.Length-outBegIdx-1], 7);
        double s3 = Math.Round(QLwma.Last().v, 7);

        Assert.Equal(s1, s3);
        Assert.Equal(s2, s3);
    }

}
