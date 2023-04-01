using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class ATR_chart : QuanTAlib_Indicator {
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private readonly int Period = 10;

    #endregion Parameters

    private ATR_Series indicator;

    public ATR_chart()
    {
        this.SeparateWindow = true;
        this.Name = "ATR - Average True Range";
        this.Description = "Average True Range description";
        this.AddLineSeries("ATR", Color.RoyalBlue, 3, LineStyle.Solid);
    }

	  protected override void OnInit() { base.OnInit();
		  indicator = new(source: bars, period: Period, useNaN: false);
    }

	protected override void OnUpdate(UpdateArgs args) {
		base.OnUpdate(args);
		this.SetValue(indicator[^1].v, lineIndex: 0);
	}

}
