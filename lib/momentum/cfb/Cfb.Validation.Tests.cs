using System;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class CfbValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public CfbValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Consistency_UpdateVsCalculate()
    {
        // Verify that Update(TValue) and Calculate(TSeries) produce identical results
        var cfb = new Cfb();
        var streamResult = new TSeries();
        foreach (var item in _testData.Data)
        {
            streamResult.Add(cfb.Update(item));
        }

        var batchResult = Cfb.Calculate(_testData.Data);

        Assert.Equal(streamResult.Count, batchResult.Count);
        Assert.NotEmpty(streamResult);
        for (int i = 0; i < streamResult.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, batchResult[i].Value, 1e-9);
        }
        _output.WriteLine("CFB Update vs Calculate validated successfully");
    }

    [Fact]
    public void Validate_Consistency_SeriesVsSpan()
    {
        // Verify that Calculate(TSeries) and Calculate(Span) produce identical results
        var batchResult = Cfb.Calculate(_testData.Data);
        
        var spanInput = _testData.Data.Values.ToArray().AsSpan();
        var spanOutput = new double[spanInput.Length];
        Cfb.Calculate(spanInput, spanOutput);

        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], 1e-9);
        }
        _output.WriteLine("CFB Series vs Span validated successfully");
    }

    [Fact]
    public void Validate_Properties()
    {
        // CFB should be >= 1.0
        var result = Cfb.Calculate(_testData.Data);
        foreach (var val in result.Values)
        {
            Assert.True(val >= 1.0, $"CFB value {val} should be >= 1.0");
        }
        _output.WriteLine("CFB properties validated successfully");
    }
}
