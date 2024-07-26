using Xunit;
using System;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace Basics;
#nullable disable
public class TBars
{
  private static Type[] maSeriesTypes = new Type[]
  {
    typeof(ATR_Series),
    typeof(ATRP_Series),
    typeof(TR_Series),
    typeof(ADL_Series),
    typeof(CCI_Series),
    typeof(OBV_Series),
    typeof(ADOSC_Series),
    typeof(MIDPRICE_Series),
};

  [Theory]
  [MemberData(nameof(MASeriesData))]
  public void Name_exists(Type classType)
  {
		GBM_Feed data = new(10);

		var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
    Assert.NotEmpty(MA_Series.Name);
  }

  [Theory]
  [MemberData(nameof(MASeriesData))]
  public void Series_Length(Type classType)
  {
    GBM_Feed data = new(1000);

    var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
    Assert.Equal(1000, MA_Series.Count);
  }

  [Theory]
  [MemberData(nameof(MASeriesData))]
  public void Return_data(Type classType)
  {
		GBM_Feed data = new(10);
		var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
    var result = MA_Series.Add((DateTime.Today, 1,2,3,4,5));
    Assert.Equal(result.v, MA_Series.Last.v);
  }

  [Theory]
  [MemberData(nameof(MASeriesData))]
  public void Update(Type classType)
  {
	  GBM_Feed data = new(10);
    var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
    var pre_update = MA_Series.Last;

    var pre_data = data.Last;
    data.Add((DateTime.Today, 1, 2, 3, 4, 5), true);
    data.Add(pre_data, true);

    Assert.Equal(pre_update.v, MA_Series.Last.v);
    Assert.Equal(data.Count, MA_Series.Count);
}

	[Theory]
  [MemberData(nameof(MASeriesData))]
  public void Reset(Type classType)
  {
    GBM_Feed data = new(10);
    var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
    MA_Series.Reset();
    data.Add();
		Assert.False(double.IsNaN(MA_Series.Last.v));
}

  [Theory]
  [MemberData(nameof(MASeriesData))]
  public void Period_default(Type classType) {
	  GBM_Feed data = new(100);

	  var MA_Series = Activator.CreateInstance(classType, data) as TSeries;
	  Assert.False(double.IsNaN(MA_Series.Last.v));
	}

	public static IEnumerable<object[]> MASeriesData()
  {
    foreach (var type in maSeriesTypes)
    {
      yield return new object[] { type };
    }
  }
}
#nullable restore