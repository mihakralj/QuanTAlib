using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class Abstract_Test
{
    [Fact]
    public void Single_Add_variations()
    {
        TSeries s = new() { 1,2,3,4,5 };
		SMA_Series a = new(s, 3)
		{
			{ (DateTime.Today, 10), true }
		};
		Assert.Equal(s.Length, a.Length);
        a.Add(true);
        Assert.Equal(s.Length, a.Length);
        a.Add();
        Assert.Equal(s.Length+1, a.Length);
		}

}
