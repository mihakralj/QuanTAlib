using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class ZLEMA_Test {
  [Fact]
  public void ZLEMASeries_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 10, 1, 2, 4.5 };
    ZLEMA_Series c = new(a, 5);
    Assert.Equal(16, c.Count);
    Assert.Equal(3.4296359994527807, c.Last().v);
  }

  [Fact]
  public void ZLEMAUpdate_Test() {
    TSeries a = new() { 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5 };
    ZLEMA_Series c = new(a, 3);
    a.Add(30.0, true);
    Assert.Equal(30.8115234375, c.Last().v);
  }
}