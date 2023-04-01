using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class RSI_chart : QuanTAlib_Indicator {
  #region Parameters

  [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
  private int Period = 10;

  [InputParameter("Data source", 1, variants: new object[]
    { "Open", 0, "High", 1,  "Low", 2,  "Close", 3,  "HL2", 4,  "OC2", 5,
      "OHL3", 6,  "HLC3", 7,  "OHLC4", 8,  "Weighted (HLCC4)", 9 })]
  private int DataSource = 8;

  [InputParameter("Overbought level", 2, 1, 100, 1, 1)]
  private int Overbought = 70;

  [InputParameter("Oversold level", 2, 1, 100, 1, 1)]
  private int Oversold = 30;

  #endregion Parameters

  ///////
  private RSI_Series indicator;
  ///////

  public RSI_chart() : base() {
    this.Name = "RSI - Relative Strength Index";
    this.Description = "RSI description";
    this.AddLineSeries("RSI", Color.RoyalBlue, 3, LineStyle.Solid);
    this.SeparateWindow = true;
  }

  protected override void OnInit() {
    base.OnInit();
    indicator = new(source: bars.Select(this.DataSource), period: this.Period, useNaN: true);
  }

  protected override void OnUpdate(UpdateArgs args) {
    base.OnUpdate(args);
    SetValue(indicator[^1].v, lineIndex: 0);
    if (indicator[^1].v >= Overbought)
      LinesSeries[0].SetMarker(0, color: Color.Red);
    if (indicator[^1].v <= Oversold)
      LinesSeries[0].SetMarker(0, color: Color.Red);
  }
  public override void OnPaintChart(PaintChartEventArgs args) {
    base.OnPaintChart(args);
    for (int i = firstOnScreenBarIndex; i <= lastOnScreenBarIndex; i++) {
      int xLeft = (int)Math.Round(mainWindow.CoordinatesConverter.GetChartX(Time(Count - i - 1)));
      int y = (int)Math.Round((mainWindow.CoordinatesConverter.GetChartY(Overbought)));
    }
  }
}
