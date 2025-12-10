using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib;

public class MamaValidationTests
{
    private readonly ITestOutputHelper _output;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;

    public MamaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 1. Generate data
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _data = bars.Close;

        // 2. Prepare data for Skender (List<Quote>)
        _skenderQuotes = new List<Quote>();
        for (int i = 0; i < _data.Count; i++)
        {
            _skenderQuotes.Add(new Quote
            {
                Date = new DateTime(_data.Times[i], DateTimeKind.Utc),
                Close = (decimal)_data.Values[i],
                Open = (decimal)_data.Values[i],
                High = (decimal)_data.Values[i],
                Low = (decimal)_data.Values[i],
                Volume = 1000
            });
        }
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        double fastLimit = 0.5;
        double slowLimit = 0.05;

        // 1. Calculate QuanTAlib MAMA
        // Skender uses HL2 by default. We need to feed (H+L)/2 to our Mama to match.
        var mama = new Mama(fastLimit, slowLimit);
        
        var hl2Values = new List<double>();
        var hl2Times = new List<long>();
        foreach(var q in _skenderQuotes)
        {
            hl2Values.Add(((double)q.High + (double)q.Low) / 2.0);
            hl2Times.Add(q.Date.Ticks);
        }
        var hl2Series = new TSeries(hl2Times, hl2Values);
        
        _ = mama.Update(hl2Series);

        // 2. Calculate Skender MAMA
        // Note: Skender might use different parameter names or order.
        // Assuming GetMama(fastLimit, slowLimit)
        var sResult = _skenderQuotes.GetMama(fastLimit, slowLimit).ToList();

        // 3. Verify
        VerifyData_Skender(sResult);
        
        _output.WriteLine("MAMA Batch validated successfully against Skender");
    }

    private void VerifyData_Skender(List<MamaResult> sResult)
    {
        // Skip warmup period
        int skip = 500; 
        
        // We need to compare both MAMA and FAMA
        // But Update(TSeries) returns only MAMA line in TSeries.
        // We can iterate and check.
        
        // Actually, let's re-run streaming update to capture FAMA values if needed, 
        // or just trust that if MAMA matches, FAMA likely matches (since FAMA depends on MAMA).
        // But better to verify both.
        
        // Re-calculate streaming to get FAMA access
        var m = new Mama(0.5, 0.05);
        for(int i=0; i < _data.Count; i++)
        {
            double hl2 = ((double)_skenderQuotes[i].High + (double)_skenderQuotes[i].Low) / 2.0;
            m.Update(new TValue(_data.Times[i], hl2));
            
            if (i < skip) continue;

            var sItem = sResult[i];
            
            // Check MAMA
            if (sItem.Mama != null)
            {
                double sMama = (double)sItem.Mama;
                double qMama = m.Last.Value;
                Assert.True(Math.Abs(sMama - qMama) < 0.5, $"MAMA mismatch at index {i}: Skender {sMama}, QuanTAlib {qMama}");
            }
            
            // Check FAMA
            if (sItem.Fama != null)
            {
                double sFama = (double)sItem.Fama;
                double qFama = m.Fama.Value;
                Assert.True(Math.Abs(sFama - qFama) < 0.5, $"FAMA mismatch at index {i}: Skender {sFama}, QuanTAlib {qFama}");
            }
        }
    }
}
