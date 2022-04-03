using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class WMA_Test {
  [Fact]
  public void WMASeries_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5 };
    WMA_Series c = new(a, 3);
    Assert.Equal(6, c.Count);
    Assert.Equal(4.333333333333333, c.Last().v);
  }

  [Fact]
  public void WMAUpdate_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5 };
    WMA_Series c = new(a, 3);
    a.Add(2, true);
    Assert.Equal(2.8333333333333335, c.Last().v);
  }
}