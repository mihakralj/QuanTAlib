using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AAA_chart : Indicator {
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private readonly int Period = 10;

    #endregion Parameters

    private TBars bars;
    private TSeries series;
    private JMA_Series jma;
	private DWMA_Series dwma;

	public AAA_chart() : base()
    {
        this.SeparateWindow = true;
        this.Name = "AAA - Test indicator";
        this.Description = "Test indicator";

		this.AddLineSeries("JMA", Color.RoyalBlue, 3, LineStyle.Solid);
		this.AddLineSeries("DWMA", Color.OrangeRed, 3, LineStyle.Solid);
		this.SeparateWindow = false;
	}

    protected override void OnInit()
    {
        this.ShortName = "AAA (" + this.Period + ")";
        this.bars = new();
        this.series = new();

        this.jma = new(source: bars.HLC3, period: this.Period, useNaN: false);
		this.dwma = new(source: bars.HLC3, period: this.Period, useNaN: false);
	}

	protected override void OnUpdate(UpdateArgs args)
    {
		Debug.WriteLine($"{args.Reason}");
		bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

        this.bars.Add(this.Time(), 
            this.GetPrice(PriceType.Open),
            this.GetPrice(PriceType.High),
            this.GetPrice(PriceType.Low),
            this.GetPrice(PriceType.Close),
            this.GetPrice(PriceType.Volume), 
            update);

        //this.series.Add(0.25*(this.GetPrice(PriceType.Open)+ this.GetPrice(PriceType.High)+ this.GetPrice(PriceType.Low)+ this.GetPrice(PriceType.Close)), update);

		this.SetValue(this.jma.v.Last(), 0);
		this.SetValue(this.dwma.v.Last(), 1);
	}
}
