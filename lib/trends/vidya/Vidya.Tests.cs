using QuanTAlib;
using Xunit;

namespace Trends;

public class VidyaTests
{
    [Fact]
    public void BasicCalculation()
    {
        // Test with a small dataset
        // Period = 2
        // Alpha = 2 / (2 + 1) = 0.666...
        
        var vidya = new Vidya(2);
        
        // Bar 1: Price 100
        // Init: PrevClose=100, LastVidya=100, Ups=[0,0], Downs=[0,0]
        // Output: 100
        var v1 = vidya.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, v1.Value);
        
        // Bar 2: Price 110
        // Change = 110 - 100 = 10
        // Up=10, Down=0
        // Ups=[10,0], Downs=[0,0]
        // SumUp=10, SumDown=0, Sum=10
        // VI = |10-0|/10 = 1
        // DynAlpha = 0.666 * 1 = 0.666
        // Vidya = 0.666 * 110 + 0.333 * 100 = 73.33 + 33.33 = 106.66
        var v2 = vidya.Update(new TValue(DateTime.UtcNow, 110));
        Assert.Equal(106.66666666666667, v2.Value, 5);
        
        // Bar 3: Price 105
        // Change = 105 - 110 = -5
        // Up=0, Down=5
        // Ups=[0,10], Downs=[5,0] (Circular buffer logic)
        // SumUp=10, SumDown=5, Sum=15
        // VI = |10-5|/15 = 5/15 = 0.333
        // DynAlpha = 0.666 * 0.333 = 0.222
        // Vidya = 0.222 * 105 + 0.777 * 106.66 = 23.33 + 82.96 = 106.29
        var v3 = vidya.Update(new TValue(DateTime.UtcNow, 105));
        Assert.Equal(106.29629629629629, v3.Value, 5);
    }

    [Fact]
    public void IsNewConsistency()
    {
        var vidya = new Vidya(5);
        var inputs = new double[] { 100, 105, 102, 108, 110, 105 };
        
        // Feed normally
        var expected = new List<double>();
        foreach (var input in inputs)
        {
            expected.Add(vidya.Update(new TValue(DateTime.UtcNow, input)).Value);
        }
        
        // Feed with updates
        vidya.Reset();
        for (int i = 0; i < inputs.Length; i++)
        {
            // Update with a temporary value first
            vidya.Update(new TValue(DateTime.UtcNow, inputs[i] + 1), true);
            
            // Correct it
            var corrected = vidya.Update(new TValue(DateTime.UtcNow, inputs[i]), false);
            
            Assert.Equal(expected[i], corrected.Value, 1e-9);
        }
    }

    [Fact]
    public void StaticVsInstance()
    {
        var vidya = new Vidya(5);
        var inputs = new double[] { 100, 105, 102, 108, 110, 105, 100, 95, 98, 102 };
        var tSeries = new TSeries();
        tSeries.Add(inputs);
        
        var instanceResult = vidya.Update(tSeries);
        
        var staticResult = new double[inputs.Length];
        Vidya.Calculate(inputs, staticResult, 5);
        
        for (int i = 0; i < inputs.Length; i++)
        {
            Assert.Equal(instanceResult.Values[i], staticResult[i], 1e-9);
        }
    }

    [Fact]
    public void EdgeCases()
    {
        var vidya = new Vidya(5);
        
        // Empty
        Assert.Empty(vidya.Update(new TSeries()));
        
        // NaN handling
        vidya.Reset();
        vidya.Update(new TValue(DateTime.UtcNow, 100));
        var v2 = vidya.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(100, v2.Value); // Should hold previous value
        
        // Period 1
        var vidya1 = new Vidya(1);
        var v = vidya1.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, v.Value);
    }
}
