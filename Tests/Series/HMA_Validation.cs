using Xunit;
using System;
using QuantLib;
using Skender.Stock.Indicators;
using TALib;

namespace Validation;
public class HMA_Validation {

  [Fact]
  public void Update() {
    RND_Feed bars = new(1000);
    Random rnd = new();
    int period = rnd.Next(28) + 3;
    /////

    HMA_Series QL = new(bars.Close, period, false);

    /////
    int len1 = QL.Count;
    QL.Add(bars.Close.First(), update: true);
    int len2 = QL.Count;

    Assert.Equal(len1, len2);
  }

  [Fact]
  public void Skender_Stock() {
    // Calculate Skender.Stock.Indicators value on 1000 random bars

    RND_Feed bars = new(1000);
    int period = 10;
    IEnumerable<Quote> quotes = bars.Select(
        q => new Quote { Date = q.t, Open = (decimal)q.o, High = (decimal)q.h,
                         Low = (decimal)q.l, Close = (decimal)q.c,
                         Volume = (decimal)q.v });
    /////

    HMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetHma(period);
        double expected = Math.Round((double)SK.Last().Hma!, 8);

        /////
        double result = Math.Round(QL.Last().v, 8);
        Assert.Equal(expected, result);
  }

  /*
      [Fact]
          public void Pandas_TA()
          {
              // Calculate Pandas.TA value

              RND_Feed bars = new(1000);
              int period = 10;
              Installer.SetupPython().Wait();
              Installer.TryInstallPip();
              Installer.PipInstallModule("pandas-ta");
              PythonEngine.Initialize();
              dynamic ta = Py.Import("pandas_ta");
              var df = ta.DataFrame(bars.Close.v);
              /////

              HMA_Series QL = new(bars.Close, period, false);
              var pta = ta.hma(close: df[0], length: period);

              /////
              double result = Math.Round(QL.Last().v, 7);
              double expected = System.Math.Round((double)pta.tail(1), 7);
              Assert.Equal(expected, result);
          }
  */
}
