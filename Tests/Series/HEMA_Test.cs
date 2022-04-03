<<<<<<< Updated upstream:Tests/Series/HEMA_Test.cs
using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class HEMA_Test {
  [Fact]
  public void HEMASeries_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5 };
    HEMA_Series c = new(a, 3);
    Assert.Equal(6, c.Count);
    Assert.Equal(5.744979248803241, c.Last().v);
  }

  [Fact]
  public void HEMAUpdate_Test() {
    TSeries a = new() { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
    HEMA_Series c = new(a, 3);
    a.Add(4, true);
    Assert.Equal(3.206475521456251, c.Last().v);
  }
}
=======
using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class HEMA_Test
{
    [Fact]
    public void HEMASeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        HEMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(5.744979248803241, c.Last().v);
    }

    [Fact]
    public void HEMAUpdate_Test()
    {
        TSeries a = new() { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5};
        HEMA_Series c = new(a, 3);
        a.Add(4, true);
        Assert.Equal(3.206475521456251, c.Last().v);
    }

}
>>>>>>> Stashed changes:Tests/HEMA_Test.cs
