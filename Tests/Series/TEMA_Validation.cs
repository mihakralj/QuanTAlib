using Xunit;
using System;
using QuantLib;
using TALib;
using Skender.Stock.Indicators;

namespace Validation;
public class TEMA_Validation
{
    [Fact]
    public void TEMASeries_Validation()
    {
        // generate 1000 random bars
        RND_Feed bars = new(1000);

        // generate random period between 2 and 50
        Random ran = new();
        int period = ran.Next(48)+2;

        // Calculate QuantLib TEMA
        TEMA_Series QLtema = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators TEMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKtema = quotes.GetTripleEma(period);

        // Calculate TALib.NETCore EMA
        double[] TALIBtema = new double[bars.Count];
        double[] input = bars.Close.v.ToArray();
        Core.Tema(input, 0, bars.Count-1, TALIBtema, out int outBegIdx, out int outNbElement, period);

        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKtema.Last().Tema!, 7);
        double s2 = Math.Round(TALIBtema[TALIBtema.Length-outBegIdx-1], 7);
        double s3 = Math.Round(QLtema.Last().v, 7);

        Assert.Equal(s1, s3);
        Assert.Equal(s2, s3);
    }

}
