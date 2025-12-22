using System;
using Xunit;

namespace QuanTAlib.Tests;

public class VelValidationTests
{
    [Fact]
    public void Vel_Matches_PwmaMinusWma()
    {
        // VEL = PWMA - WMA
        // We validate this relationship holds true for a random sequence of data.

        int period = 10;
        var vel = new Vel(period);
        var pwma = new Pwma(period);
        var wma = new Wma(period);

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var input = new TValue(bar.Time, bar.Close);
            
            var v = vel.Update(input);
            var p = pwma.Update(input);
            var w = wma.Update(input);

            Assert.Equal(p.Value - w.Value, v.Value, ValidationHelper.DefaultTolerance);
        }
    }
}
