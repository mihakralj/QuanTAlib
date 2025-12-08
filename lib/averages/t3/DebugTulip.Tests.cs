using System;
using System.Reflection;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class DebugTulipTests
{
    private readonly ITestOutputHelper _output;

    public DebugTulipTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ListTulipIndicators()
    {
        var type = typeof(Tulip.Indicators);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

        _output.WriteLine("Tulip Indicators (Properties):");
        foreach (var p in properties)
        {
            _output.WriteLine(p.Name);
        }

        _output.WriteLine("Tulip Indicators (Fields):");
        foreach (var f in fields)
        {
            _output.WriteLine(f.Name);
        }
    }
}
