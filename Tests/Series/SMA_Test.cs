<<<<<<< Updated upstream:Tests/Series/SMA_Test.cs
using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class SMA_Test {
  [Fact]
  public void SMASeries_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5 };
    SMA_Series c = new(a, 3);
    Assert.Equal(6, c.Count);
    Assert.Equal(4.0, c.Last().v);
  }

  [Fact]
  public void SMAUpdate_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5 };
    SMA_Series c = new(a, 3);
    a.Add(2, true);
    Assert.Equal(3.0, c.Last().v);
  }
}
=======
using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class SMA_Test
{
    [Fact]
    public void SMASeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        SMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(4.0, c.Last().v);
    }

    [Fact]
    public void SMAUpdate_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        SMA_Series c = new(a, 3);
        a.Add(2, true);
        Assert.Equal(3.0, c.Last().v);
    }

}
>>>>>>> Stashed changes:Tests/SMA_Test.cs
