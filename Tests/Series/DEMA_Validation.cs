using Xunit;
using System;
using Skender.Stock.Indicators;
using TALib;
using QuantLib;

namespace Validation;
public class DEMA_Validation
{
    [Fact]
    public void DEMASeries_Validation()
    {
        // generate 1000 random bars
        RND_Feed bars = new(1000);

        // generate random period between 2 and 50
        Random ran = new();
        int period = ran.Next(48)+2;

        // Calculate QuantLib DEMA
        DEMA_Series QLdema = new(bars.Close,period);

        // Calculate Skender.Stock.Indicators DEMA
        IEnumerable<Quote> quotes = bars.Select(q => new Quote
            { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h, Low = (decimal)q.l, Close = (decimal)q.c, Volume = (decimal)q.v });
        var SKdema = quotes.GetDoubleEma(period);

        // Calculate TALib.NETCore EMA
        double[] TALIBdema = new double[bars.Count];
        double[] input = bars.Close.v.ToArray();
        Core.Dema(input, 0, bars.Count-1, TALIBdema, out int outBegIdx, out int outNbElement, period);

        //Round results to 7 decimal places
        double s1 = Math.Round((double) SKdema.Last().Dema!, 7);
        double s2 = Math.Round(TALIBdema[TALIBdema.Length-outBegIdx-1], 7);
        double s3 = Math.Round(QLdema.Last().v, 7);

        Assert.Equal(s1, s3);
        Assert.Equal(s2, s3);
    }

}
