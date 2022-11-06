/*

using Xunit;
using System;
using QuanTAlib;
using Python.Runtime;
using Python.Included;

namespace Validation;
public class PandasTA
{
	private readonly RND_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;
	private readonly dynamic ta;
	private readonly dynamic df;

	public PandasTA()
	{
		this.bars = new(1000);
		this.period = this.rnd.Next(28) + 3;

		Installer.SetupPython().Wait();
		Installer.TryInstallPip();
		Installer.PipInstallModule("numpy");
		Installer.PipInstallModule("pandas");
		Installer.PipInstallModule("pandas-ta");
		PythonEngine.Initialize();
		this.ta = Py.Import("pandas_ta");
		this.df = this.ta.DataFrame(this.bars.Close.v);
	}

	~PandasTA()
	{
		PythonEngine.Shutdown();
	}

	[Fact]
	void SMA()
	{
		SMA_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.sma(close: this.df[0], length: this.period);
		
		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}
	

	[Fact]
	void EMA()
	{
		EMA_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.ema(close: this.df[0], length: this.period);
		
		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}


	[Fact]
	void TEMA()
	{
		TEMA_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.tema(close: this.df[0], length: this.period);

		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}

	[Fact]
	void ENTP()
	{
		ENTP_Series QL = new(this.bars.Close, this.period, useNaN:false);
		var pta = this.ta.entropy(close: this.df[0], length: this.period);

		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}


	[Fact]
	void WMA()
	{
		WMA_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.wma(close: this.df[0], length: this.period);
		
		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}
	
	[Fact]
	void DEMA()
	{
		DEMA_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.dema(close: this.df[0], length: this.period);
		
		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}
	
	[Fact]
	void BIAS()
	{
		BIAS_Series QL = new(this.bars.Close, this.period, false);
		var pta = this.ta.bias(close: this.df[0], length: this.period);

		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}

	[Fact]
	void KURT()
	{
		KURT_Series QL = new(this.bars.Close, this.period, useNaN: false);
		var pta = this.ta.kurtosis(close: this.df[0], length: this.period);

		Assert.Equal(System.Math.Round((double)pta.tail(1), 4), Math.Round(QL.Last().v, 4));
	}

	[Fact]
	void MAD()
	{
		MAD_Series QL = new(this.bars.Close, this.period, useNaN: false);
		var pta = this.ta.mad(close: this.df[0], length: this.period);

		Assert.Equal(System.Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
	}
	
}

*/