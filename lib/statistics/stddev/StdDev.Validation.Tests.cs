using System;
using System.Linq;
using Xunit;
using QuanTAlib;
using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;
using MathNet.Numerics.Statistics;

namespace QuanTAlib.Validation;

public class StdDevValidationTests
{
    private readonly ValidationTestData _data = new();

    [Fact]
    public void StdDev_Matches_Skender()
    {
        // Skender StdDev uses Population Standard Deviation (N)
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: true);
        var skenderStdDev = _data.SkenderQuotes.GetStdDev(period);

        var skenderList = skenderStdDev.ToList();
        var quotes = _data.SkenderQuotes.ToList();
        
        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = stdDev.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));
            var skenderVal = skenderList[i].StdDev;

            if (i >= period && skenderVal.HasValue)
            {
                Assert.Equal(skenderVal.Value, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void StdDev_Matches_Talib()
    {
        // TA-Lib STDDEV uses Population Standard Deviation (N)
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: true);
        
        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();
        double[] output = new double[input.Length];

        // TA-Lib calculation
        // STDDEV(real, timeperiod=5, nbdev=1)
        var retCode = TALib.Functions.StdDev(input, 0..^0, output, out var outRange, period, 1.0);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = stdDev.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));

            if (i >= outRange.Start.Value)
            {
                double talibVal = output[i - outRange.Start.Value];
                Assert.Equal(talibVal, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void StdDev_Matches_Tulip()
    {
        // Tulip STDDEV uses Population Standard Deviation (N)
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: true);
        
        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();
        
        // Tulip calculation
        var stdDevInd = Tulip.Indicators.stddev;
        double[][] inputs = { input };
        double[] options = { period };
        double[][] outputs = { new double[input.Length - stdDevInd.Start(options)] };
        
        stdDevInd.Run(inputs, options, outputs);
        
        double[] output = outputs[0];
        int lookback = stdDevInd.Start(options);

        for (int i = 0; i < quotes.Count; i++)
        {
            var tValue = stdDev.Update(new TValue(quotes[i].Date, (double)quotes[i].Close));

            if (i >= lookback)
            {
                double tulipVal = output[i - lookback];
                Assert.Equal(tulipVal, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void StdDev_Matches_MathNet()
    {
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: false);
        var popStdDev = new StdDev(period, isPopulation: true);
        
        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = stdDev.Update(new TValue(DateTime.UtcNow, input[i]));
            var popVal = popStdDev.Update(new TValue(DateTime.UtcNow, input[i]));

            if (i >= input.Length - 100)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = Statistics.StandardDeviation(window);
                double expectedPop = Statistics.PopulationStandardDeviation(window);
                
                Assert.Equal(expected, val.Value, ValidationHelper.DefaultTolerance);
                Assert.Equal(expectedPop, popVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

}
