using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using QuanTAlib;
using System.Drawing;

namespace SimpleMACross {
	public class SimpleMACross1 : Strategy, ICurrentAccount, ICurrentSymbol {
		[InputParameter("Symbol", 0)]
		public Symbol CurrentSymbol { get; set; }

		[InputParameter("Account", 1)]
		public Account CurrentAccount { get; set; }

		[InputParameter("Fast MA", 2, minimum: 1, maximum: 100, increment: 1, decimalPlaces: 0)]
		private int FastMA = 5;

		[InputParameter("Slow MA", 3, minimum: 1, maximum: 100, increment: 1, decimalPlaces: 0)]
		private int SlowMA = 10;

		[InputParameter("Quantity", 4, 0.1, 99999, 0.1, 2)]
		private double Quantity = 1.0;
	
		[InputParameter("Period", 5)]
		private Period period = Period.MIN1;

		public override string[] MonitoringConnectionsIds => new string[] { this.CurrentSymbol?.ConnectionId, this.CurrentAccount?.ConnectionId };

		private HistoricalData hdm;
		private DateTime prev_time;
		private readonly TBars bars = new();

		public SimpleMACross1() {
			this.Name = "MA Cross strategy 3";
			this.Description = "Raw strategy without any additional functional";
		}

		protected override void OnRun() {
			if (this.CurrentAccount != null && this.CurrentAccount.State == BusinessObjectState.Fake)	{this.CurrentAccount = Core.Instance.GetAccount(this.CurrentAccount.CreateInfo());}
			if (this.CurrentSymbol != null && this.CurrentSymbol.State == BusinessObjectState.Fake)	{this.CurrentSymbol = Core.Instance.GetSymbol(this.CurrentSymbol.CreateInfo());}
			if (this.CurrentSymbol == null || this.CurrentAccount == null || this.CurrentSymbol.ConnectionId != this.CurrentAccount.ConnectionId) {
				this.Log("Incorrect input parameters... Symbol or Account are not specified or they have different connectionID.", StrategyLoggingLevel.Error);
				return;	}

			/////////////////////////////////////////////////////
			this.hdm = this.CurrentSymbol.GetHistory(Period.MIN1, this.CurrentSymbol.HistoryType, Core.TimeUtils.DateTimeUtcNow.AddDays(-1));
			////////////////////////////////////////////////////
			

			this.LogInfo($"Symbol: {CurrentSymbol.Name} period: {this.period} :-: {this.CurrentSymbol.HistoryType.ToString()} :-: {this.hdm.Count} bars loaded");
			this.hdm.HistoryItemUpdated += this.Hdm_HistoryItemUpdated;
		}

		private void Hdm_HistoryItemUpdated(object sender, HistoryEventArgs e) {
			this.OnUpdate();
		}

		private void OnUpdate() {
			bool update = hdm.Last().TimeLeft - prev_time < this.period.Duration ? true : false;
			if (!update) {prev_time = hdm.Last().TimeLeft;}

			bars.Add(hdm.Last().TimeLeft, hdm.Last()[PriceType.Open], hdm.Last()[PriceType.High], 
					hdm.Last()[PriceType.Low], hdm.Last()[PriceType.Close], hdm.Last()[PriceType.Volume], update);

			if (!update) {this.LogInfo($"{bars.Close.Last().t}   OHLC4:{(double)bars.OHLC4.Last.v}");}

		}

		protected override List<StrategyMetric> OnGetMetrics() {
			var result = base.OnGetMetrics();

			// An example of adding custom strategy metrics:
			result.Add("Bars processed", this.bars.Count.ToString());
			
			return result;
		}

		protected override void OnStop() {
			if (this.hdm != null) {
				this.hdm.HistoryItemUpdated -= this.Hdm_HistoryItemUpdated;
				this.hdm.Dispose();
			}

			base.OnStop();
		}
	}
}

