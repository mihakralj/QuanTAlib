using Xunit;
using System;
using Skender.Stock.Indicators;
using QuantLib;

namespace Validation;
public class HMA_Validation
{
    [Fact]
    public void HMASeries_Validation()
    {
        // generate 1000 random bars
        RND_Feed bars = new(1000);

        // generate random period between 2 and 50
        int period = 10;

        // Calculate QuantLib HMA
        HMA_Series QLhma = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators HMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKhma = quotes.GetHma(period);

        // No TALib.NETCore HMA
        
        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKhma.Last().Hma!, 7);
        double s3 = Math.Round(QLhma.Last().v, 7);

        Assert.Equal(s1, s3);
    }

}
