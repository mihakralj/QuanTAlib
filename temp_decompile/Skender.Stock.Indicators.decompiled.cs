using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: CLSCompliant(true)]
[assembly: InternalsVisibleTo("Tests.Indicators")]
[assembly: InternalsVisibleTo("Tests.Performance")]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyCompany("Dave Skender")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("@2020 Dave Skender")]
[assembly: AssemblyDescription("Stock Indicators for .NET.  Transform financial market price quotes into technical analysis indicators such as MACD, Stochastic RSI, Average True Range, Parabolic SAR, etc.  Nothing more.")]
[assembly: AssemblyFileVersion("2.7.0.0")]
[assembly: AssemblyInformationalVersion("2.7.0-43+Branch.main.Sha.e4c40d7cc048936a44d34291729c7772537f65da.e4c40d7cc048936a44d34291729c7772537f65da")]
[assembly: AssemblyProduct("Stock Indicators for .NET")]
[assembly: AssemblyTitle("Skender.Stock.Indicators")]
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/DaveSkender/Stock.Indicators")]
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: AssemblyVersion("2.7.0.0")]
[module: RefSafetyRules(11)]
namespace Skender.Stock.Indicators;

public static class Indicator
{
	private static readonly CultureInfo invCulture = CultureInfo.InvariantCulture;

	private static readonly Calendar invCalendar = invCulture.Calendar;

	private static readonly CalendarWeekRule invCalendarWeekRule = invCulture.DateTimeFormat.CalendarWeekRule;

	private static readonly DayOfWeek invFirstDayOfWeek = invCulture.DateTimeFormat.FirstDayOfWeek;

	/// <summary>
	///     Accumulation/Distribution Line (ADL) is a rolling accumulation of Chaikin Money Flow Volume.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Adl/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="smaPeriods">Optional.  Number of periods in the moving average of ADL.</param><returns>Time series of ADL values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AdlResult> GetAdl<TQuote>(this IEnumerable<TQuote> quotes, int? smaPeriods = null) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcAdl(smaPeriods);
	}

	internal static List<AdlResult> CalcAdl(this List<QuoteD> qdList, int? smaPeriods)
	{
		ValidateAdl(smaPeriods);
		List<AdlResult> list = new List<AdlResult>(qdList.Count);
		double num = 0.0;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				double num2 = ((quoteD.High == quoteD.Low) ? 0.0 : ((quoteD.Close - quoteD.Low - (quoteD.High - quoteD.Close)) / (quoteD.High - quoteD.Low)));
				double num3 = num2 * quoteD.Volume;
				double num4 = num3 + num;
				AdlResult adlResult = new AdlResult(quoteD.Date)
				{
					MoneyFlowMultiplier = num2,
					MoneyFlowVolume = num3,
					Adl = num4
				};
				list.Add(adlResult);
				num = num4;
				if (smaPeriods.HasValue && i + 1 >= smaPeriods)
				{
					double? num5 = 0.0;
					for (int j = i + 1 - smaPeriods.Value; j <= i; j++)
					{
						num5 += list[j].Adl;
					}
					adlResult.AdlSma = num5 / (double?)smaPeriods;
				}
			}
			return list;
		}
	}

	private static void ValidateAdl(int? smaPeriods)
	{
		if (smaPeriods.HasValue && smaPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "SMA periods must be greater than 0 for ADL.");
		}
	}

	/// <summary>
	///     Directional Movement Index (DMI) and Average Directional Movement Index (ADX) is a measure of price directional movement.
	///     It includes upward and downward indicators, and is often used to measure strength of trend.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Adx/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of ADX and Plus/Minus Directional values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AdxResult> GetAdx<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcAdx(lookbackPeriods);
	}

	internal static List<AdxResult> CalcAdx(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateAdx(lookbackPeriods);
		int count = qdList.Count;
		List<AdxResult> list = new List<AdxResult>(count);
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 0.0;
		double num8 = 0.0;
		double num9 = 0.0;
		double num10 = 0.0;
		double num11 = 0.0;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				AdxResult adxResult = new AdxResult(quoteD.Date);
				list.Add(adxResult);
				if (i == 0)
				{
					num = quoteD.High;
					num2 = quoteD.Low;
					num3 = quoteD.Close;
					continue;
				}
				double val = Math.Abs(quoteD.High - num3);
				double val2 = Math.Abs(quoteD.Low - num3);
				double num12 = quoteD.High - num;
				double num13 = num2 - quoteD.Low;
				double num14 = Math.Max(quoteD.High - quoteD.Low, Math.Max(val, val2));
				double num15 = ((num12 > num13) ? Math.Max(num12, 0.0) : 0.0);
				double num16 = ((num13 > num12) ? Math.Max(num13, 0.0) : 0.0);
				num = quoteD.High;
				num2 = quoteD.Low;
				num3 = quoteD.Close;
				if (i <= lookbackPeriods)
				{
					num8 += num14;
					num9 += num15;
					num10 += num16;
				}
				if (i < lookbackPeriods)
				{
					continue;
				}
				double num17;
				double num18;
				double num19;
				if (i == lookbackPeriods)
				{
					num17 = num8;
					num18 = num9;
					num19 = num10;
				}
				else
				{
					num17 = num4 - num4 / (double)lookbackPeriods + num14;
					num18 = num5 - num5 / (double)lookbackPeriods + num15;
					num19 = num6 - num6 / (double)lookbackPeriods + num16;
				}
				num4 = num17;
				num5 = num18;
				num6 = num19;
				if (num17 != 0.0)
				{
					double num20 = 100.0 * num18 / num17;
					double num21 = 100.0 * num19 / num17;
					adxResult.Pdi = num20;
					adxResult.Mdi = num21;
					double num22 = ((num20 == num21) ? 0.0 : ((num20 + num21 != 0.0) ? (100.0 * Math.Abs(num20 - num21) / (num20 + num21)) : double.NaN));
					if (i > 2 * lookbackPeriods - 1)
					{
						double num23 = (num7 * (double)(lookbackPeriods - 1) + num22) / (double)lookbackPeriods;
						adxResult.Adx = num23.NaN2Null();
						adxResult.Adxr = (num23 + list[i + 1 - lookbackPeriods].Adx).NaN2Null() / 2.0;
						num7 = num23;
					}
					else if (i == 2 * lookbackPeriods - 1)
					{
						num11 += num22;
						double num23 = num11 / (double)lookbackPeriods;
						adxResult.Adx = num23.NaN2Null();
						num7 = num23;
					}
					else
					{
						num11 += num22;
					}
				}
			}
			return list;
		}
	}

	private static void ValidateAdx(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for ADX.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AdxResult> RemoveWarmupPeriods(this IEnumerable<AdxResult> results)
	{
		int num = results.ToList().FindIndex((AdxResult x) => x.Pdi.HasValue);
		return results.Remove(checked(2 * num + 100));
	}

	/// <summary>
	///     Williams Alligator is an indicator that transposes multiple moving averages,
	///     showing chart patterns that creator Bill Williams compared to an alligator's
	///     feeding habits when describing market movement.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Alligator/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="jawPeriods">Lookback periods for the Jaw line.</param><param name="jawOffset">Offset periods for the Jaw line.</param><param name="teethPeriods">Lookback periods for the Teeth line.</param><param name="teethOffset">Offset periods for the Teeth line.</param><param name="lipsPeriods">Lookback periods for the Lips line.</param><param name="lipsOffset">Offset periods for the Lips line.</param><returns>Time series of Alligator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AlligatorResult> GetAlligator<TQuote>(this IEnumerable<TQuote> quotes, int jawPeriods = 13, int jawOffset = 8, int teethPeriods = 8, int teethOffset = 5, int lipsPeriods = 5, int lipsOffset = 3) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).CalcAlligator(jawPeriods, jawOffset, teethPeriods, teethOffset, lipsPeriods, lipsOffset);
	}

	public static IEnumerable<AlligatorResult> GetAlligator(this IEnumerable<IReusableResult> results, int jawPeriods = 13, int jawOffset = 8, int teethPeriods = 8, int teethOffset = 5, int lipsPeriods = 5, int lipsOffset = 3)
	{
		return results.ToTuple().CalcAlligator(jawPeriods, jawOffset, teethPeriods, teethOffset, lipsPeriods, lipsOffset).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<AlligatorResult> GetAlligator(this IEnumerable<(DateTime, double)> priceTuples, int jawPeriods = 13, int jawOffset = 8, int teethPeriods = 8, int teethOffset = 5, int lipsPeriods = 5, int lipsOffset = 3)
	{
		return priceTuples.ToSortedList().CalcAlligator(jawPeriods, jawOffset, teethPeriods, teethOffset, lipsPeriods, lipsOffset);
	}

	internal static List<AlligatorResult> CalcAlligator(this List<(DateTime Date, double Value)> tpList, int jawPeriods, int jawOffset, int teethPeriods, int teethOffset, int lipsPeriods, int lipsOffset)
	{
		ValidateAlligator(jawPeriods, jawOffset, teethPeriods, teethOffset, lipsPeriods, lipsOffset);
		int count = tpList.Count;
		double[] array = new double[count];
		List<AlligatorResult> list = tpList.Select(((DateTime Date, double Value) x) => new AlligatorResult(x.Date)).ToList();
		checked
		{
			for (int num = 0; num < count; num++)
			{
				double item = tpList[num].Value;
				array[num] = item;
				if (num + jawOffset < count)
				{
					AlligatorResult alligatorResult = list[num + jawOffset];
					if (num + 1 == jawPeriods)
					{
						double num2 = 0.0;
						for (int num3 = num + 1 - jawPeriods; num3 <= num; num3++)
						{
							num2 += array[num3];
						}
						alligatorResult.Jaw = num2 / (double)jawPeriods;
					}
					else if (num + 1 > jawPeriods)
					{
						alligatorResult.Jaw = (list[num + jawOffset - 1].Jaw * (double)(jawPeriods - 1) + array[num]) / (double)jawPeriods;
					}
					alligatorResult.Jaw = alligatorResult.Jaw.NaN2Null();
				}
				if (num + teethOffset < count)
				{
					AlligatorResult alligatorResult2 = list[num + teethOffset];
					if (num + 1 == teethPeriods)
					{
						double num4 = 0.0;
						for (int num5 = num + 1 - teethPeriods; num5 <= num; num5++)
						{
							num4 += array[num5];
						}
						alligatorResult2.Teeth = num4 / (double)teethPeriods;
					}
					else if (num + 1 > teethPeriods)
					{
						alligatorResult2.Teeth = (list[num + teethOffset - 1].Teeth * (double)(teethPeriods - 1) + array[num]) / (double)teethPeriods;
					}
					alligatorResult2.Teeth = alligatorResult2.Teeth.NaN2Null();
				}
				if (num + lipsOffset >= count)
				{
					continue;
				}
				AlligatorResult alligatorResult3 = list[num + lipsOffset];
				if (num + 1 == lipsPeriods)
				{
					double num6 = 0.0;
					for (int num7 = num + 1 - lipsPeriods; num7 <= num; num7++)
					{
						num6 += array[num7];
					}
					alligatorResult3.Lips = num6 / (double)lipsPeriods;
				}
				else if (num + 1 > lipsPeriods)
				{
					alligatorResult3.Lips = (list[num + lipsOffset - 1].Lips * (double)(lipsPeriods - 1) + array[num]) / (double)lipsPeriods;
				}
				alligatorResult3.Lips = alligatorResult3.Lips.NaN2Null();
			}
			return list;
		}
	}

	private static void ValidateAlligator(int jawPeriods, int jawOffset, int teethPeriods, int teethOffset, int lipsPeriods, int lipsOffset)
	{
		if (jawPeriods <= teethPeriods)
		{
			throw new ArgumentOutOfRangeException("jawPeriods", jawPeriods, "Jaw lookback periods must be greater than Teeth lookback periods for Alligator.");
		}
		if (teethPeriods <= lipsPeriods)
		{
			throw new ArgumentOutOfRangeException("teethPeriods", teethPeriods, "Teeth lookback periods must be greater than Lips lookback periods for Alligator.");
		}
		if (lipsPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lipsPeriods", lipsPeriods, "Lips lookback periods must be greater than 0 for Alligator.");
		}
		if (jawOffset <= 0)
		{
			throw new ArgumentOutOfRangeException("jawOffset", jawOffset, "Jaw offset periods must be greater than 0 for Alligator.");
		}
		if (teethOffset <= 0)
		{
			throw new ArgumentOutOfRangeException("teethOffset", teethOffset, "Jaw offset periods must be greater than 0 for Alligator.");
		}
		if (lipsOffset <= 0)
		{
			throw new ArgumentOutOfRangeException("lipsOffset", lipsOffset, "Jaw offset periods must be greater than 0 for Alligator.");
		}
		checked
		{
			if (jawPeriods + jawOffset <= teethPeriods + teethOffset)
			{
				throw new ArgumentOutOfRangeException("jawPeriods", jawPeriods, "Jaw lookback + offset are too small for Alligator.");
			}
			if (teethPeriods + teethOffset <= lipsPeriods + lipsOffset)
			{
				throw new ArgumentOutOfRangeException("teethPeriods", teethPeriods, "Teeth lookback + offset are too small for Alligator.");
			}
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<AlligatorResult> Condense(this IEnumerable<AlligatorResult> results)
	{
		List<AlligatorResult> list = results.ToList();
		list.RemoveAll((AlligatorResult x) => !x.Jaw.HasValue && !x.Teeth.HasValue && !x.Lips.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AlligatorResult> RemoveWarmupPeriods(this IEnumerable<AlligatorResult> results)
	{
		int removePeriods = checked(results.ToList().FindIndex((AlligatorResult x) => x.Jaw.HasValue) + 251);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Arnaud Legoux Moving Average (ALMA) is a Gaussian distribution
	///     weighted moving average of price over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Alma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="offset">Adjusts smoothness versus responsiveness.</param><param name="sigma">Defines the width of the Gaussian normal distribution.</param><returns>Time series of ALMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AlmaResult> GetAlma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 9, double offset = 0.85, double sigma = 6.0) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcAlma(lookbackPeriods, offset, sigma);
	}

	public static IEnumerable<AlmaResult> GetAlma(this IEnumerable<IReusableResult> results, int lookbackPeriods = 9, double offset = 0.85, double sigma = 6.0)
	{
		return results.ToTuple().CalcAlma(lookbackPeriods, offset, sigma).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<AlmaResult> GetAlma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods = 9, double offset = 0.85, double sigma = 6.0)
	{
		return priceTuples.ToSortedList().CalcAlma(lookbackPeriods, offset, sigma);
	}

	internal static List<AlmaResult> CalcAlma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offset, double sigma)
	{
		ValidateAlma(lookbackPeriods, offset, sigma);
		List<AlmaResult> list = new List<AlmaResult>(tpList.Count);
		checked
		{
			double num = offset * (double)(lookbackPeriods - 1);
			double num2 = (double)lookbackPeriods / sigma;
			double[] array = new double[lookbackPeriods];
			double num3 = 0.0;
			for (int i = 0; i < lookbackPeriods; i++)
			{
				num3 += (array[i] = Math.Exp((0.0 - ((double)i - num) * ((double)i - num)) / (2.0 * num2 * num2)));
			}
			for (int j = 0; j < tpList.Count; j++)
			{
				AlmaResult almaResult = new AlmaResult(tpList[j].Item1);
				list.Add(almaResult);
				if (j + 1 >= lookbackPeriods)
				{
					double? num4 = 0.0;
					int num5 = 0;
					for (int k = j + 1 - lookbackPeriods; k <= j; k++)
					{
						double item = tpList[k].Item2;
						num4 += array[num5] * item;
						num5++;
					}
					almaResult.Alma = (num4 / num3).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateAlma(int lookbackPeriods, double offset, double sigma)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for ALMA.");
		}
		if ((offset < 0.0 || offset > 1.0) ? true : false)
		{
			throw new ArgumentOutOfRangeException("offset", offset, "Offset must be between 0 and 1 for ALMA.");
		}
		if (sigma <= 0.0)
		{
			throw new ArgumentOutOfRangeException("sigma", sigma, "Sigma must be greater than 0 for ALMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AlmaResult> RemoveWarmupPeriods(this IEnumerable<AlmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((AlmaResult x) => x.Alma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Aroon is a simple oscillator view of how long the new high or low price occured over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Aroon/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Aroon Up/Down and Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AroonResult> GetAroon<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 25) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcAroon(lookbackPeriods);
	}

	internal static List<AroonResult> CalcAroon(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateAroon(lookbackPeriods);
		List<AroonResult> list = new List<AroonResult>(qdList.Count);
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				AroonResult aroonResult = new AroonResult(qdList[i].Date);
				list.Add(aroonResult);
				if (i + 1 <= lookbackPeriods)
				{
					continue;
				}
				double? num = 0.0;
				double? num2 = double.MaxValue;
				int num3 = 0;
				int num4 = 0;
				for (int j = i + 1 - lookbackPeriods - 1; j <= i; j++)
				{
					QuoteD quoteD = qdList[j];
					if (quoteD.High > num)
					{
						num = quoteD.High;
						num3 = j + 1;
					}
					if (quoteD.Low < num2)
					{
						num2 = quoteD.Low;
						num4 = j + 1;
					}
				}
				aroonResult.AroonUp = 100.0 * (double)(lookbackPeriods - (i + 1 - num3)) / (double)lookbackPeriods;
				aroonResult.AroonDown = 100.0 * (double)(lookbackPeriods - (i + 1 - num4)) / (double)lookbackPeriods;
				aroonResult.Oscillator = aroonResult.AroonUp - aroonResult.AroonDown;
			}
			return list;
		}
	}

	private static void ValidateAroon(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Aroon.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AroonResult> RemoveWarmupPeriods(this IEnumerable<AroonResult> results)
	{
		int removePeriods = results.ToList().FindIndex((AroonResult x) => x.Oscillator.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Average True Range (ATR) is a measure of volatility that captures gaps and limits between periods.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Atr/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of ATR values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AtrResult> GetAtr<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcAtr(lookbackPeriods);
	}

	internal static List<AtrResult> CalcAtr(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateAtr(lookbackPeriods);
		List<AtrResult> list = new List<AtrResult>(qdList.Count);
		double num = double.NaN;
		double num2 = double.NaN;
		double num3 = 0.0;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				AtrResult atrResult = new AtrResult(quoteD.Date);
				list.Add(atrResult);
				if (i > 0)
				{
					double val = Math.Abs(quoteD.High - num2);
					double val2 = Math.Abs(quoteD.Low - num2);
					double num4 = Math.Max(quoteD.High - quoteD.Low, Math.Max(val, val2));
					atrResult.Tr = num4;
					if (i > lookbackPeriods)
					{
						double num5 = (num * (double)(lookbackPeriods - 1) + num4) / (double)lookbackPeriods;
						atrResult.Atr = num5;
						atrResult.Atrp = ((quoteD.Close == 0.0) ? ((double?)null) : new double?(num5 / quoteD.Close * 100.0));
						num = num5;
					}
					else if (i == lookbackPeriods)
					{
						num3 += num4;
						double num6 = num3 / (double)lookbackPeriods;
						atrResult.Atr = num6;
						atrResult.Atrp = ((quoteD.Close == 0.0) ? ((double?)null) : new double?(num6 / quoteD.Close * 100.0));
						num = num6;
					}
					else
					{
						num3 += num4;
					}
					num2 = quoteD.Close;
				}
				else
				{
					num2 = quoteD.Close;
				}
			}
			return list;
		}
	}

	private static void ValidateAtr(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Average True Range.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AtrResult> RemoveWarmupPeriods(this IEnumerable<AtrResult> results)
	{
		int removePeriods = results.ToList().FindIndex((AtrResult x) => x.Atr.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     ATR Trailing Stop attempts to determine the primary trend of prices by using
	///     Average True Range (ATR) band thresholds. It can indicate a buy/sell signal or a
	///     trailing stop when the trend changes.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/AtrStop/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for ATR.</param><param name="multiplier">Multiplier sets the ATR band width.</param><param name="endType">Sets basis for stop offsets (Close or High/Low).</param><returns>Time series of ATR Trailing Stop values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AtrStopResult> GetAtrStop<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 21, double multiplier = 3.0, EndType endType = EndType.Close) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcAtrStop(lookbackPeriods, multiplier, endType);
	}

	internal static List<AtrStopResult> CalcAtrStop(this List<QuoteD> qdList, int lookbackPeriods, double multiplier, EndType endType)
	{
		ValidateAtrStop(lookbackPeriods, multiplier);
		List<AtrStopResult> list = new List<AtrStopResult>(qdList.Count);
		List<AtrResult> list2 = qdList.CalcAtr(lookbackPeriods);
		bool flag = true;
		double? num = null;
		double? num2 = null;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				AtrStopResult atrStopResult = new AtrStopResult(quoteD.Date);
				list.Add(atrStopResult);
				if (i >= lookbackPeriods)
				{
					double? atr = list2[i].Atr;
					QuoteD quoteD2 = qdList[i - 1];
					double? num3;
					double? num4;
					if (endType == EndType.Close)
					{
						num3 = quoteD.Close + multiplier * atr;
						num4 = quoteD.Close - multiplier * atr;
					}
					else
					{
						num3 = quoteD.High + multiplier * atr;
						num4 = quoteD.Low - multiplier * atr;
					}
					if (i == lookbackPeriods)
					{
						flag = quoteD.Close >= quoteD2.Close;
						num = num3;
						num2 = num4;
					}
					if (num3 < num || quoteD2.Close > num)
					{
						num = num3;
					}
					if (num4 > num2 || quoteD2.Close < num2)
					{
						num2 = num4;
					}
					if (quoteD.Close <= (flag ? num2 : num))
					{
						atrStopResult.AtrStop = (decimal?)num;
						atrStopResult.BuyStop = (decimal?)num;
						flag = false;
					}
					else
					{
						atrStopResult.AtrStop = (decimal?)num2;
						atrStopResult.SellStop = (decimal?)num2;
						flag = true;
					}
				}
			}
			return list;
		}
	}

	private static void ValidateAtrStop(int lookbackPeriods, double multiplier)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for ATR Trailing Stop.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "Multiplier must be greater than 0 for ATR Trailing Stop.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<AtrStopResult> Condense(this IEnumerable<AtrStopResult> results)
	{
		List<AtrStopResult> list = results.ToList();
		list.RemoveAll((AtrStopResult x) => !x.AtrStop.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AtrStopResult> RemoveWarmupPeriods(this IEnumerable<AtrStopResult> results)
	{
		int removePeriods = results.ToList().FindIndex((AtrStopResult x) => x.AtrStop.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Awesome Oscillator (aka Super AO) is a measure of the gap between a fast and slow period modified moving average.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Awesome/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastPeriods">Number of periods in the Fast moving average.</param><param name="slowPeriods">Number of periods in the Slow moving average.</param><returns>Time series of Awesome Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<AwesomeResult> GetAwesome<TQuote>(this IEnumerable<TQuote> quotes, int fastPeriods = 5, int slowPeriods = 34) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).CalcAwesome(fastPeriods, slowPeriods);
	}

	public static IEnumerable<AwesomeResult> GetAwesome(this IEnumerable<IReusableResult> results, int fastPeriods = 5, int slowPeriods = 34)
	{
		return results.ToTuple().CalcAwesome(fastPeriods, slowPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<AwesomeResult> GetAwesome(this IEnumerable<(DateTime, double)> priceTuples, int fastPeriods = 5, int slowPeriods = 34)
	{
		return priceTuples.ToSortedList().CalcAwesome(fastPeriods, slowPeriods);
	}

	internal static List<AwesomeResult> CalcAwesome(this List<(DateTime, double)> tpList, int fastPeriods, int slowPeriods)
	{
		ValidateAwesome(fastPeriods, slowPeriods);
		int count = tpList.Count;
		List<AwesomeResult> list = new List<AwesomeResult>(count);
		double[] array = new double[count];
		checked
		{
			for (int i = 0; i < count; i++)
			{
				var (date, num) = tpList[i];
				array[i] = num;
				AwesomeResult awesomeResult = new AwesomeResult(date);
				list.Add(awesomeResult);
				if (i + 1 < slowPeriods)
				{
					continue;
				}
				double num2 = 0.0;
				double num3 = 0.0;
				for (int j = i + 1 - slowPeriods; j <= i; j++)
				{
					num2 += array[j];
					if (j >= i + 1 - fastPeriods)
					{
						num3 += array[j];
					}
				}
				awesomeResult.Oscillator = (num3 / (double)fastPeriods - num2 / (double)slowPeriods).NaN2Null();
				awesomeResult.Normalized = ((array[i] == 0.0) ? ((double?)null) : (100.0 * awesomeResult.Oscillator / array[i]));
			}
			return list;
		}
	}

	private static void ValidateAwesome(int fastPeriods, int slowPeriods)
	{
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Fast periods must be greater than 0 for Awesome Oscillator.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow periods must be larger than Fast Periods for Awesome Oscillator.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<AwesomeResult> RemoveWarmupPeriods(this IEnumerable<AwesomeResult> results)
	{
		int removePeriods = results.ToList().FindIndex((AwesomeResult x) => x.Oscillator.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     A simple quote transform.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/BasicQuote/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="candlePart">The OHLCV element or simply calculated value type.</param><returns>Time series of Basic Quote values.</returns><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Invalid candle part provided.</exception>
	public static IEnumerable<BasicData> GetBaseQuote<TQuote>(this IEnumerable<TQuote> quotes, CandlePart candlePart = CandlePart.Close) where TQuote : IQuote
	{
		return from q in quotes
			select q.ToBasicData(candlePart) into x
			orderby x.Date
			select x;
	}

	/// <summary>
	///     Beta shows how strongly one stock responds to systemic volatility of the entire market.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Beta/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotesEval">Historical price quotes for Evaluation.</param><param name="quotesMarket">Historical price quotes for Market.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="type">Type of Beta to calculate.</param><returns>Time series of Beta values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Invalid quotes provided.</exception>
	public static IEnumerable<BetaResult> GetBeta<TQuote>(this IEnumerable<TQuote> quotesEval, IEnumerable<TQuote> quotesMarket, int lookbackPeriods, BetaType type = BetaType.Standard) where TQuote : IQuote
	{
		List<(DateTime, double)> tpListEval = quotesEval.ToTuple(CandlePart.Close);
		List<(DateTime, double)> tpListMrkt = quotesMarket.ToTuple(CandlePart.Close);
		return CalcBeta(tpListEval, tpListMrkt, lookbackPeriods, type);
	}

	public static IEnumerable<BetaResult> GetBeta(this IEnumerable<IReusableResult> evalResults, IEnumerable<IReusableResult> mrktResults, int lookbackPeriods, BetaType type = BetaType.Standard)
	{
		List<(DateTime Date, double Value)> tpListEval = evalResults.ToTuple();
		List<(DateTime, double)> tpListMrkt = mrktResults.ToTuple();
		return CalcBeta(tpListEval, tpListMrkt, lookbackPeriods, type).SyncIndex(evalResults, SyncType.Prepend);
	}

	public static IEnumerable<BetaResult> GetBeta(this IEnumerable<(DateTime, double)> evalTuple, IEnumerable<(DateTime, double)> mrktTuple, int lookbackPeriods, BetaType type = BetaType.Standard)
	{
		List<(DateTime, double)> tpListEval = evalTuple.ToSortedList();
		List<(DateTime, double)> tpListMrkt = mrktTuple.ToSortedList();
		return CalcBeta(tpListEval, tpListMrkt, lookbackPeriods, type);
	}

	internal static List<BetaResult> CalcBeta(List<(DateTime, double)> tpListEval, List<(DateTime, double)> tpListMrkt, int lookbackPeriods, BetaType type = BetaType.Standard)
	{
		ValidateBeta(tpListEval, tpListMrkt, lookbackPeriods);
		int count = tpListEval.Count;
		List<BetaResult> list = new List<BetaResult>(count);
		bool flag = ((type == BetaType.Standard || type == BetaType.All) ? true : false);
		bool flag2 = flag;
		flag = ((type == BetaType.Up || type == BetaType.All) ? true : false);
		bool flag3 = flag;
		flag = (uint)(type - 2) <= 1u;
		bool flag4 = flag;
		double[] array = new double[count];
		double[] array2 = new double[count];
		double num = 0.0;
		double num2 = 0.0;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				var (dateTime, num3) = tpListEval[i];
				var (dateTime2, num4) = tpListMrkt[i];
				if (dateTime != dateTime2)
				{
					throw new InvalidQuotesException("tpListEval", dateTime, "Date sequence does not match.  Beta requires matching dates in provided quotes.");
				}
				array[i] = ((num != 0.0) ? (num3 / num - 1.0) : 0.0);
				array2[i] = ((num2 != 0.0) ? (num4 / num2 - 1.0) : 0.0);
				num = num3;
				num2 = num4;
			}
			for (int j = 0; j < count; j++)
			{
				BetaResult betaResult = new BetaResult(tpListEval[j].Item1)
				{
					ReturnsEval = array[j],
					ReturnsMrkt = array2[j]
				};
				list.Add(betaResult);
				if (j >= lookbackPeriods)
				{
					if (flag2)
					{
						betaResult.CalcBetaWindow(j, lookbackPeriods, array2, array, BetaType.Standard);
					}
					if (flag4)
					{
						betaResult.CalcBetaWindow(j, lookbackPeriods, array2, array, BetaType.Down);
					}
					if (flag3)
					{
						betaResult.CalcBetaWindow(j, lookbackPeriods, array2, array, BetaType.Up);
					}
					if (type == BetaType.All && betaResult.BetaUp.HasValue && betaResult.BetaDown.HasValue)
					{
						betaResult.Ratio = ((betaResult.BetaDown == 0.0) ? ((double?)null) : (betaResult.BetaUp / betaResult.BetaDown));
						betaResult.Convexity = (betaResult.BetaUp - betaResult.BetaDown) * (betaResult.BetaUp - betaResult.BetaDown);
					}
				}
			}
			return list;
		}
	}

	private static void CalcBetaWindow(this BetaResult r, int i, int lookbackPeriods, double[] mrktReturns, double[] evalReturns, BetaType type)
	{
		CorrResult corrResult = new CorrResult(r.Date);
		List<double> list = new List<double>(lookbackPeriods);
		List<double> list2 = new List<double>(lookbackPeriods);
		checked
		{
			for (int j = i - lookbackPeriods + 1; j <= i; j++)
			{
				double num = mrktReturns[j];
				double item = evalReturns[j];
				if (type == BetaType.Standard || (type == BetaType.Down && num < 0.0) || (type == BetaType.Up && num > 0.0))
				{
					list.Add(num);
					list2.Add(item);
				}
			}
			if (list.Count <= 0)
			{
				return;
			}
			corrResult.PeriodCorrelation(list.ToArray(), list2.ToArray());
			if (corrResult.Covariance.HasValue && corrResult.VarianceA.HasValue && corrResult.VarianceA != 0.0)
			{
				double? num2 = (corrResult.Covariance / corrResult.VarianceA).NaN2Null();
				switch (type)
				{
				case BetaType.Standard:
					r.Beta = num2;
					break;
				case BetaType.Down:
					r.BetaDown = num2;
					break;
				case BetaType.Up:
					r.BetaUp = num2;
					break;
				}
			}
		}
	}

	private static void ValidateBeta(List<(DateTime, double)> tpListEval, List<(DateTime, double)> tpListMrkt, int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Beta.");
		}
		if (tpListEval.Count != tpListMrkt.Count)
		{
			throw new InvalidQuotesException("tpListEval", "Eval quotes should have the same number of Market quotes for Beta.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<BetaResult> RemoveWarmupPeriods(this IEnumerable<BetaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((BetaResult x) => x.Beta.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Bollinger Bands® depict volatility as standard deviation boundary lines from a moving average of price.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/BollingerBands/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="standardDeviations">Width of bands. Number of Standard Deviations from the moving average.</param><returns>Time series of Bollinger Band and %B values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<BollingerBandsResult> GetBollingerBands<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 20, double standardDeviations = 2.0) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcBollingerBands(lookbackPeriods, standardDeviations);
	}

	public static IEnumerable<BollingerBandsResult> GetBollingerBands(this IEnumerable<IReusableResult> results, int lookbackPeriods = 20, double standardDeviations = 2.0)
	{
		return results.ToTuple().CalcBollingerBands(lookbackPeriods, standardDeviations).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<BollingerBandsResult> GetBollingerBands(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods = 20, double standardDeviations = 2.0)
	{
		return priceTuples.ToSortedList().CalcBollingerBands(lookbackPeriods, standardDeviations);
	}

	internal static List<BollingerBandsResult> CalcBollingerBands(this List<(DateTime, double)> tpList, int lookbackPeriods, double standardDeviations)
	{
		ValidateBollingerBands(lookbackPeriods, standardDeviations);
		List<BollingerBandsResult> list = new List<BollingerBandsResult>(tpList.Count);
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				BollingerBandsResult bollingerBandsResult = new BollingerBandsResult(item);
				list.Add(bollingerBandsResult);
				if (i + 1 >= lookbackPeriods)
				{
					double[] array = new double[lookbackPeriods];
					double num = 0.0;
					int num2 = 0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						num += (array[num2] = tpList[j].Item2);
						num2++;
					}
					double? num3 = (num / (double)lookbackPeriods).NaN2Null();
					double? num4 = array.StdDev().NaN2Null();
					bollingerBandsResult.Sma = num3;
					bollingerBandsResult.UpperBand = num3 + standardDeviations * num4;
					bollingerBandsResult.LowerBand = num3 - standardDeviations * num4;
					bollingerBandsResult.PercentB = ((bollingerBandsResult.UpperBand == bollingerBandsResult.LowerBand) ? ((double?)null) : ((item2 - bollingerBandsResult.LowerBand) / (bollingerBandsResult.UpperBand - bollingerBandsResult.LowerBand)));
					bollingerBandsResult.ZScore = ((num4 == 0.0) ? ((double?)null) : ((item2 - bollingerBandsResult.Sma) / num4));
					bollingerBandsResult.Width = ((num3 == 0.0) ? ((double?)null) : ((bollingerBandsResult.UpperBand - bollingerBandsResult.LowerBand) / num3));
				}
			}
			return list;
		}
	}

	private static void ValidateBollingerBands(int lookbackPeriods, double standardDeviations)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Bollinger Bands.");
		}
		if (standardDeviations <= 0.0)
		{
			throw new ArgumentOutOfRangeException("standardDeviations", standardDeviations, "Standard Deviations must be greater than 0 for Bollinger Bands.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<BollingerBandsResult> RemoveWarmupPeriods(this IEnumerable<BollingerBandsResult> results)
	{
		int removePeriods = results.ToList().FindIndex((BollingerBandsResult x) => x.Width.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Balance of Power (aka Balance of Market Power) is a momentum oscillator that depicts the strength of buying and selling pressure.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Bop/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="smoothPeriods">Number of periods for smoothing.</param><returns>Time series of BOP values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<BopResult> GetBop<TQuote>(this IEnumerable<TQuote> quotes, int smoothPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcBop(smoothPeriods);
	}

	internal static List<BopResult> CalcBop(this List<QuoteD> qdList, int smoothPeriods)
	{
		ValidateBop(smoothPeriods);
		int count = qdList.Count;
		List<BopResult> list = new List<BopResult>(count);
		double[] array = qdList.Select((QuoteD x) => (x.High == x.Low) ? double.NaN : ((x.Close - x.Open) / (x.High - x.Low))).ToArray();
		checked
		{
			for (int num = 0; num < count; num++)
			{
				BopResult bopResult = new BopResult(qdList[num].Date);
				list.Add(bopResult);
				if (num >= smoothPeriods - 1)
				{
					double num2 = 0.0;
					for (int num3 = num - smoothPeriods + 1; num3 <= num; num3++)
					{
						num2 += array[num3];
					}
					bopResult.Bop = (num2 / (double)smoothPeriods).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateBop(int smoothPeriods)
	{
		if (smoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("smoothPeriods", smoothPeriods, "Smoothing periods must be greater than 0 for BOP.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<BopResult> RemoveWarmupPeriods(this IEnumerable<BopResult> results)
	{
		int removePeriods = results.ToList().FindIndex((BopResult x) => x.Bop.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Commodity Channel Index (CCI) is an oscillator depicting deviation from typical price range, often used to identify cyclical trends.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Cci/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of CCI values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<CciResult> GetCci<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 20) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcCci(lookbackPeriods);
	}

	internal static List<CciResult> CalcCci(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateCci(lookbackPeriods);
		int count = qdList.Count;
		List<CciResult> list = new List<CciResult>(count);
		double[] array = new double[count];
		checked
		{
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				array[i] = (quoteD.High + quoteD.Low + quoteD.Close) / 3.0;
				CciResult cciResult = new CciResult(quoteD.Date);
				list.Add(cciResult);
				if (i + 1 >= lookbackPeriods)
				{
					double num = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						num += array[j];
					}
					num /= (double)lookbackPeriods;
					double num2 = 0.0;
					for (int k = i + 1 - lookbackPeriods; k <= i; k++)
					{
						num2 += Math.Abs(num - array[k]);
					}
					num2 /= (double)lookbackPeriods;
					cciResult.Cci = ((num2 == 0.0) ? ((double?)null) : ((array[i] - num) / (0.015 * num2)).NaN2Null());
				}
			}
			return list;
		}
	}

	private static void ValidateCci(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Commodity Channel Index.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<CciResult> RemoveWarmupPeriods(this IEnumerable<CciResult> results)
	{
		int removePeriods = results.ToList().FindIndex((CciResult x) => x.Cci.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Chaikin Oscillator is the difference between fast and slow Exponential Moving Averages (EMA) of the Accumulation/Distribution Line (ADL).
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/ChaikinOsc/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastPeriods">Number of periods for the ADL fast EMA.</param><param name="slowPeriods">Number of periods for the ADL slow EMA.</param><returns>Time series of Chaikin Oscillator, Money Flow Volume, and ADL values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ChaikinOscResult> GetChaikinOsc<TQuote>(this IEnumerable<TQuote> quotes, int fastPeriods = 3, int slowPeriods = 10) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcChaikinOsc(fastPeriods, slowPeriods);
	}

	internal static List<ChaikinOscResult> CalcChaikinOsc(this List<QuoteD> qdList, int fastPeriods, int slowPeriods)
	{
		ValidateChaikinOsc(fastPeriods, slowPeriods);
		List<ChaikinOscResult> list = (from r in qdList.CalcAdl(null)
			select new ChaikinOscResult(r.Date)
			{
				MoneyFlowMultiplier = r.MoneyFlowMultiplier,
				MoneyFlowVolume = r.MoneyFlowVolume,
				Adl = r.Adl
			}).ToList();
		List<(DateTime Date, double)> tpList = list.Select((ChaikinOscResult x) => (Date: x.Date, x.Adl ?? double.NaN)).ToList();
		List<EmaResult> list2 = tpList.CalcEma(slowPeriods);
		List<EmaResult> list3 = tpList.CalcEma(fastPeriods);
		checked
		{
			for (int num = slowPeriods - 1; num < list.Count; num++)
			{
				ChaikinOscResult chaikinOscResult = list[num];
				EmaResult emaResult = list3[num];
				EmaResult emaResult2 = list2[num];
				chaikinOscResult.Oscillator = emaResult.Ema - emaResult2.Ema;
			}
			return list;
		}
	}

	private static void ValidateChaikinOsc(int fastPeriods, int slowPeriods)
	{
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast lookback periods must be greater than 0 for Chaikin Oscillator.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow lookback periods must be greater than Fast lookback period for Chaikin Oscillator.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ChaikinOscResult> RemoveWarmupPeriods(this IEnumerable<ChaikinOscResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((ChaikinOscResult x) => x.Oscillator.HasValue) + 1;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///     Chandelier Exit is typically used for stop-loss and can be computed for both long or short types.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Chandelier/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="multiplier">Multiplier.</param><param name="type">Short or Long variant selection.</param><returns>Time series of Chandelier Exit values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ChandelierResult> GetChandelier<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 22, double multiplier = 3.0, ChandelierType type = ChandelierType.Long) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcChandelier(lookbackPeriods, multiplier, type);
	}

	internal static List<ChandelierResult> CalcChandelier(this List<QuoteD> qdList, int lookbackPeriods, double multiplier, ChandelierType type)
	{
		ValidateChandelier(lookbackPeriods, multiplier);
		int count = qdList.Count;
		List<ChandelierResult> list = new List<ChandelierResult>(count);
		List<AtrResult> list2 = qdList.CalcAtr(lookbackPeriods).ToList();
		checked
		{
			for (int i = 0; i < count; i++)
			{
				ChandelierResult chandelierResult = new ChandelierResult(qdList[i].Date);
				list.Add(chandelierResult);
				if (i < lookbackPeriods)
				{
					continue;
				}
				double? atr = list2[i].Atr;
				switch (type)
				{
				case ChandelierType.Long:
				{
					double num2 = 0.0;
					for (int k = i + 1 - lookbackPeriods; k <= i; k++)
					{
						QuoteD quoteD2 = qdList[k];
						if (quoteD2.High > num2)
						{
							num2 = quoteD2.High;
						}
					}
					chandelierResult.ChandelierExit = num2 - atr * multiplier;
					break;
				}
				case ChandelierType.Short:
				{
					double num = double.MaxValue;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						QuoteD quoteD = qdList[j];
						if (quoteD.Low < num)
						{
							num = quoteD.Low;
						}
					}
					chandelierResult.ChandelierExit = num + atr * multiplier;
					break;
				}
				default:
					throw new ArgumentOutOfRangeException("type");
				}
			}
			return list;
		}
	}

	private static void ValidateChandelier(int lookbackPeriods, double multiplier)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Chandelier Exit.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "Multiplier must be greater than 0 for Chandelier Exit.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ChandelierResult> RemoveWarmupPeriods(this IEnumerable<ChandelierResult> results)
	{
		int removePeriods = results.ToList().FindIndex((ChandelierResult x) => x.ChandelierExit.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Choppiness Index (CHOP) measures the trendiness or choppiness over N lookback periods
	///     on a scale of 0 to 100.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Chop/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of CHOP values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ChopResult> GetChop<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcChop(lookbackPeriods);
	}

	internal static List<ChopResult> CalcChop(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateChop(lookbackPeriods);
		int count = qdList.Count;
		List<ChopResult> list = new List<ChopResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		double[] array3 = new double[count];
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				ChopResult chopResult = new ChopResult(qdList[i].Date);
				list.Add(chopResult);
				if (i <= 0)
				{
					continue;
				}
				array[i] = Math.Max(qdList[i].High, qdList[i - 1].Close);
				array2[i] = Math.Min(qdList[i].Low, qdList[i - 1].Close);
				array3[i] = array[i] - array2[i];
				if (i >= lookbackPeriods)
				{
					double num = array3[i];
					double num2 = array[i];
					double num3 = array2[i];
					for (int j = 1; j < lookbackPeriods; j++)
					{
						num += array3[i - j];
						num2 = Math.Max(num2, array[i - j]);
						num3 = Math.Min(num3, array2[i - j]);
					}
					double num4 = num2 - num3;
					if (num4 != 0.0)
					{
						chopResult.Chop = 100.0 * (Math.Log(num / num4) / Math.Log(lookbackPeriods));
					}
				}
			}
			return list;
		}
	}

	private static void ValidateChop(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for CHOP.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ChopResult> RemoveWarmupPeriods(this IEnumerable<ChopResult> results)
	{
		int removePeriods = results.ToList().FindIndex((ChopResult x) => x.Chop.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Chaikin Money Flow (CMF) is the simple moving average of Money Flow Volume (MFV).
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Cmf/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the MFV moving average.</param><returns>Time series of Chaikin Money Flow and MFV values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<CmfResult> GetCmf<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 20) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcCmf(lookbackPeriods);
	}

	internal static List<CmfResult> CalcCmf(this List<QuoteD> qdList, int lookbackPeriods)
	{
		List<(DateTime, double)> list = qdList.ToTuple(CandlePart.Volume);
		ValidateCmf(lookbackPeriods);
		int count = list.Count;
		List<CmfResult> list2 = new List<CmfResult>(count);
		List<AdlResult> list3 = qdList.CalcAdl(null).ToList();
		checked
		{
			for (int i = 0; i < count; i++)
			{
				AdlResult adlResult = list3[i];
				CmfResult cmfResult = new CmfResult(adlResult.Date)
				{
					MoneyFlowMultiplier = adlResult.MoneyFlowMultiplier,
					MoneyFlowVolume = adlResult.MoneyFlowVolume
				};
				list2.Add(cmfResult);
				if (i >= lookbackPeriods - 1)
				{
					double? num = 0.0;
					double? num2 = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						num2 += list[j].Item2;
						num += list3[j].MoneyFlowVolume;
					}
					double? num3 = num / (double)lookbackPeriods;
					double? num4 = num2 / (double)lookbackPeriods;
					if (num4 != 0.0)
					{
						cmfResult.Cmf = num3 / num4;
					}
				}
			}
			return list2;
		}
	}

	private static void ValidateCmf(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Chaikin Money Flow.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<CmfResult> RemoveWarmupPeriods(this IEnumerable<CmfResult> results)
	{
		int removePeriods = results.ToList().FindIndex((CmfResult x) => x.Cmf.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///       The Chande Momentum Oscillator is a momentum indicator depicting the weighted percent of higher prices in financial markets.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Cmo/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of CMO values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<CmoResult> GetCmo<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcCmo(lookbackPeriods);
	}

	public static IEnumerable<CmoResult> GetCmo(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcCmo(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<CmoResult> GetCmo(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcCmo(lookbackPeriods);
	}

	internal static List<CmoResult> CalcCmo(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateCmo(lookbackPeriods);
		int count = tpList.Count;
		List<CmoResult> list = new List<CmoResult>(count);
		List<(bool?, double)> list2 = new List<(bool?, double)>(count);
		double num = double.NaN;
		if (count > 0)
		{
			list.Add(new CmoResult(tpList[0].Item1));
			list2.Add((null, double.NaN));
			num = tpList[0].Item2;
		}
		checked
		{
			for (int i = 1; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				CmoResult cmoResult = new CmoResult(item);
				list.Add(cmoResult);
				list2.Add(((item2 > num) ? new bool?(true) : ((item2 < num) ? new bool?(false) : ((bool?)null)), Math.Abs(item2 - num)));
				if (i >= lookbackPeriods)
				{
					double num2 = 0.0;
					double num3 = 0.0;
					for (int j = i - lookbackPeriods + 1; j <= i; j++)
					{
						var (flag, num4) = list2[j];
						if (flag.HasValue)
						{
							if (flag == true)
							{
								num2 += num4;
							}
							else
							{
								num3 += num4;
							}
						}
					}
					cmoResult.Cmo = ((num2 + num3 != 0.0) ? (100.0 * (num2 - num3) / (num2 + num3)).NaN2Null() : ((double?)null));
				}
				num = item2;
			}
			return list;
		}
	}

	private static void ValidateCmo(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for CMO.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<CmoResult> RemoveWarmupPeriods(this IEnumerable<CmoResult> results)
	{
		int removePeriods = results.ToList().FindIndex((CmoResult x) => x.Cmo.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     ConnorsRSI is a composite oscillator that incorporates RSI, winning/losing streaks, and percentile gain metrics on scale of 0 to 100.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/ConnorsRsi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="rsiPeriods">Number of periods in the RSI.</param><param name="streakPeriods">Number of periods for streak RSI.</param><param name="rankPeriods">Number of periods for the percentile ranking.</param><returns>Time series of ConnorsRSI, RSI, Streak RSI, and Percent Rank values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ConnorsRsiResult> GetConnorsRsi<TQuote>(this IEnumerable<TQuote> quotes, int rsiPeriods = 3, int streakPeriods = 2, int rankPeriods = 100) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcConnorsRsi(rsiPeriods, streakPeriods, rankPeriods);
	}

	public static IEnumerable<ConnorsRsiResult> GetConnorsRsi(this IEnumerable<IReusableResult> results, int rsiPeriods = 3, int streakPeriods = 2, int rankPeriods = 100)
	{
		return results.ToTuple().CalcConnorsRsi(rsiPeriods, streakPeriods, rankPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<ConnorsRsiResult> GetConnorsRsi(this IEnumerable<(DateTime, double)> priceTuples, int rsiPeriods = 3, int streakPeriods = 2, int rankPeriods = 100)
	{
		return priceTuples.ToSortedList().CalcConnorsRsi(rsiPeriods, streakPeriods, rankPeriods);
	}

	internal static List<ConnorsRsiResult> CalcConnorsRsi(this List<(DateTime, double)> tpList, int rsiPeriods, int streakPeriods, int rankPeriods)
	{
		ValidateConnorsRsi(rsiPeriods, streakPeriods, rankPeriods);
		List<ConnorsRsiResult> list = tpList.CalcStreak(rsiPeriods, rankPeriods);
		checked
		{
			int num = Math.Max(rsiPeriods, Math.Max(streakPeriods, rankPeriods)) + 2;
			int count = list.Count;
			List<RsiResult> list2 = (from x in list.Remove(Math.Min(count, 1))
				select ((DateTime Date, double))(Date: x.Date, x.Streak)).ToList().CalcRsi(streakPeriods);
			for (int num2 = streakPeriods + 2; num2 < count; num2++)
			{
				ConnorsRsiResult connorsRsiResult = list[num2];
				RsiResult rsiResult = list2[num2 - 1];
				connorsRsiResult.RsiStreak = rsiResult.Rsi;
				if (num2 + 1 >= num)
				{
					connorsRsiResult.ConnorsRsi = (connorsRsiResult.Rsi + connorsRsiResult.RsiStreak + connorsRsiResult.PercentRank) / 3.0;
				}
			}
			return list;
		}
	}

	private static List<ConnorsRsiResult> CalcStreak(this List<(DateTime Date, double Streak)> tpList, int rsiPeriods, int rankPeriods)
	{
		List<RsiResult> list = tpList.CalcRsi(rsiPeriods);
		int count = tpList.Count;
		List<ConnorsRsiResult> list2 = new List<ConnorsRsiResult>(count);
		double[] array = new double[count];
		double num = double.NaN;
		int num2 = 0;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				(DateTime Date, double Streak) tuple = tpList[i];
				DateTime item = tuple.Date;
				double item2 = tuple.Streak;
				ConnorsRsiResult connorsRsiResult = new ConnorsRsiResult(item)
				{
					Rsi = list[i].Rsi
				};
				list2.Add(connorsRsiResult);
				if (i == 0)
				{
					num = item2;
					continue;
				}
				num2 = (connorsRsiResult.Streak = ((item2 != num) ? ((item2 > num) ? ((num2 < 0) ? 1 : (num2 + 1)) : ((num2 > 0) ? (-1) : (num2 - 1))) : 0));
				array[i] = ((num <= 0.0) ? double.NaN : ((item2 - num) / num));
				if (i + 1 > rankPeriods)
				{
					int num4 = 0;
					for (int j = i - rankPeriods; j <= i; j++)
					{
						if (array[j] < array[i])
						{
							num4++;
						}
					}
					unchecked
					{
						connorsRsiResult.PercentRank = checked(100 * num4) / rankPeriods;
					}
				}
				num = item2;
			}
			return list2;
		}
	}

	private static void ValidateConnorsRsi(int rsiPeriods, int streakPeriods, int rankPeriods)
	{
		if (rsiPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("rsiPeriods", rsiPeriods, "RSI period for Close price must be greater than 1 for ConnorsRsi.");
		}
		if (streakPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("streakPeriods", streakPeriods, "RSI period for Streak must be greater than 1 for ConnorsRsi.");
		}
		if (rankPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("rankPeriods", rankPeriods, "Percent Rank periods must be greater than 1 for ConnorsRsi.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ConnorsRsiResult> RemoveWarmupPeriods(this IEnumerable<ConnorsRsiResult> results)
	{
		int removePeriods = results.ToList().FindIndex((ConnorsRsiResult x) => x.ConnorsRsi.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Correlation Coefficient between two quote histories, based on price.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Correlation/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotesA">Historical price quotes A for comparison.</param><param name="quotesB">Historical price quotes B for comparison.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>
	///     Time series of Correlation Coefficient values.
	///     R², Variance, and Covariance are also included.
	///   </returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Invalid quotes provided.</exception>
	public static IEnumerable<CorrResult> GetCorrelation<TQuote>(this IEnumerable<TQuote> quotesA, IEnumerable<TQuote> quotesB, int lookbackPeriods) where TQuote : IQuote
	{
		List<(DateTime, double)> tpListA = quotesA.ToTuple(CandlePart.Close);
		List<(DateTime, double)> tpListB = quotesB.ToTuple(CandlePart.Close);
		return tpListA.CalcCorrelation(tpListB, lookbackPeriods);
	}

	public static IEnumerable<CorrResult> GetCorrelation(this IEnumerable<IReusableResult> quotesA, IEnumerable<IReusableResult> quotesB, int lookbackPeriods)
	{
		List<(DateTime Date, double Value)> tpListA = quotesA.ToTuple();
		List<(DateTime, double)> tpListB = quotesB.ToTuple();
		return tpListA.CalcCorrelation(tpListB, lookbackPeriods).SyncIndex(quotesA, SyncType.Prepend);
	}

	public static IEnumerable<CorrResult> GetCorrelation(this IEnumerable<(DateTime, double)> tuplesA, IEnumerable<(DateTime, double)> tuplesB, int lookbackPeriods)
	{
		List<(DateTime, double)> tpListA = tuplesA.ToSortedList();
		List<(DateTime, double)> tpListB = tuplesB.ToSortedList();
		return tpListA.CalcCorrelation(tpListB, lookbackPeriods);
	}

	internal static List<CorrResult> CalcCorrelation(this List<(DateTime, double)> tpListA, List<(DateTime, double)> tpListB, int lookbackPeriods)
	{
		ValidateCorrelation(tpListA, tpListB, lookbackPeriods);
		int count = tpListA.Count;
		List<CorrResult> list = new List<CorrResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				DateTime item = tpListA[i].Item1;
				DateTime item2 = tpListB[i].Item1;
				if (item != item2)
				{
					throw new InvalidQuotesException("tpListA", item, "Date sequence does not match.  Correlation requires matching dates in provided histories.");
				}
				CorrResult corrResult = new CorrResult(item);
				list.Add(corrResult);
				if (i >= lookbackPeriods - 1)
				{
					double[] array = new double[lookbackPeriods];
					double[] array2 = new double[lookbackPeriods];
					int num = 0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						array[num] = tpListA[j].Item2;
						array2[num] = tpListB[j].Item2;
						num++;
					}
					corrResult.PeriodCorrelation(array, array2);
				}
			}
			return list;
		}
	}

	private static void PeriodCorrelation(this CorrResult r, double[] dataA, double[] dataB)
	{
		int num = dataA.Length;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		for (int i = 0; i < num; i = checked(i + 1))
		{
			double num7 = dataA[i];
			double num8 = dataB[i];
			num2 += num7;
			num3 += num8;
			num4 += num7 * num7;
			num5 += num8 * num8;
			num6 += num7 * num8;
		}
		double num9 = num2 / (double)num;
		double num10 = num3 / (double)num;
		double num11 = num4 / (double)num;
		double num12 = num5 / (double)num;
		double num13 = num6 / (double)num;
		double num14 = num11 - num9 * num9;
		double num15 = num12 - num10 * num10;
		double num16 = num13 - num9 * num10;
		double num17 = Math.Sqrt(num14 * num15);
		r.VarianceA = num14.NaN2Null();
		r.VarianceB = num15.NaN2Null();
		r.Covariance = num16.NaN2Null();
		r.Correlation = ((num17 == 0.0) ? ((double?)null) : (num16 / num17).NaN2Null());
		r.RSquared = r.Correlation * r.Correlation;
	}

	private static void ValidateCorrelation(List<(DateTime, double)> quotesA, List<(DateTime, double)> quotesB, int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Correlation.");
		}
		if (quotesA.Count != quotesB.Count)
		{
			throw new InvalidQuotesException("quotesB", "B quotes should have at least as many records as A quotes for Correlation.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<CorrResult> RemoveWarmupPeriods(this IEnumerable<CorrResult> results)
	{
		int removePeriods = results.ToList().FindIndex((CorrResult x) => x.Correlation.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Double Exponential Moving Average (DEMA) of the price.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Dema/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Double EMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<DemaResult> GetDema<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcDema(lookbackPeriods);
	}

	public static IEnumerable<DemaResult> GetDema(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcDema(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<DemaResult> GetDema(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcDema(lookbackPeriods);
	}

	internal static List<DemaResult> CalcDema(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateDema(lookbackPeriods);
		int count = tpList.Count;
		List<DemaResult> list = new List<DemaResult>(count);
		checked
		{
			double num = 2.0 / (double)(lookbackPeriods + 1);
			double? num2 = 0.0;
			int num3 = Math.Min(lookbackPeriods, count);
			for (int i = 0; i < num3; i++)
			{
				num2 += tpList[i].Item2;
			}
			num2 /= (double)lookbackPeriods;
			double? num4 = num2;
			for (int j = 0; j < count; j++)
			{
				(DateTime, double) tuple = tpList[j];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				DemaResult demaResult = new DemaResult(item);
				list.Add(demaResult);
				if (j > lookbackPeriods - 1)
				{
					double? num5 = num2 + num * (item2 - num2);
					double? num6 = num4 + num * (num5 - num4);
					demaResult.Dema = (2.0 * num5 - num6).NaN2Null();
					num2 = num5;
					num4 = num6;
				}
				else if (j == lookbackPeriods - 1)
				{
					demaResult.Dema = 2.0 * num2 - num4;
				}
			}
			return list;
		}
	}

	private static void ValidateDema(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for DEMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<DemaResult> RemoveWarmupPeriods(this IEnumerable<DemaResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((DemaResult x) => x.Dema.HasValue) + 1;
			return results.Remove(2 * num + 100);
		}
	}

	/// <summary>
	///     Doji is a single candlestick pattern where open and close price are virtually identical, representing market indecision.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Patterns/Doji/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="maxPriceChangePercent">Optional.  Maximum absolute percent difference in open and close price.</param><returns>Time series of Doji values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<CandleResult> GetDoji<TQuote>(this IEnumerable<TQuote> quotes, double maxPriceChangePercent = 0.1) where TQuote : IQuote
	{
		return quotes.CalcDoji(maxPriceChangePercent);
	}

	/// <summary>
	///     Doji is a single candlestick pattern where open and close price are virtually identical, representing market indecision.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Patterns/Doji/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="maxPriceChangePercent">Optional.  Maximum absolute percent difference in open and close price.</param><returns>Time series of Doji values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	internal static List<CandleResult> CalcDoji<TQuote>(this IEnumerable<TQuote> quotes, double maxPriceChangePercent) where TQuote : IQuote
	{
		ValidateDoji(maxPriceChangePercent);
		List<CandleResult> list = quotes.ToCandleResults();
		maxPriceChangePercent /= 100.0;
		int count = list.Count;
		for (int i = 0; i < count; i = checked(i + 1))
		{
			CandleResult candleResult = list[i];
			if (candleResult.Candle.Open != 0m && Math.Abs((double)(candleResult.Candle.Close / candleResult.Candle.Open) - 1.0) <= maxPriceChangePercent)
			{
				candleResult.Price = candleResult.Candle.Close;
				candleResult.Match = Match.Neutral;
			}
		}
		return list;
	}

	private static void ValidateDoji(double maxPriceChangePercent)
	{
		if ((maxPriceChangePercent < 0.0 || maxPriceChangePercent > 0.5) ? true : false)
		{
			throw new ArgumentOutOfRangeException("maxPriceChangePercent", maxPriceChangePercent, "Maximum Percent Change must be between 0 and 0.5 for Doji (0% to 0.5%).");
		}
	}

	/// <summary>
	///     Donchian Channels, also called Price Channels, are derived from highest High and lowest Low values over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Donchian/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Donchian Channel values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<DonchianResult> GetDonchian<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 20) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcDonchian(lookbackPeriods);
	}

	internal static List<DonchianResult> CalcDonchian<TQuote>(this List<TQuote> quotesList, int lookbackPeriods) where TQuote : IQuote
	{
		ValidateDonchian(lookbackPeriods);
		int count = quotesList.Count;
		List<DonchianResult> list = new List<DonchianResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				DonchianResult donchianResult = new DonchianResult(quotesList[i].Date);
				list.Add(donchianResult);
				if (i < lookbackPeriods)
				{
					continue;
				}
				decimal num = default(decimal);
				decimal num2 = decimal.MaxValue;
				for (int j = i - lookbackPeriods; j < i; j++)
				{
					TQuote val = quotesList[j];
					if (val.High > num)
					{
						num = val.High;
					}
					if (val.Low < num2)
					{
						num2 = val.Low;
					}
				}
				donchianResult.UpperBand = num;
				donchianResult.LowerBand = num2;
				donchianResult.Centerline = (donchianResult.UpperBand + donchianResult.LowerBand) / (decimal?)2m;
				decimal? centerline = donchianResult.Centerline;
				donchianResult.Width = (((centerline.GetValueOrDefault() == default(decimal)) & centerline.HasValue) ? ((decimal?)null) : ((donchianResult.UpperBand - donchianResult.LowerBand) / donchianResult.Centerline));
			}
			return list;
		}
	}

	private static void ValidateDonchian(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Donchian Channel.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<DonchianResult> Condense(this IEnumerable<DonchianResult> results)
	{
		List<DonchianResult> list = results.ToList();
		list.RemoveAll((DonchianResult x) => !x.UpperBand.HasValue && !x.LowerBand.HasValue && !x.Centerline.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<DonchianResult> RemoveWarmupPeriods(this IEnumerable<DonchianResult> results)
	{
		int removePeriods = results.ToList().FindIndex((DonchianResult x) => x.Width.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Detrended Price Oscillator (DPO) depicts the difference between price and an offset simple moving average.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Dpo/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of DPO values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<DpoResult> GetDpo<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcDpo(lookbackPeriods);
	}

	public static IEnumerable<DpoResult> GetDpo(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcDpo(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<DpoResult> GetDpo(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcDpo(lookbackPeriods);
	}

	internal static List<DpoResult> CalcDpo(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateDpo(lookbackPeriods);
		int count = tpList.Count;
		checked
		{
			int num = unchecked(lookbackPeriods / 2) + 1;
			List<SmaResult> list = tpList.GetSma(lookbackPeriods).ToList();
			List<DpoResult> list2 = new List<DpoResult>(count);
			for (int i = 0; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				DpoResult dpoResult = new DpoResult(item);
				list2.Add(dpoResult);
				if (i >= lookbackPeriods - num - 1 && i < count - num)
				{
					SmaResult smaResult = list[i + num];
					dpoResult.Sma = smaResult.Sma;
					dpoResult.Dpo = ((!smaResult.Sma.HasValue) ? ((double?)null) : (item2 - smaResult.Sma).NaN2Null());
				}
			}
			return list2;
		}
	}

	private static void ValidateDpo(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for DPO.");
		}
	}

	/// <summary>
	///     McGinley Dynamic is a more responsive variant of exponential moving average.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Dynamic/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="kFactor">Optional. Range adjustment factor.</param><returns>Time series of Dynamic values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<DynamicResult> GetDynamic<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, double kFactor = 0.6) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcDynamic(lookbackPeriods, kFactor);
	}

	public static IEnumerable<DynamicResult> GetDynamic(this IEnumerable<IReusableResult> results, int lookbackPeriods, double kFactor = 0.6)
	{
		return results.ToTuple().CalcDynamic(lookbackPeriods, kFactor).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<DynamicResult> GetDynamic(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, double kFactor = 0.6)
	{
		return priceTuples.ToSortedList().CalcDynamic(lookbackPeriods, kFactor);
	}

	internal static List<DynamicResult> CalcDynamic(this List<(DateTime, double)> tpList, int lookbackPeriods, double kFactor)
	{
		ValidateDynamic(lookbackPeriods, kFactor);
		int num = 1;
		int count = tpList.Count;
		List<DynamicResult> list = new List<DynamicResult>(count);
		if (count == 0)
		{
			return list;
		}
		double num2 = tpList[0].Item2;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				DynamicResult dynamicResult = new DynamicResult(item);
				list.Add(dynamicResult);
				if (double.IsNaN(item2) || num2 == 0.0)
				{
					num2 = item2;
					num = i + lookbackPeriods;
					continue;
				}
				double num3 = num2 + (item2 - num2) / (kFactor * (double)lookbackPeriods * Math.Pow(item2 / num2, 4.0));
				if (i >= num)
				{
					dynamicResult.Dynamic = num3.NaN2Null();
				}
				num2 = num3;
			}
			return list;
		}
	}

	private static void ValidateDynamic(int lookbackPeriods, double kFactor)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for DYNAMIC.");
		}
		if (kFactor <= 0.0)
		{
			throw new ArgumentOutOfRangeException("kFactor", kFactor, "K-Factor range adjustment must be greater than 0 for DYNAMIC.");
		}
	}

	/// <summary>
	///     The Elder-ray Index depicts buying and selling pressure, also known as Bull and Bear Power.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/ElderRay/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the EMA.</param><returns>Time series of Elder-ray Index values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ElderRayResult> GetElderRay<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 13) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcElderRay(lookbackPeriods);
	}

	internal static List<ElderRayResult> CalcElderRay(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateElderRay(lookbackPeriods);
		List<ElderRayResult> list = (from x in qdList.ToTuple(CandlePart.Close).CalcEma(lookbackPeriods)
			select new ElderRayResult(x.Date)
			{
				Ema = x.Ema
			}).ToList();
		checked
		{
			for (int num = lookbackPeriods - 1; num < qdList.Count; num++)
			{
				QuoteD quoteD = qdList[num];
				ElderRayResult elderRayResult = list[num];
				elderRayResult.BullPower = quoteD.High - elderRayResult.Ema;
				elderRayResult.BearPower = quoteD.Low - elderRayResult.Ema;
			}
			return list;
		}
	}

	private static void ValidateElderRay(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Elder-ray Index.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ElderRayResult> RemoveWarmupPeriods(this IEnumerable<ElderRayResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((ElderRayResult x) => x.BullPower.HasValue) + 1;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///       Exponential Moving Average (EMA) of price or any other specified OHLCV element.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Ema/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of EMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<EmaResult> GetEma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcEma(lookbackPeriods);
	}

	public static IEnumerable<EmaResult> GetEma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcEma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<EmaResult> GetEma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcEma(lookbackPeriods);
	}

	/// <summary>
	///       Extablish a streaming base for Exponential Moving Average (EMA).
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Ema/#streaming?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>EMA base that you can add Quotes to with the .Add(quote) method.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	internal static EmaBase InitEma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return new EmaBase(quotes.ToTuple(CandlePart.Close), lookbackPeriods);
	}

	internal static EmaBase InitEma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return new EmaBase(results.ToTuple(), lookbackPeriods);
	}

	internal static List<EmaResult> CalcEma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		EmaBase.Validate(lookbackPeriods);
		int count = tpList.Count;
		List<EmaResult> list = new List<EmaResult>(count);
		double num = 0.0;
		checked
		{
			double k = 2.0 / (double)(lookbackPeriods + 1);
			int num2 = Math.Min(lookbackPeriods, count);
			for (int i = 0; i < num2; i++)
			{
				double item = tpList[i].Item2;
				num += item;
			}
			num /= (double)lookbackPeriods;
			for (int j = 0; j < count; j++)
			{
				(DateTime, double) tuple = tpList[j];
				DateTime item2 = tuple.Item1;
				double item3 = tuple.Item2;
				EmaResult emaResult = new EmaResult(item2);
				list.Add(emaResult);
				if (j + 1 > lookbackPeriods)
				{
					double num3 = EmaBase.Increment(item3, num, k);
					emaResult.Ema = num3.NaN2Null();
					num = num3;
				}
				else if (j == lookbackPeriods - 1)
				{
					emaResult.Ema = num.NaN2Null();
				}
			}
			return list;
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<EmaResult> RemoveWarmupPeriods(this IEnumerable<EmaResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((EmaResult x) => x.Ema.HasValue) + 1;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///     Endpoint Moving Average (EPMA), also known as Least Squares Moving Average (LSMA), plots the projected last point of a linear regression lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Slope/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Endpoint Moving Average values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<EpmaResult> GetEpma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcEpma(lookbackPeriods);
	}

	public static IEnumerable<EpmaResult> GetEpma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcEpma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<EpmaResult> GetEpma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcEpma(lookbackPeriods);
	}

	internal static List<EpmaResult> CalcEpma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateEpma(lookbackPeriods);
		List<SlopeResult> list = tpList.CalcSlope(lookbackPeriods).ToList();
		int count = list.Count;
		List<EpmaResult> list2 = new List<EpmaResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				SlopeResult slopeResult = list[i];
				EpmaResult item = new EpmaResult(slopeResult.Date)
				{
					Epma = (slopeResult.Slope * (double)(i + 1) + slopeResult.Intercept).NaN2Null()
				};
				list2.Add(item);
			}
			return list2;
		}
	}

	private static void ValidateEpma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Epma.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<EpmaResult> RemoveWarmupPeriods(this IEnumerable<EpmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((EpmaResult x) => x.Epma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Fractal Chaos Bands outline high and low price channels to depict broad less-chaotic price movements. FCB is a channelized depiction of Williams Fractals.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Fcb/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="windowSpan">Number of span periods in the evaluation window.</param><returns>Time series of Fractal Chaos Band and Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<FcbResult> GetFcb<TQuote>(this IEnumerable<TQuote> quotes, int windowSpan = 2) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcFcb(windowSpan);
	}

	internal static List<FcbResult> CalcFcb<TQuote>(this List<TQuote> quotesList, int windowSpan) where TQuote : IQuote
	{
		ValidateFcb(windowSpan);
		List<FractalResult> list = quotesList.CalcFractal(windowSpan, windowSpan, EndType.HighLow).ToList();
		int count = list.Count;
		List<FcbResult> list2 = new List<FcbResult>(count);
		decimal? num = null;
		decimal? num2 = null;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				FcbResult fcbResult = new FcbResult(list[i].Date);
				list2.Add(fcbResult);
				if (i >= 2 * windowSpan)
				{
					FractalResult fractalResult = list[i - windowSpan];
					num = fractalResult.FractalBear ?? num;
					num2 = fractalResult.FractalBull ?? num2;
					fcbResult.UpperBand = num;
					fcbResult.LowerBand = num2;
				}
			}
			return list2;
		}
	}

	private static void ValidateFcb(int windowSpan)
	{
		if (windowSpan < 2)
		{
			throw new ArgumentOutOfRangeException("windowSpan", windowSpan, "Window span must be at least 2 for FCB.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<FcbResult> Condense(this IEnumerable<FcbResult> results)
	{
		List<FcbResult> list = results.ToList();
		list.RemoveAll((FcbResult x) => !x.UpperBand.HasValue && !x.LowerBand.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<FcbResult> RemoveWarmupPeriods(this IEnumerable<FcbResult> results)
	{
		int removePeriods = results.ToList().FindIndex((FcbResult x) => x.UpperBand.HasValue || x.LowerBand.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Ehlers Fisher Transform converts prices into a Gaussian normal distribution.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/FisherTransform/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Fisher Transform values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<FisherTransformResult> GetFisherTransform<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 10) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).CalcFisherTransform(lookbackPeriods);
	}

	public static IEnumerable<FisherTransformResult> GetFisherTransform(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcFisherTransform(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<FisherTransformResult> GetFisherTransform(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcFisherTransform(lookbackPeriods);
	}

	internal static List<FisherTransformResult> CalcFisherTransform(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateFisherTransform(lookbackPeriods);
		int count = tpList.Count;
		double[] array = new double[count];
		double[] array2 = new double[count];
		List<FisherTransformResult> list = new List<FisherTransformResult>(count);
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				var (date, num) = tpList[i];
				array[i] = num;
				double num2 = array[i];
				double num3 = array[i];
				for (int j = Math.Max(i - lookbackPeriods + 1, 0); j <= i; j++)
				{
					num2 = Math.Min(array[j], num2);
					num3 = Math.Max(array[j], num3);
				}
				FisherTransformResult fisherTransformResult = new FisherTransformResult(date);
				list.Add(fisherTransformResult);
				if (i > 0)
				{
					array2[i] = ((num3 != num2) ? (0.66 * ((array[i] - num2) / (num3 - num2) - 0.5) + 0.67 * array2[i - 1]) : 0.0);
					array2[i] = ((array2[i] > 0.99) ? 0.999 : array2[i]);
					array2[i] = ((array2[i] < -0.99) ? (-0.999) : array2[i]);
					fisherTransformResult.Fisher = (0.5 * Math.Log((1.0 + array2[i]) / (1.0 - array2[i])) + 0.5 * list[i - 1].Fisher).NaN2Null();
					fisherTransformResult.Trigger = list[i - 1].Fisher;
				}
				else
				{
					array2[i] = 0.0;
					fisherTransformResult.Fisher = 0.0;
				}
			}
			return list;
		}
	}

	private static void ValidateFisherTransform(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Fisher Transform.");
		}
	}

	/// <summary>
	///     The Force Index depicts volume-based buying and selling pressure.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/ForceIndex/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the EMA of Force Index.</param><returns>Time series of Force Index values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ForceIndexResult> GetForceIndex<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 2) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcForceIndex(lookbackPeriods);
	}

	internal static List<ForceIndexResult> CalcForceIndex(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateForceIndex(lookbackPeriods);
		int count = qdList.Count;
		List<ForceIndexResult> list = new List<ForceIndexResult>(count);
		double? num = null;
		double? num2 = null;
		double? num3 = 0.0;
		checked
		{
			double num4 = 2.0 / (double)(lookbackPeriods + 1);
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				ForceIndexResult forceIndexResult = new ForceIndexResult(quoteD.Date);
				list.Add(forceIndexResult);
				if (i == 0)
				{
					num = quoteD.Close;
					continue;
				}
				double? num5 = quoteD.Volume * (quoteD.Close - num);
				num = quoteD.Close;
				if (i > lookbackPeriods)
				{
					forceIndexResult.ForceIndex = num2 + num4 * (num5 - num2);
				}
				else
				{
					num3 += num5;
					if (i == lookbackPeriods)
					{
						forceIndexResult.ForceIndex = num3 / (double)lookbackPeriods;
					}
				}
				num2 = forceIndexResult.ForceIndex;
			}
			return list;
		}
	}

	private static void ValidateForceIndex(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Force Index.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ForceIndexResult> RemoveWarmupPeriods(this IEnumerable<ForceIndexResult> results)
	{
		int num = results.ToList().FindIndex((ForceIndexResult x) => x.ForceIndex.HasValue);
		return results.Remove(checked(num + 100));
	}

	/// <summary>
	///       Williams Fractal is a retrospective price pattern that identifies a central high or low point over a lookback window.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Fractal/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="windowSpan">Number of span periods to the left and right of the evaluation period.</param><param name="endType">Determines use of Close or High/Low wicks for points.</param><returns>Time series of Williams Fractal Bull/Bear values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<FractalResult> GetFractal<TQuote>(this IEnumerable<TQuote> quotes, int windowSpan = 2, EndType endType = EndType.HighLow) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcFractal(windowSpan, windowSpan, endType);
	}

	/// <summary>
	///       Williams Fractal is a retrospective price pattern that identifies a central high or low point over a lookback window.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Fractal/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="leftSpan">Number of span periods to the left of the evaluation period.</param><param name="rightSpan">Number of span periods to the right of the evaluation period.</param><param name="endType">Determines use of Close or High/Low wicks for points.</param><returns>Time series of Williams Fractal Bull/Bear values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<FractalResult> GetFractal<TQuote>(this IEnumerable<TQuote> quotes, int leftSpan, int rightSpan, EndType endType = EndType.HighLow) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcFractal(leftSpan, rightSpan, endType);
	}

	internal static List<FractalResult> CalcFractal<TQuote>(this List<TQuote> quotesList, int leftSpan, int rightSpan, EndType endType) where TQuote : IQuote
	{
		ValidateFractal(Math.Min(leftSpan, rightSpan));
		List<FractalResult> list = new List<FractalResult>(quotesList.Count);
		checked
		{
			for (int i = 0; i < quotesList.Count; i++)
			{
				TQuote val = quotesList[i];
				FractalResult fractalResult = new FractalResult(val.Date);
				list.Add(fractalResult);
				if (i + 1 <= leftSpan || i + 1 > quotesList.Count - rightSpan)
				{
					continue;
				}
				bool flag = true;
				bool flag2 = true;
				decimal num = ((endType == EndType.Close) ? val.Close : val.High);
				decimal num2 = ((endType == EndType.Close) ? val.Close : val.Low);
				for (int j = i - leftSpan; j <= i + rightSpan; j++)
				{
					if (j != i)
					{
						TQuote val2 = quotesList[j];
						decimal num3 = ((endType == EndType.Close) ? val2.Close : val2.High);
						decimal num4 = ((endType == EndType.Close) ? val2.Close : val2.Low);
						if (num <= num3)
						{
							flag = false;
						}
						if (num2 >= num4)
						{
							flag2 = false;
						}
					}
				}
				if (flag)
				{
					fractalResult.FractalBear = num;
				}
				if (flag2)
				{
					fractalResult.FractalBull = num2;
				}
			}
			return list;
		}
	}

	private static void ValidateFractal(int windowSpan)
	{
		if (windowSpan < 2)
		{
			throw new ArgumentOutOfRangeException("windowSpan", windowSpan, "Window span must be at least 2 for Fractal.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<FractalResult> Condense(this IEnumerable<FractalResult> results)
	{
		List<FractalResult> list = results.ToList();
		list.RemoveAll((FractalResult x) => !x.FractalBull.HasValue && !x.FractalBear.HasValue);
		return list.ToSortedList();
	}

	/// <summary>
	///     Gator Oscillator is an expanded view of Williams Alligator.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Gator/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><returns>Time series of Gator values.</returns>
	public static IEnumerable<GatorResult> GetGator<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).GetAlligator().ToList()
			.CalcGator();
	}

	public static IEnumerable<GatorResult> GetGator(this IEnumerable<AlligatorResult> alligator)
	{
		return alligator.ToList().CalcGator();
	}

	public static IEnumerable<GatorResult> GetGator(this IEnumerable<IReusableResult> results)
	{
		return results.ToTuple().GetAlligator().ToList()
			.CalcGator()
			.SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<GatorResult> GetGator(this IEnumerable<(DateTime, double)> priceTuples)
	{
		return priceTuples.ToSortedList().GetAlligator().ToList()
			.CalcGator();
	}

	internal static List<GatorResult> CalcGator(this List<AlligatorResult> alligator)
	{
		List<GatorResult> list = alligator.Select((AlligatorResult x) => new GatorResult(x.Date)
		{
			Upper = (x.Jaw - x.Teeth).Abs(),
			Lower = 0.0 - (x.Teeth - x.Lips).Abs()
		}).ToList();
		checked
		{
			for (int num = 1; num < list.Count; num++)
			{
				GatorResult gatorResult = list[num];
				GatorResult gatorResult2 = list[num - 1];
				gatorResult.UpperIsExpanding = (gatorResult2.Upper.HasValue ? new bool?(gatorResult.Upper > gatorResult2.Upper) : ((bool?)null));
				gatorResult.LowerIsExpanding = (gatorResult2.Lower.HasValue ? new bool?(gatorResult.Lower < gatorResult2.Lower) : ((bool?)null));
			}
			return list;
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<GatorResult> Condense(this IEnumerable<GatorResult> results)
	{
		List<GatorResult> list = results.ToList();
		list.RemoveAll((GatorResult x) => !x.Upper.HasValue && !x.Lower.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<GatorResult> RemoveWarmupPeriods(this IEnumerable<GatorResult> results)
	{
		return results.Remove(150);
	}

	/// <summary>
	///     Heikin-Ashi is a modified candlestick pattern that uses prior day for smoothing.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/HeikinAshi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><returns>Time series of Heikin-Ashi candlestick values.</returns>
	public static IEnumerable<HeikinAshiResult> GetHeikinAshi<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcHeikinAshi();
	}

	internal static List<HeikinAshiResult> CalcHeikinAshi<TQuote>(this List<TQuote> quotesList) where TQuote : IQuote
	{
		int count = quotesList.Count;
		List<HeikinAshiResult> list = new List<HeikinAshiResult>(count);
		decimal num = decimal.MinValue;
		decimal num2 = decimal.MinValue;
		if (count > 0)
		{
			TQuote val = quotesList[0];
			num = val.Open;
			num2 = val.Close;
		}
		for (int i = 0; i < count; i = checked(i + 1))
		{
			TQuote val2 = quotesList[i];
			decimal num3 = (val2.Open + val2.High + val2.Low + val2.Close) / 4m;
			decimal num4 = (num + num2) / 2m;
			decimal high = new decimal[3] { val2.High, num4, num3 }.Max();
			decimal low = new decimal[3] { val2.Low, num4, num3 }.Min();
			HeikinAshiResult item = new HeikinAshiResult(val2.Date)
			{
				Open = num4,
				High = high,
				Low = low,
				Close = num3,
				Volume = val2.Volume
			};
			list.Add(item);
			num = num4;
			num2 = num3;
		}
		return list;
	}

	public static IEnumerable<Quote> ToQuotes(this IEnumerable<HeikinAshiResult> results)
	{
		return (from x in results
			select new Quote
			{
				Date = x.Date,
				Open = x.Open,
				High = x.High,
				Low = x.Low,
				Close = x.Close,
				Volume = x.Volume
			} into x
			orderby x.Date
			select x).ToList();
	}

	/// <summary>
	///     Hull Moving Average (HMA) is a modified weighted average of price over N lookback periods that reduces lag.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Hma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of HMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<HmaResult> GetHma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcHma(lookbackPeriods);
	}

	public static IEnumerable<HmaResult> GetHma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcHma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<HmaResult> GetHma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcHma(lookbackPeriods);
	}

	internal static List<HmaResult> CalcHma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateHma(lookbackPeriods);
		int num = checked(lookbackPeriods - 1);
		List<(DateTime, double)> list = new List<(DateTime, double)>();
		List<WmaResult> list2 = tpList.GetWma(lookbackPeriods).ToList();
		List<WmaResult> list3 = tpList.GetWma(lookbackPeriods / 2).ToList();
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				DateTime item = tpList[i].Item1;
				WmaResult wmaResult = list2[i];
				WmaResult wmaResult2 = list3[i];
				if (i >= num)
				{
					(DateTime, double) item2 = (item, wmaResult2.Wma.Null2NaN() * 2.0 - wmaResult.Wma.Null2NaN());
					list.Add(item2);
				}
			}
			int lookbackPeriods2 = (int)Math.Sqrt(lookbackPeriods);
			List<HmaResult> list4 = (from x in tpList.Take(num)
				select new HmaResult(x.Item1)).ToList();
			List<HmaResult> collection = (from x in list.CalcWma(lookbackPeriods2)
				select new HmaResult(x.Date)
				{
					Hma = x.Wma
				}).ToList();
			list4.AddRange(collection);
			return list4.ToSortedList();
		}
	}

	private static void ValidateHma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for HMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<HmaResult> RemoveWarmupPeriods(this IEnumerable<HmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((HmaResult x) => x.Hma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Hilbert Transform Instantaneous Trendline (HTL) is a 5-period trendline of high/low price that uses signal processing to reduce noise.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/HtTrendline/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><returns>Time series of HTL values and smoothed price.</returns>
	public static IEnumerable<HtlResult> GetHtTrendline<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).CalcHtTrendline();
	}

	public static IEnumerable<HtlResult> GetHtTrendline(this IEnumerable<IReusableResult> results)
	{
		return results.ToTuple().CalcHtTrendline().SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<HtlResult> GetHtTrendline(this IEnumerable<(DateTime, double)> priceTuples)
	{
		return priceTuples.ToSortedList().CalcHtTrendline();
	}

	internal static List<HtlResult> CalcHtTrendline(this List<(DateTime, double)> tpList)
	{
		int count = tpList.Count;
		List<HtlResult> list = new List<HtlResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		double[] array3 = new double[count];
		double[] array4 = new double[count];
		double[] array5 = new double[count];
		double[] array6 = new double[count];
		double[] array7 = new double[count];
		double[] array8 = new double[count];
		double[] array9 = new double[count];
		double[] array10 = new double[count];
		double[] array11 = new double[count];
		double[] array12 = new double[count];
		checked
		{
			for (int i = 0; i < count; i++)
			{
				var (date, num) = tpList[i];
				array[i] = num;
				HtlResult htlResult = new HtlResult(date);
				list.Add(htlResult);
				if (i > 5)
				{
					double num2 = 0.075 * array4[i - 1] + 0.54;
					array2[i] = (4.0 * array[i] + 3.0 * array[i - 1] + 2.0 * array[i - 2] + array[i - 3]) / 10.0;
					array3[i] = (0.0962 * array2[i] + 0.5769 * array2[i - 2] - 0.5769 * array2[i - 4] - 0.0962 * array2[i - 6]) * num2;
					array5[i] = (0.0962 * array3[i] + 0.5769 * array3[i - 2] - 0.5769 * array3[i - 4] - 0.0962 * array3[i - 6]) * num2;
					array6[i] = array3[i - 3];
					double num3 = (0.0962 * array6[i] + 0.5769 * array6[i - 2] - 0.5769 * array6[i - 4] - 0.0962 * array6[i - 6]) * num2;
					double num4 = (0.0962 * array5[i] + 0.5769 * array5[i - 2] - 0.5769 * array5[i - 4] - 0.0962 * array5[i - 6]) * num2;
					array8[i] = array6[i] - num4;
					array7[i] = array5[i] + num3;
					array8[i] = 0.2 * array8[i] + 0.8 * array8[i - 1];
					array7[i] = 0.2 * array7[i] + 0.8 * array7[i - 1];
					array9[i] = array8[i] * array8[i - 1] + array7[i] * array7[i - 1];
					array10[i] = array8[i] * array7[i - 1] - array7[i] * array8[i - 1];
					array9[i] = 0.2 * array9[i] + 0.8 * array9[i - 1];
					array10[i] = 0.2 * array10[i] + 0.8 * array10[i - 1];
					array4[i] = ((array10[i] != 0.0 && array9[i] != 0.0) ? (Math.PI * 2.0 / Math.Atan(array10[i] / array9[i])) : 0.0);
					array4[i] = ((array4[i] > 1.5 * array4[i - 1]) ? (1.5 * array4[i - 1]) : array4[i]);
					array4[i] = ((array4[i] < 0.67 * array4[i - 1]) ? (0.67 * array4[i - 1]) : array4[i]);
					array4[i] = ((array4[i] < 6.0) ? 6.0 : array4[i]);
					array4[i] = ((array4[i] > 50.0) ? 50.0 : array4[i]);
					array4[i] = 0.2 * array4[i] + 0.8 * array4[i - 1];
					array11[i] = 0.33 * array4[i] + 0.67 * array11[i - 1];
					int num5 = (int)(double.IsNaN(array11[i]) ? 0.0 : (array11[i] + 0.5));
					double num6 = 0.0;
					for (int j = i - num5 + 1; j <= i; j++)
					{
						if (j >= 0)
						{
							num6 += array[j];
						}
						else
						{
							num5--;
						}
					}
					array12[i] = ((num5 > 0) ? (num6 / (double)num5) : array[i]);
					htlResult.DcPeriods = ((num5 > 0) ? new int?(num5) : ((int?)null));
					htlResult.Trendline = ((i >= 11) ? ((4.0 * array12[i] + 3.0 * array12[i - 1] + 2.0 * array12[i - 2] + array12[i - 3]) / 10.0).NaN2Null() : array[i].NaN2Null());
					htlResult.SmoothPrice = ((4.0 * array[i] + 3.0 * array[i - 1] + 2.0 * array[i - 2] + array[i - 3]) / 10.0).NaN2Null();
				}
				else
				{
					htlResult.Trendline = array[i].NaN2Null();
					htlResult.SmoothPrice = null;
					array4[i] = 0.0;
					array2[i] = 0.0;
					array3[i] = 0.0;
					array6[i] = 0.0;
					array5[i] = 0.0;
					array8[i] = 0.0;
					array7[i] = 0.0;
					array9[i] = 0.0;
					array10[i] = 0.0;
					array11[i] = 0.0;
				}
			}
			return list;
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<HtlResult> RemoveWarmupPeriods(this IEnumerable<HtlResult> results)
	{
		return results.Remove(100);
	}

	/// <summary>
	///     Hurst Exponent is a measure of randomness, trending, and mean-reverting tendencies of incremental return values.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Hurst/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of lookback periods.</param><returns>Time series of Hurst Exponent values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<HurstResult> GetHurst<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 100) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcHurst(lookbackPeriods);
	}

	public static IEnumerable<HurstResult> GetHurst(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcHurst(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<HurstResult> GetHurst(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcHurst(lookbackPeriods);
	}

	internal static List<HurstResult> CalcHurst(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateHurst(lookbackPeriods);
		int count = tpList.Count;
		List<HurstResult> list = new List<HurstResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				HurstResult hurstResult = new HurstResult(tpList[i].Item1);
				list.Add(hurstResult);
				if (i + 1 > lookbackPeriods)
				{
					double[] array = new double[lookbackPeriods];
					int num = 0;
					double num2 = tpList[i - lookbackPeriods].Item2;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						double item = tpList[j].Item2;
						array[num] = ((num2 != 0.0) ? (item / num2 - 1.0) : double.NaN);
						num2 = item;
						num++;
					}
					hurstResult.HurstExponent = CalcHurstWindow(array).NaN2Null();
				}
			}
			return list;
		}
	}

	private static double CalcHurstWindow(double[] values)
	{
		int num = values.Length;
		int num2 = 0;
		int num3 = 0;
		int num4 = 1;
		checked
		{
			while (num4 <= 32 && unchecked(num / num4) >= 8)
			{
				num2 = num4;
				num3++;
				num4 *= 2;
			}
			double[] array = new double[num3];
			double[] array2 = new double[num3];
			int num5 = 0;
			for (int num6 = 1; num6 <= num2; num6 *= 2)
			{
				int num7 = unchecked(num / num6);
				double num8 = 0.0;
				int num9 = num - num7 * num6;
				for (int i = 1; i <= num6; i++)
				{
					double num10 = 0.0;
					for (int j = num9; j < num9 + num7; j++)
					{
						num10 += values[j];
					}
					double num11 = num10 / (double)num7;
					double num12 = 0.0;
					double num13 = 0.0;
					double num14 = values[num9] - num11;
					double num15 = values[num9] - num11;
					for (int k = num9; k < num9 + num7; k++)
					{
						double num16 = values[k] - num11;
						num12 += num16;
						num15 = ((num12 < num15) ? num12 : num15);
						num14 = ((num12 > num14) ? num12 : num14);
						num13 += num16 * num16;
					}
					double num17 = num14 - num15;
					double num18 = Math.Sqrt(num13 / (double)num7);
					double num19 = ((num18 != 0.0) ? (num17 / num18) : 0.0);
					num8 += num19;
					num9 += num7;
				}
				array2[num5] = Math.Log10(num7);
				array[num5] = Math.Log10(num8 / (double)num6);
				num5++;
			}
			return Numerix.Slope(array2, array);
		}
	}

	private static void ValidateHurst(int lookbackPeriods)
	{
		if (lookbackPeriods < 20)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be at least 20 for Hurst Exponent.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<HurstResult> RemoveWarmupPeriods(this IEnumerable<HurstResult> results)
	{
		int removePeriods = results.ToList().FindIndex((HurstResult x) => x.HurstExponent.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///       Ichimoku Cloud, also known as Ichimoku Kinkō Hyō, is a collection of indicators that depict support and resistance, momentum, and trend direction.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Ichimoku/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="tenkanPeriods">Number of periods in the Tenkan-sen midpoint evaluation.</param><param name="kijunPeriods">Number of periods in the shorter Kijun-sen midpoint evaluation.  This value is also used to offset Senkou and Chinkou spans.</param><param name="senkouBPeriods">Number of periods in the longer Senkou leading span B midpoint evaluation.</param><returns>Time series of Ichimoku Cloud values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<IchimokuResult> GetIchimoku<TQuote>(this IEnumerable<TQuote> quotes, int tenkanPeriods = 9, int kijunPeriods = 26, int senkouBPeriods = 52) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcIchimoku(tenkanPeriods, kijunPeriods, senkouBPeriods, kijunPeriods, kijunPeriods);
	}

	/// <summary>
	///       Ichimoku Cloud, also known as Ichimoku Kinkō Hyō, is a collection of indicators that depict support and resistance, momentum, and trend direction.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Ichimoku/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="tenkanPeriods">Number of periods in the Tenkan-sen midpoint evaluation.</param><param name="kijunPeriods">Number of periods in the shorter Kijun-sen midpoint evaluation.</param><param name="senkouBPeriods">Number of periods in the longer Senkou leading span B midpoint evaluation.</param><param name="offsetPeriods">Number of periods to displace the Senkou and Chikou Spans.</param><returns>Time series of Ichimoku Cloud values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<IchimokuResult> GetIchimoku<TQuote>(this IEnumerable<TQuote> quotes, int tenkanPeriods, int kijunPeriods, int senkouBPeriods, int offsetPeriods) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcIchimoku(tenkanPeriods, kijunPeriods, senkouBPeriods, offsetPeriods, offsetPeriods);
	}

	/// <summary>
	///       Ichimoku Cloud, also known as Ichimoku Kinkō Hyō, is a collection of indicators that depict support and resistance, momentum, and trend direction.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Ichimoku/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="tenkanPeriods">Number of periods in the Tenkan-sen midpoint evaluation.</param><param name="kijunPeriods">Number of periods in the shorter Kijun-sen midpoint evaluation.</param><param name="senkouBPeriods">Number of periods in the longer Senkou leading span B midpoint evaluation.</param><param name="senkouOffset">Number of periods to displace the Senkou Spans.</param><param name="chikouOffset">Number of periods in displace the Chikou Span.</param><returns>Time series of Ichimoku Cloud values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<IchimokuResult> GetIchimoku<TQuote>(this IEnumerable<TQuote> quotes, int tenkanPeriods, int kijunPeriods, int senkouBPeriods, int senkouOffset, int chikouOffset) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcIchimoku(tenkanPeriods, kijunPeriods, senkouBPeriods, senkouOffset, chikouOffset);
	}

	internal static List<IchimokuResult> CalcIchimoku<TQuote>(this List<TQuote> quotesList, int tenkanPeriods, int kijunPeriods, int senkouBPeriods, int senkouOffset, int chikouOffset) where TQuote : IQuote
	{
		ValidateIchimoku(tenkanPeriods, kijunPeriods, senkouBPeriods, senkouOffset, chikouOffset);
		int count = quotesList.Count;
		List<IchimokuResult> list = new List<IchimokuResult>(count);
		checked
		{
			int num = Math.Max(2 * senkouOffset, Math.Max(tenkanPeriods, kijunPeriods)) - 1;
			for (int i = 0; i < count; i++)
			{
				IchimokuResult ichimokuResult = new IchimokuResult(quotesList[i].Date);
				list.Add(ichimokuResult);
				CalcIchimokuTenkanSen(i, quotesList, ichimokuResult, tenkanPeriods);
				CalcIchimokuKijunSen(i, quotesList, ichimokuResult, kijunPeriods);
				if (i >= num)
				{
					IchimokuResult ichimokuResult2 = list[i - senkouOffset];
					if (ichimokuResult2 != null && ichimokuResult2.TenkanSen.HasValue && ichimokuResult2.KijunSen.HasValue)
					{
						ichimokuResult.SenkouSpanA = (ichimokuResult2.TenkanSen + ichimokuResult2.KijunSen) / (decimal?)2;
					}
				}
				CalcIchimokuSenkouB(i, quotesList, ichimokuResult, senkouOffset, senkouBPeriods);
				if (i + chikouOffset < quotesList.Count)
				{
					ichimokuResult.ChikouSpan = quotesList[i + chikouOffset].Close;
				}
			}
			return list;
		}
	}

	private static void CalcIchimokuTenkanSen<TQuote>(int i, List<TQuote> quotesList, IchimokuResult result, int tenkanPeriods) where TQuote : IQuote
	{
		checked
		{
			if (i < tenkanPeriods - 1)
			{
				return;
			}
			decimal num = default(decimal);
			decimal num2 = decimal.MaxValue;
			for (int j = i - tenkanPeriods + 1; j <= i; j++)
			{
				TQuote val = quotesList[j];
				if (val.High > num)
				{
					num = val.High;
				}
				if (val.Low < num2)
				{
					num2 = val.Low;
				}
			}
			result.TenkanSen = ((num2 == decimal.MaxValue) ? ((decimal?)null) : new decimal?((num2 + num) / 2m));
		}
	}

	private static void CalcIchimokuKijunSen<TQuote>(int i, List<TQuote> quotesList, IchimokuResult result, int kijunPeriods) where TQuote : IQuote
	{
		checked
		{
			if (i < kijunPeriods - 1)
			{
				return;
			}
			decimal num = default(decimal);
			decimal num2 = decimal.MaxValue;
			for (int j = i - kijunPeriods + 1; j <= i; j++)
			{
				TQuote val = quotesList[j];
				if (val.High > num)
				{
					num = val.High;
				}
				if (val.Low < num2)
				{
					num2 = val.Low;
				}
			}
			result.KijunSen = ((num2 == decimal.MaxValue) ? ((decimal?)null) : new decimal?((num2 + num) / 2m));
		}
	}

	private static void CalcIchimokuSenkouB<TQuote>(int i, List<TQuote> quotesList, IchimokuResult result, int senkouOffset, int senkouBPeriods) where TQuote : IQuote
	{
		checked
		{
			if (i < senkouOffset + senkouBPeriods - 1)
			{
				return;
			}
			decimal num = default(decimal);
			decimal num2 = decimal.MaxValue;
			for (int j = i - senkouOffset - senkouBPeriods + 1; j <= i - senkouOffset; j++)
			{
				TQuote val = quotesList[j];
				if (val.High > num)
				{
					num = val.High;
				}
				if (val.Low < num2)
				{
					num2 = val.Low;
				}
			}
			result.SenkouSpanB = ((num2 == decimal.MaxValue) ? ((decimal?)null) : new decimal?((num2 + num) / 2m));
		}
	}

	private static void ValidateIchimoku(int tenkanPeriods, int kijunPeriods, int senkouBPeriods, int senkouOffset, int chikouOffset)
	{
		if (tenkanPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("tenkanPeriods", tenkanPeriods, "Tenkan periods must be greater than 0 for Ichimoku Cloud.");
		}
		if (kijunPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("kijunPeriods", kijunPeriods, "Kijun periods must be greater than 0 for Ichimoku Cloud.");
		}
		if (senkouBPeriods <= kijunPeriods)
		{
			throw new ArgumentOutOfRangeException("senkouBPeriods", senkouBPeriods, "Senkou B periods must be greater than Kijun periods for Ichimoku Cloud.");
		}
		if (senkouOffset < 0 || chikouOffset < 0)
		{
			throw new ArgumentOutOfRangeException("senkouOffset", senkouOffset, "Senkou and Chikou offset periods must be non-negative for Ichimoku Cloud.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<IchimokuResult> Condense(this IEnumerable<IchimokuResult> results)
	{
		List<IchimokuResult> list = results.ToList();
		list.RemoveAll((IchimokuResult x) => !x.TenkanSen.HasValue && !x.KijunSen.HasValue && !x.SenkouSpanA.HasValue && !x.SenkouSpanB.HasValue && !x.ChikouSpan.HasValue);
		return list.ToSortedList();
	}

	/// <summary>
	///     Kaufman’s Adaptive Moving Average (KAMA) is an volatility adaptive moving average of price over configurable lookback periods.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Kama/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="erPeriods">Number of Efficiency Ratio (volatility) periods.</param><param name="fastPeriods">Number of periods in the Fast EMA.</param><param name="slowPeriods">Number of periods in the Slow EMA.</param><returns>Time series of KAMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<KamaResult> GetKama<TQuote>(this IEnumerable<TQuote> quotes, int erPeriods = 10, int fastPeriods = 2, int slowPeriods = 30) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcKama(erPeriods, fastPeriods, slowPeriods);
	}

	public static IEnumerable<KamaResult> GetKama(this IEnumerable<IReusableResult> results, int erPeriods = 10, int fastPeriods = 2, int slowPeriods = 30)
	{
		return results.ToTuple().CalcKama(erPeriods, fastPeriods, slowPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<KamaResult> GetKama(this IEnumerable<(DateTime, double)> priceTuples, int erPeriods = 10, int fastPeriods = 2, int slowPeriods = 30)
	{
		return priceTuples.ToSortedList().CalcKama(erPeriods, fastPeriods, slowPeriods);
	}

	internal static List<KamaResult> CalcKama(this List<(DateTime, double)> tpList, int erPeriods, int fastPeriods, int slowPeriods)
	{
		ValidateKama(erPeriods, fastPeriods, slowPeriods);
		int count = tpList.Count;
		List<KamaResult> list = new List<KamaResult>(count);
		checked
		{
			double num = 2.0 / (double)(fastPeriods + 1);
			double num2 = 2.0 / (double)(slowPeriods + 1);
			for (int i = 0; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				KamaResult kamaResult = new KamaResult(item);
				list.Add(kamaResult);
				if (i + 1 > erPeriods)
				{
					double num3 = Math.Abs(item2 - tpList[i - erPeriods].Item2);
					double num4 = 0.0;
					for (int j = i - erPeriods + 1; j <= i; j++)
					{
						num4 += Math.Abs(tpList[j].Item2 - tpList[j - 1].Item2);
					}
					if (num4 != 0.0)
					{
						double num5 = num3 / num4;
						kamaResult.ER = num5.NaN2Null();
						double num6 = num5 * (num - num2) + num2;
						double? kama = list[i - 1].Kama;
						kamaResult.Kama = (kama + num6 * num6 * (item2 - kama)).NaN2Null();
					}
					else
					{
						kamaResult.ER = 0.0;
						kamaResult.Kama = item2.NaN2Null();
					}
				}
				else if (i + 1 == erPeriods)
				{
					kamaResult.Kama = item2.NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateKama(int erPeriods, int fastPeriods, int slowPeriods)
	{
		if (erPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("erPeriods", erPeriods, "Efficiency Ratio periods must be greater than 0 for KAMA.");
		}
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast EMA periods must be greater than 0 for KAMA.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow EMA periods must be greater than Fast EMA period for KAMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<KamaResult> RemoveWarmupPeriods(this IEnumerable<KamaResult> results)
	{
		int num = results.ToList().FindIndex((KamaResult x) => x.ER.HasValue);
		return results.Remove(checked(Math.Max(num + 100, 10 * num)));
	}

	/// <summary>
	///     Keltner Channels are based on an EMA centerline and ATR band widths. See also STARC Bands for an SMA centerline equivalent.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Keltner/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="emaPeriods">Number of periods for the centerline EMA.</param><param name="multiplier">ATR multiplier sets the width of the channel.</param><param name="atrPeriods">Number of periods in the ATR evaluation.</param><returns>Time series of Keltner Channel values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<KeltnerResult> GetKeltner<TQuote>(this IEnumerable<TQuote> quotes, int emaPeriods = 20, double multiplier = 2.0, int atrPeriods = 10) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcKeltner(emaPeriods, multiplier, atrPeriods);
	}

	internal static List<KeltnerResult> CalcKeltner(this List<QuoteD> qdList, int emaPeriods, double multiplier, int atrPeriods)
	{
		ValidateKeltner(emaPeriods, multiplier, atrPeriods);
		int count = qdList.Count;
		List<KeltnerResult> list = new List<KeltnerResult>(count);
		List<EmaResult> list2 = qdList.ToTuple(CandlePart.Close).CalcEma(emaPeriods).ToList();
		List<AtrResult> list3 = qdList.CalcAtr(atrPeriods).ToList();
		int num = Math.Max(emaPeriods, atrPeriods);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				KeltnerResult keltnerResult = new KeltnerResult(qdList[i].Date);
				list.Add(keltnerResult);
				if (i + 1 >= num)
				{
					EmaResult emaResult = list2[i];
					double? num2 = list3[i].Atr * multiplier;
					keltnerResult.UpperBand = emaResult.Ema + num2;
					keltnerResult.LowerBand = emaResult.Ema - num2;
					keltnerResult.Centerline = emaResult.Ema;
					keltnerResult.Width = ((keltnerResult.Centerline == 0.0) ? ((double?)null) : ((keltnerResult.UpperBand - keltnerResult.LowerBand) / keltnerResult.Centerline));
				}
			}
			return list;
		}
	}

	private static void ValidateKeltner(int emaPeriods, double multiplier, int atrPeriods)
	{
		if (emaPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("emaPeriods", emaPeriods, "EMA periods must be greater than 1 for Keltner Channel.");
		}
		if (atrPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("atrPeriods", atrPeriods, "ATR periods must be greater than 1 for Keltner Channel.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "Multiplier must be greater than 0 for Keltner Channel.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<KeltnerResult> Condense(this IEnumerable<KeltnerResult> results)
	{
		List<KeltnerResult> list = results.ToList();
		list.RemoveAll((KeltnerResult x) => !x.UpperBand.HasValue && !x.LowerBand.HasValue && !x.Centerline.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<KeltnerResult> RemoveWarmupPeriods(this IEnumerable<KeltnerResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((KeltnerResult x) => x.Width.HasValue) + 1;
			return results.Remove(Math.Max(2 * num, num + 100));
		}
	}

	/// <summary>
	///     Klinger Oscillator depicts volume-based divergence between short and long-term money flow.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Klinger/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastPeriods">Number of periods for the short EMA.</param><param name="slowPeriods">Number of periods for the long EMA.</param><param name="signalPeriods">Number of periods Signal line.</param><returns>Time series of Klinger Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<KvoResult> GetKvo<TQuote>(this IEnumerable<TQuote> quotes, int fastPeriods = 34, int slowPeriods = 55, int signalPeriods = 13) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcKvo(fastPeriods, slowPeriods, signalPeriods);
	}

	internal static List<KvoResult> CalcKvo(this List<QuoteD> qdList, int fastPeriods, int slowPeriods, int signalPeriods)
	{
		ValidateKlinger(fastPeriods, slowPeriods, signalPeriods);
		int count = qdList.Count;
		List<KvoResult> list = new List<KvoResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		double[] array3 = new double[count];
		double[] array4 = new double[count];
		double[] array5 = new double[count];
		double[] array6 = new double[count];
		double[] array7 = new double[count];
		checked
		{
			double num = 2.0 / (double)(fastPeriods + 1);
			double num2 = 2.0 / (double)(slowPeriods + 1);
			double num3 = 2.0 / (double)(signalPeriods + 1);
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				KvoResult kvoResult = new KvoResult(quoteD.Date);
				list.Add(kvoResult);
				array2[i] = quoteD.High + quoteD.Low + quoteD.Close;
				array3[i] = quoteD.High - quoteD.Low;
				if (i <= 0)
				{
					continue;
				}
				array[i] = ((array2[i] > array2[i - 1]) ? 1 : (-1));
				if (i <= 1)
				{
					array4[i] = 0.0;
					continue;
				}
				array4[i] = ((array[i] == array[i - 1]) ? (array4[i - 1] + array3[i]) : (array3[i - 1] + array3[i]));
				array5[i] = ((array3[i] == array4[i] || quoteD.Volume == 0.0) ? 0.0 : ((array3[i] == 0.0) ? (quoteD.Volume * 2.0 * array[i] * 100.0) : ((array4[i] != 0.0) ? (quoteD.Volume * Math.Abs(2.0 * (array3[i] / array4[i] - 1.0)) * array[i] * 100.0) : array5[i - 1])));
				if (i > fastPeriods + 1)
				{
					array6[i] = array5[i] * num + array6[i - 1] * (1.0 - num);
				}
				else if (i == fastPeriods + 1)
				{
					double num4 = 0.0;
					for (int j = 2; j <= i; j++)
					{
						num4 += array5[j];
					}
					array6[i] = num4 / (double)fastPeriods;
				}
				if (i > slowPeriods + 1)
				{
					array7[i] = array5[i] * num2 + array7[i - 1] * (1.0 - num2);
				}
				else if (i == slowPeriods + 1)
				{
					double num5 = 0.0;
					for (int k = 2; k <= i; k++)
					{
						num5 += array5[k];
					}
					array7[i] = num5 / (double)slowPeriods;
				}
				if (i < slowPeriods + 1)
				{
					continue;
				}
				kvoResult.Oscillator = array6[i] - array7[i];
				if (i > slowPeriods + signalPeriods)
				{
					kvoResult.Signal = kvoResult.Oscillator * num3 + list[i - 1].Signal * (1.0 - num3);
				}
				else if (i == slowPeriods + signalPeriods)
				{
					double? num6 = 0.0;
					for (int l = slowPeriods + 1; l <= i; l++)
					{
						num6 += list[l].Oscillator;
					}
					kvoResult.Signal = num6 / (double)signalPeriods;
				}
			}
			return list;
		}
	}

	private static void ValidateKlinger(int fastPeriods, int slowPeriods, int signalPeriods)
	{
		if (fastPeriods <= 2)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast (short) Periods must be greater than 2 for Klinger Oscillator.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow (long) Periods must be greater than Fast Periods for Klinger Oscillator.");
		}
		if (signalPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal Periods must be greater than 0 for Klinger Oscillator.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<KvoResult> RemoveWarmupPeriods(this IEnumerable<KvoResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((KvoResult x) => x.Oscillator.HasValue) - 1;
			return results.Remove(num + 150);
		}
	}

	internal static List<MacdResult> CalcMacd(this List<(DateTime, double)> tpList, int fastPeriods, int slowPeriods, int signalPeriods)
	{
		ValidateMacd(fastPeriods, slowPeriods, signalPeriods);
		List<EmaResult> list = tpList.CalcEma(fastPeriods);
		List<EmaResult> list2 = tpList.CalcEma(slowPeriods);
		int count = tpList.Count;
		List<(DateTime, double)> list3 = new List<(DateTime, double)>();
		List<MacdResult> list4 = new List<MacdResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				DateTime item = tpList[i].Item1;
				EmaResult emaResult = list[i];
				EmaResult emaResult2 = list2[i];
				MacdResult macdResult = new MacdResult(item)
				{
					FastEma = emaResult.Ema,
					SlowEma = emaResult2.Ema
				};
				list4.Add(macdResult);
				if (i >= slowPeriods - 1)
				{
					double num = (emaResult.Ema - emaResult2.Ema).Null2NaN();
					macdResult.Macd = num.NaN2Null();
					(DateTime, double) item2 = (item, num);
					list3.Add(item2);
				}
			}
			List<EmaResult> list5 = list3.CalcEma(signalPeriods);
			for (int j = slowPeriods - 1; j < count; j++)
			{
				MacdResult macdResult2 = list4[j];
				EmaResult emaResult3 = list5[j + 1 - slowPeriods];
				macdResult2.Signal = emaResult3.Ema.NaN2Null();
				macdResult2.Histogram = (macdResult2.Macd - macdResult2.Signal).NaN2Null();
			}
			return list4;
		}
	}

	private static void ValidateMacd(int fastPeriods, int slowPeriods, int signalPeriods)
	{
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast periods must be greater than 0 for MACD.");
		}
		if (signalPeriods < 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than or equal to 0 for MACD.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow periods must be greater than the fast period for MACD.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<MacdResult> RemoveWarmupPeriods(this IEnumerable<MacdResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((MacdResult x) => x.Signal.HasValue) + 2;
			return results.Remove(num + 250);
		}
	}

	/// <summary>
	///     Moving Average Convergence/Divergence (MACD) is a simple oscillator view of two converging/diverging exponential moving averages.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Macd/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastPeriods">Number of periods in the Fast EMA.</param><param name="slowPeriods">Number of periods in the Slow EMA.</param><param name="signalPeriods">Number of periods for the Signal moving average.</param><returns>Time series of MACD values, including MACD, Signal, and Histogram.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<MacdResult> GetMacd<TQuote>(this IEnumerable<TQuote> quotes, int fastPeriods = 12, int slowPeriods = 26, int signalPeriods = 9) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcMacd(fastPeriods, slowPeriods, signalPeriods);
	}

	public static IEnumerable<MacdResult> GetMacd(this IEnumerable<IReusableResult> results, int fastPeriods = 12, int slowPeriods = 26, int signalPeriods = 9)
	{
		return results.ToTuple().CalcMacd(fastPeriods, slowPeriods, signalPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<MacdResult> GetMacd(this IEnumerable<(DateTime, double)> priceTuples, int fastPeriods = 12, int slowPeriods = 26, int signalPeriods = 9)
	{
		return priceTuples.ToSortedList().CalcMacd(fastPeriods, slowPeriods, signalPeriods);
	}

	/// <summary>
	///     Moving Average Envelopes is a price band overlay that is offset from the moving average of price over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/MaEnvelopes/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="percentOffset">Percent offset for envelope width.</param><param name="movingAverageType">Moving average type (e.g. EMA, HMA, TEMA, etc.).</param><returns>Time series of MA Envelopes values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<MaEnvelopeResult> GetMaEnvelopes<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, double percentOffset = 2.5, MaType movingAverageType = MaType.SMA) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcMaEnvelopes(lookbackPeriods, percentOffset, movingAverageType);
	}

	public static IEnumerable<MaEnvelopeResult> GetMaEnvelopes(this IEnumerable<IReusableResult> results, int lookbackPeriods, double percentOffset = 2.5, MaType movingAverageType = MaType.SMA)
	{
		return results.ToTuple().CalcMaEnvelopes(lookbackPeriods, percentOffset, movingAverageType).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<MaEnvelopeResult> GetMaEnvelopes(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, double percentOffset = 2.5, MaType movingAverageType = MaType.SMA)
	{
		return priceTuples.ToSortedList().CalcMaEnvelopes(lookbackPeriods, percentOffset, movingAverageType);
	}

	internal static IEnumerable<MaEnvelopeResult> CalcMaEnvelopes(this List<(DateTime, double)> tpList, int lookbackPeriods, double percentOffset, MaType movingAverageType)
	{
		ValidateMaEnvelopes(percentOffset);
		double offsetRatio = percentOffset / 100.0;
		return movingAverageType switch
		{
			MaType.ALMA => tpList.MaEnvAlma(lookbackPeriods, offsetRatio), 
			MaType.DEMA => tpList.MaEnvDema(lookbackPeriods, offsetRatio), 
			MaType.EMA => tpList.MaEnvEma(lookbackPeriods, offsetRatio), 
			MaType.EPMA => tpList.MaEnvEpma(lookbackPeriods, offsetRatio), 
			MaType.HMA => tpList.MaEnvHma(lookbackPeriods, offsetRatio), 
			MaType.SMA => tpList.MaEnvSma(lookbackPeriods, offsetRatio), 
			MaType.SMMA => tpList.MaEnvSmma(lookbackPeriods, offsetRatio), 
			MaType.TEMA => tpList.MaEnvTema(lookbackPeriods, offsetRatio), 
			MaType.WMA => tpList.MaEnvWma(lookbackPeriods, offsetRatio), 
			_ => throw new ArgumentOutOfRangeException("movingAverageType", movingAverageType, string.Format(invCulture, "Moving Average Envelopes does not support {0}.", Enum.GetName(typeof(MaType), movingAverageType))), 
		};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvAlma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetAlma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Alma,
				UpperEnvelope = x.Alma + x.Alma * offsetRatio,
				LowerEnvelope = x.Alma - x.Alma * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvDema(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetDema(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Dema,
				UpperEnvelope = x.Dema + x.Dema * offsetRatio,
				LowerEnvelope = x.Dema - x.Dema * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvEma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetEma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Ema,
				UpperEnvelope = x.Ema + x.Ema * offsetRatio,
				LowerEnvelope = x.Ema - x.Ema * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvEpma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetEpma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Epma,
				UpperEnvelope = x.Epma + x.Epma * offsetRatio,
				LowerEnvelope = x.Epma - x.Epma * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvHma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetHma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Hma,
				UpperEnvelope = x.Hma + x.Hma * offsetRatio,
				LowerEnvelope = x.Hma - x.Hma * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvSma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetSma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Sma,
				UpperEnvelope = x.Sma + x.Sma * offsetRatio,
				LowerEnvelope = x.Sma - x.Sma * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvSmma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetSmma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Smma,
				UpperEnvelope = x.Smma + x.Smma * offsetRatio,
				LowerEnvelope = x.Smma - x.Smma * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvTema(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetTema(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Tema,
				UpperEnvelope = x.Tema + x.Tema * offsetRatio,
				LowerEnvelope = x.Tema - x.Tema * offsetRatio
			};
	}

	private static IEnumerable<MaEnvelopeResult> MaEnvWma(this List<(DateTime, double)> tpList, int lookbackPeriods, double offsetRatio)
	{
		return from x in tpList.GetWma(lookbackPeriods)
			select new MaEnvelopeResult(x.Date)
			{
				Centerline = x.Wma,
				UpperEnvelope = x.Wma + x.Wma * offsetRatio,
				LowerEnvelope = x.Wma - x.Wma * offsetRatio
			};
	}

	private static void ValidateMaEnvelopes(double percentOffset)
	{
		if (percentOffset <= 0.0)
		{
			throw new ArgumentOutOfRangeException("percentOffset", percentOffset, "Percent Offset must be greater than 0 for Moving Average Envelopes.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<MaEnvelopeResult> Condense(this IEnumerable<MaEnvelopeResult> results)
	{
		List<MaEnvelopeResult> list = results.ToList();
		list.RemoveAll((MaEnvelopeResult x) => !x.UpperEnvelope.HasValue && !x.LowerEnvelope.HasValue && !x.Centerline.HasValue);
		return list.ToSortedList();
	}

	/// <summary>
	///     MESA Adaptive Moving Average (MAMA) is a 5-period adaptive moving average of high/low price.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Mama/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastLimit">Fast limit threshold.</param><param name="slowLimit">Slow limit threshold.</param><returns>Time series of MAMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<MamaResult> GetMama<TQuote>(this IEnumerable<TQuote> quotes, double fastLimit = 0.5, double slowLimit = 0.05) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.HL2).CalcMama(fastLimit, slowLimit);
	}

	public static IEnumerable<MamaResult> GetMama(this IEnumerable<IReusableResult> results, double fastLimit = 0.5, double slowLimit = 0.05)
	{
		return results.ToTuple().CalcMama(fastLimit, slowLimit).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<MamaResult> GetMama(this IEnumerable<(DateTime, double)> priceTuples, double fastLimit = 0.5, double slowLimit = 0.05)
	{
		return priceTuples.ToSortedList().CalcMama(fastLimit, slowLimit);
	}

	internal static List<MamaResult> CalcMama(this List<(DateTime, double)> tpList, double fastLimit, double slowLimit)
	{
		ValidateMama(fastLimit, slowLimit);
		int count = tpList.Count;
		List<MamaResult> list = new List<MamaResult>(count);
		double num = 0.0;
		double[] array = new double[count];
		double[] array2 = new double[count];
		double[] array3 = new double[count];
		double[] array4 = new double[count];
		double[] array5 = new double[count];
		double[] array6 = new double[count];
		double[] array7 = new double[count];
		double[] array8 = new double[count];
		double[] array9 = new double[count];
		double[] array10 = new double[count];
		double[] array11 = new double[count];
		checked
		{
			for (int i = 0; i < count; i++)
			{
				var (date, num2) = tpList[i];
				array[i] = num2;
				MamaResult mamaResult = new MamaResult(date);
				list.Add(mamaResult);
				if (i > 5)
				{
					double num3 = 0.075 * array4[i - 1] + 0.54;
					array2[i] = (4.0 * array[i] + 3.0 * array[i - 1] + 2.0 * array[i - 2] + array[i - 3]) / 10.0;
					array3[i] = (0.0962 * array2[i] + 0.5769 * array2[i - 2] - 0.5769 * array2[i - 4] - 0.0962 * array2[i - 6]) * num3;
					array5[i] = (0.0962 * array3[i] + 0.5769 * array3[i - 2] - 0.5769 * array3[i - 4] - 0.0962 * array3[i - 6]) * num3;
					array6[i] = array3[i - 3];
					double num4 = (0.0962 * array6[i] + 0.5769 * array6[i - 2] - 0.5769 * array6[i - 4] - 0.0962 * array6[i - 6]) * num3;
					double num5 = (0.0962 * array5[i] + 0.5769 * array5[i - 2] - 0.5769 * array5[i - 4] - 0.0962 * array5[i - 6]) * num3;
					array8[i] = array6[i] - num5;
					array7[i] = array5[i] + num4;
					array8[i] = 0.2 * array8[i] + 0.8 * array8[i - 1];
					array7[i] = 0.2 * array7[i] + 0.8 * array7[i - 1];
					array9[i] = array8[i] * array8[i - 1] + array7[i] * array7[i - 1];
					array10[i] = array8[i] * array7[i - 1] - array7[i] * array8[i - 1];
					array9[i] = 0.2 * array9[i] + 0.8 * array9[i - 1];
					array10[i] = 0.2 * array10[i] + 0.8 * array10[i - 1];
					array4[i] = ((array10[i] != 0.0 && array9[i] != 0.0) ? (Math.PI * 2.0 / Math.Atan(array10[i] / array9[i])) : 0.0);
					array4[i] = ((array4[i] > 1.5 * array4[i - 1]) ? (1.5 * array4[i - 1]) : array4[i]);
					array4[i] = ((array4[i] < 0.67 * array4[i - 1]) ? (0.67 * array4[i - 1]) : array4[i]);
					array4[i] = ((array4[i] < 6.0) ? 6.0 : array4[i]);
					array4[i] = ((array4[i] > 50.0) ? 50.0 : array4[i]);
					array4[i] = 0.2 * array4[i] + 0.8 * array4[i - 1];
					array11[i] = ((array6[i] != 0.0) ? (Math.Atan(array5[i] / array6[i]) * 180.0 / Math.PI) : 0.0);
					double num6 = Math.Max(array11[i - 1] - array11[i], 1.0);
					double num7 = Math.Max(fastLimit / num6, slowLimit);
					mamaResult.Mama = (num7 * array[i] + (1.0 - num7) * list[i - 1].Mama).NaN2Null();
					mamaResult.Fama = (0.5 * num7 * mamaResult.Mama + (1.0 - 0.5 * num7) * list[i - 1].Fama).NaN2Null();
				}
				else
				{
					num += array[i];
					if (i == 5)
					{
						mamaResult.Mama = (num / 6.0).NaN2Null();
						mamaResult.Fama = mamaResult.Mama;
					}
					array4[i] = 0.0;
					array2[i] = 0.0;
					array3[i] = 0.0;
					array6[i] = 0.0;
					array5[i] = 0.0;
					array8[i] = 0.0;
					array7[i] = 0.0;
					array9[i] = 0.0;
					array10[i] = 0.0;
					array11[i] = 0.0;
				}
			}
			return list;
		}
	}

	private static void ValidateMama(double fastLimit, double slowLimit)
	{
		if (fastLimit <= slowLimit || fastLimit >= 1.0)
		{
			throw new ArgumentOutOfRangeException("fastLimit", fastLimit, "Fast Limit must be greater than Slow Limit and less than 1 for MAMA.");
		}
		if (slowLimit <= 0.0)
		{
			throw new ArgumentOutOfRangeException("slowLimit", slowLimit, "Slow Limit must be greater than 0 for MAMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<MamaResult> RemoveWarmupPeriods(this IEnumerable<MamaResult> results)
	{
		return results.Remove(50);
	}

	/// <summary>
	///     Marubozu is a single candlestick pattern that has no wicks, representing consistent directional movement.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Patterns/Marubozu/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="minBodyPercent">Optional.  Minimum candle body size as percentage.</param><returns>Time series of Marubozu values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<CandleResult> GetMarubozu<TQuote>(this IEnumerable<TQuote> quotes, double minBodyPercent = 95.0) where TQuote : IQuote
	{
		return quotes.CalcMarubozu(minBodyPercent);
	}

	/// <summary>
	///     Marubozu is a single candlestick pattern that has no wicks, representing consistent directional movement.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Patterns/Marubozu/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="minBodyPercent">Optional.  Minimum candle body size as percentage.</param><returns>Time series of Marubozu values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	internal static List<CandleResult> CalcMarubozu<TQuote>(this IEnumerable<TQuote> quotes, double minBodyPercent) where TQuote : IQuote
	{
		ValidateMarubozu(minBodyPercent);
		List<CandleResult> list = quotes.ToCandleResults();
		minBodyPercent /= 100.0;
		int count = list.Count;
		for (int i = 0; i < count; i = checked(i + 1))
		{
			CandleResult candleResult = list[i];
			if (candleResult.Candle.BodyPct >= minBodyPercent)
			{
				candleResult.Price = candleResult.Candle.Close;
				candleResult.Match = (candleResult.Candle.IsBullish ? Match.BullSignal : Match.BearSignal);
			}
		}
		return list;
	}

	private static void ValidateMarubozu(double minBodyPercent)
	{
		if (minBodyPercent > 100.0)
		{
			throw new ArgumentOutOfRangeException("minBodyPercent", minBodyPercent, "Minimum Body Percent must be less than 100 for Marubozu (<=100%).");
		}
		if (minBodyPercent < 80.0)
		{
			throw new ArgumentOutOfRangeException("minBodyPercent", minBodyPercent, "Minimum Body Percent must at least 80 (80%) for Marubozu and is usually greater than 90 (90%).");
		}
	}

	/// <summary>
	///     Money Flow Index (MFI) is a price-volume oscillator that shows buying and selling momentum.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Mfi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of MFI values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<MfiResult> GetMfi<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcMfi(lookbackPeriods);
	}

	internal static List<MfiResult> CalcMfi(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateMfi(lookbackPeriods);
		int count = qdList.Count;
		List<MfiResult> list = new List<MfiResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		int[] array3 = new int[count];
		double? num = null;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				MfiResult item = new MfiResult(quoteD.Date);
				list.Add(item);
				array[i] = (quoteD.High + quoteD.Low + quoteD.Close) / 3.0;
				array2[i] = array[i] * quoteD.Volume;
				if (!num.HasValue || array[i] == num)
				{
					array3[i] = 0;
				}
				else if (array[i] > num)
				{
					array3[i] = 1;
				}
				else if (array[i] < num)
				{
					array3[i] = -1;
				}
				num = array[i];
			}
			for (int j = lookbackPeriods; j < list.Count; j++)
			{
				MfiResult mfiResult = list[j];
				double num2 = 0.0;
				double num3 = 0.0;
				for (int k = j + 1 - lookbackPeriods; k <= j; k++)
				{
					if (array3[k] == 1)
					{
						num2 += array2[k];
					}
					else if (array3[k] == -1)
					{
						num3 += array2[k];
					}
				}
				if (num3 != 0.0)
				{
					mfiResult.Mfi = 100.0 - 100.0 / (1.0 + new double?(num2 / num3));
				}
				else
				{
					mfiResult.Mfi = 100.0;
				}
			}
			return list;
		}
	}

	private static void ValidateMfi(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for MFI.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<MfiResult> RemoveWarmupPeriods(this IEnumerable<MfiResult> results)
	{
		int removePeriods = results.ToList().FindIndex((MfiResult x) => x.Mfi.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     On-balance Volume (OBV) is a rolling accumulation of volume based on Close price direction.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Obv/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="smaPeriods">Optional.  Number of periods for an SMA of the OBV line.</param><returns>Time series of OBV values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ObvResult> GetObv<TQuote>(this IEnumerable<TQuote> quotes, int? smaPeriods = null) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcObv(smaPeriods);
	}

	internal static List<ObvResult> CalcObv(this List<QuoteD> qdList, int? smaPeriods)
	{
		ValidateObv(smaPeriods);
		List<ObvResult> list = new List<ObvResult>(qdList.Count);
		double num = double.NaN;
		double num2 = 0.0;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				if (!double.IsNaN(num) && quoteD.Close != num)
				{
					if (quoteD.Close > num)
					{
						num2 += quoteD.Volume;
					}
					else if (quoteD.Close < num)
					{
						num2 -= quoteD.Volume;
					}
				}
				ObvResult obvResult = new ObvResult(quoteD.Date)
				{
					Obv = num2
				};
				list.Add(obvResult);
				num = quoteD.Close;
				if (smaPeriods.HasValue && i + 1 > smaPeriods)
				{
					double? num3 = 0.0;
					for (int j = i + 1 - smaPeriods.Value; j <= i; j++)
					{
						num3 += list[j].Obv;
					}
					obvResult.ObvSma = num3 / (double?)smaPeriods;
				}
			}
			return list;
		}
	}

	private static void ValidateObv(int? smaPeriods)
	{
		if (smaPeriods.HasValue && smaPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "SMA periods must be greater than 0 for OBV.");
		}
	}

	/// <summary>
	///       Parabolic SAR (stop and reverse) is a price-time based indicator used to determine trend direction and reversals.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/ParabolicSar/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="accelerationStep">Incremental step size.</param><param name="maxAccelerationFactor">Maximum step threshold.</param><returns>Time series of Parabolic SAR values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ParabolicSarResult> GetParabolicSar<TQuote>(this IEnumerable<TQuote> quotes, double accelerationStep = 0.02, double maxAccelerationFactor = 0.2) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcParabolicSar(accelerationStep, maxAccelerationFactor, accelerationStep);
	}

	/// <summary>
	///       Parabolic SAR (stop and reverse) is a price-time based indicator used to determine trend direction and reversals.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/ParabolicSar/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="accelerationStep">Incremental step size.</param><param name="maxAccelerationFactor">Maximum step threshold.</param><param name="initialFactor">Initial starting acceleration factor.</param><returns>Time series of Parabolic SAR values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ParabolicSarResult> GetParabolicSar<TQuote>(this IEnumerable<TQuote> quotes, double accelerationStep, double maxAccelerationFactor, double initialFactor) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcParabolicSar(accelerationStep, maxAccelerationFactor, initialFactor);
	}

	internal static List<ParabolicSarResult> CalcParabolicSar(this List<QuoteD> qdList, double accelerationStep, double maxAccelerationFactor, double initialFactor)
	{
		ValidateParabolicSar(accelerationStep, maxAccelerationFactor, initialFactor);
		int count = qdList.Count;
		List<ParabolicSarResult> list = new List<ParabolicSarResult>(count);
		if (count == 0)
		{
			return list;
		}
		QuoteD quoteD = qdList[0];
		double num = initialFactor;
		double num2 = quoteD.High;
		double num3 = quoteD.Low;
		bool flag = true;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD2 = qdList[i];
				ParabolicSarResult parabolicSarResult = new ParabolicSarResult(quoteD2.Date);
				list.Add(parabolicSarResult);
				if (i == 0)
				{
					continue;
				}
				if (flag)
				{
					double num4 = num3 + num * (num2 - num3);
					if (i >= 2)
					{
						double val = Math.Min(qdList[i - 1].Low, qdList[i - 2].Low);
						num4 = Math.Min(num4, val);
					}
					if (quoteD2.Low < num4)
					{
						parabolicSarResult.IsReversal = true;
						parabolicSarResult.Sar = num2;
						flag = false;
						num = initialFactor;
						num2 = quoteD2.Low;
					}
					else
					{
						parabolicSarResult.IsReversal = false;
						parabolicSarResult.Sar = num4;
						if (quoteD2.High > num2)
						{
							num2 = quoteD2.High;
							num = Math.Min(num + accelerationStep, maxAccelerationFactor);
						}
					}
				}
				else
				{
					double num5 = num3 - num * (num3 - num2);
					if (i >= 2)
					{
						double val2 = Math.Max(qdList[i - 1].High, qdList[i - 2].High);
						num5 = Math.Max(num5, val2);
					}
					if (quoteD2.High > num5)
					{
						parabolicSarResult.IsReversal = true;
						parabolicSarResult.Sar = num2;
						flag = true;
						num = initialFactor;
						num2 = quoteD2.High;
					}
					else
					{
						parabolicSarResult.IsReversal = false;
						parabolicSarResult.Sar = num5;
						if (quoteD2.Low < num2)
						{
							num2 = quoteD2.Low;
							num = Math.Min(num + accelerationStep, maxAccelerationFactor);
						}
					}
				}
				num3 = parabolicSarResult.Sar.Value;
			}
			ParabolicSarResult parabolicSarResult2 = (from x in list
				where x.IsReversal == true
				orderby x.Date
				select x).FirstOrDefault();
			int num6 = ((parabolicSarResult2 != null) ? list.IndexOf(parabolicSarResult2) : (count - 1));
			for (int num7 = 0; num7 <= num6; num7++)
			{
				ParabolicSarResult parabolicSarResult3 = list[num7];
				parabolicSarResult3.Sar = null;
				parabolicSarResult3.IsReversal = null;
			}
			return list;
		}
	}

	private static void ValidateParabolicSar(double accelerationStep, double maxAccelerationFactor, double initialFactor)
	{
		if (accelerationStep <= 0.0)
		{
			throw new ArgumentOutOfRangeException("accelerationStep", accelerationStep, "Acceleration Step must be greater than 0 for Parabolic SAR.");
		}
		if (maxAccelerationFactor <= 0.0)
		{
			throw new ArgumentOutOfRangeException("maxAccelerationFactor", maxAccelerationFactor, "Max Acceleration Factor must be greater than 0 for Parabolic SAR.");
		}
		if (accelerationStep > maxAccelerationFactor)
		{
			string message = string.Format(invCulture, "Acceleration Step cannot be larger than the Max Acceleration Factor ({0}) for Parabolic SAR.", maxAccelerationFactor);
			throw new ArgumentOutOfRangeException("accelerationStep", accelerationStep, message);
		}
		if (initialFactor <= 0.0 || initialFactor > maxAccelerationFactor)
		{
			throw new ArgumentOutOfRangeException("initialFactor", initialFactor, "Initial Factor must be greater than 0 and not larger than Max Acceleration Factor for Parabolic SAR.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<ParabolicSarResult> RemoveWarmupPeriods(this IEnumerable<ParabolicSarResult> results)
	{
		int removePeriods = results.ToList().FindIndex((ParabolicSarResult x) => x.Sar.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Pivot Points depict support and resistance levels, based on the prior lookback window. You can specify window size (e.g. month, week, day, etc).
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/PivotPoints/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="windowSize">Calendar size of the lookback window.</param><param name="pointType">Pivot Point type.</param><returns>Time series of Pivot Points values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<PivotPointsResult> GetPivotPoints<TQuote>(this IEnumerable<TQuote> quotes, PeriodSize windowSize, PivotPointType pointType = PivotPointType.Standard) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcPivotPoints(windowSize, pointType);
	}

	internal static List<PivotPointsResult> CalcPivotPoints<TQuote>(this List<TQuote> quotesList, PeriodSize windowSize, PivotPointType pointType) where TQuote : IQuote
	{
		int count = quotesList.Count;
		List<PivotPointsResult> list = new List<PivotPointsResult>(count);
		PivotPointsResult pivotPointsResult = new PivotPointsResult();
		if (count == 0)
		{
			return list;
		}
		TQuote val = quotesList[0];
		int num = GetWindowNumber(val.Date, windowSize);
		bool flag = true;
		decimal num2 = val.High;
		decimal num3 = val.Low;
		decimal open = val.Open;
		decimal close = val.Close;
		for (int i = 0; i < count; i = checked(i + 1))
		{
			TQuote val2 = quotesList[i];
			PivotPointsResult pivotPointsResult2 = new PivotPointsResult
			{
				Date = val2.Date
			};
			int windowNumber = GetWindowNumber(val2.Date, windowSize);
			if (windowNumber != num)
			{
				num = windowNumber;
				flag = false;
				if (pointType == PivotPointType.Woodie)
				{
					open = val2.Open;
				}
				pivotPointsResult = GetPivotPoint<PivotPointsResult>(pointType, open, num2, num3, close);
				open = val2.Open;
				num2 = val2.High;
				num3 = val2.Low;
			}
			if (!flag)
			{
				pivotPointsResult2.PP = pivotPointsResult?.PP;
				pivotPointsResult2.S1 = pivotPointsResult?.S1;
				pivotPointsResult2.S2 = pivotPointsResult?.S2;
				pivotPointsResult2.S3 = pivotPointsResult?.S3;
				pivotPointsResult2.S4 = pivotPointsResult?.S4;
				pivotPointsResult2.R1 = pivotPointsResult?.R1;
				pivotPointsResult2.R2 = pivotPointsResult?.R2;
				pivotPointsResult2.R3 = pivotPointsResult?.R3;
				pivotPointsResult2.R4 = pivotPointsResult?.R4;
			}
			list.Add(pivotPointsResult2);
			num2 = ((val2.High > num2) ? val2.High : num2);
			num3 = ((val2.Low < num3) ? val2.Low : num3);
			close = val2.Close;
		}
		return list;
	}

	internal static TPivotPoint GetPivotPointStandard<TPivotPoint>(decimal high, decimal low, decimal close) where TPivotPoint : IPivotPoint, new()
	{
		decimal num = (high + low + close) / 3m;
		TPivotPoint result = new TPivotPoint();
		decimal? pP = num;
		result.PP = pP;
		decimal? s = num * 2m - high;
		result.S1 = s;
		decimal? s2 = num - (high - low);
		result.S2 = s2;
		decimal? s3 = low - 2m * (high - num);
		result.S3 = s3;
		decimal? r = num * 2m - low;
		result.R1 = r;
		decimal? r2 = num + (high - low);
		result.R2 = r2;
		decimal? r3 = high + 2m * (num - low);
		result.R3 = r3;
		return result;
	}

	internal static TPivotPoint GetPivotPointCamarilla<TPivotPoint>(decimal high, decimal low, decimal close) where TPivotPoint : IPivotPoint, new()
	{
		TPivotPoint result = new TPivotPoint();
		decimal? pP = close;
		result.PP = pP;
		decimal? s = close - 0.0916666666666666666666666667m * (high - low);
		result.S1 = s;
		decimal? s2 = close - 0.1833333333333333333333333333m * (high - low);
		result.S2 = s2;
		decimal? s3 = close - 0.275m * (high - low);
		result.S3 = s3;
		decimal? s4 = close - 0.55m * (high - low);
		result.S4 = s4;
		decimal? r = close + 0.0916666666666666666666666667m * (high - low);
		result.R1 = r;
		decimal? r2 = close + 0.1833333333333333333333333333m * (high - low);
		result.R2 = r2;
		decimal? r3 = close + 0.275m * (high - low);
		result.R3 = r3;
		decimal? r4 = close + 0.55m * (high - low);
		result.R4 = r4;
		return result;
	}

	internal static TPivotPoint GetPivotPointDemark<TPivotPoint>(decimal open, decimal high, decimal low, decimal close) where TPivotPoint : IPivotPoint, new()
	{
		decimal? num = ((close < open) ? (high + 2m * low + close) : ((close > open) ? (2m * high + low + close) : (high + low + 2m * close)));
		TPivotPoint result = new TPivotPoint();
		decimal? pP = num / (decimal?)4;
		result.PP = pP;
		decimal? s = num / (decimal?)2 - (decimal?)high;
		result.S1 = s;
		decimal? r = num / (decimal?)2 - (decimal?)low;
		result.R1 = r;
		return result;
	}

	internal static TPivotPoint GetPivotPointFibonacci<TPivotPoint>(decimal high, decimal low, decimal close) where TPivotPoint : IPivotPoint, new()
	{
		decimal num = (high + low + close) / 3m;
		TPivotPoint result = new TPivotPoint();
		decimal? pP = num;
		result.PP = pP;
		decimal? s = num - 0.382m * (high - low);
		result.S1 = s;
		decimal? s2 = num - 0.618m * (high - low);
		result.S2 = s2;
		decimal? s3 = num - 1.000m * (high - low);
		result.S3 = s3;
		decimal? r = num + 0.382m * (high - low);
		result.R1 = r;
		decimal? r2 = num + 0.618m * (high - low);
		result.R2 = r2;
		decimal? r3 = num + 1.000m * (high - low);
		result.R3 = r3;
		return result;
	}

	internal static TPivotPoint GetPivotPointWoodie<TPivotPoint>(decimal currentOpen, decimal high, decimal low) where TPivotPoint : IPivotPoint, new()
	{
		decimal num = (high + low + 2m * currentOpen) / 4m;
		TPivotPoint result = new TPivotPoint();
		decimal? pP = num;
		result.PP = pP;
		decimal? s = num * 2m - high;
		result.S1 = s;
		decimal? s2 = num - high + low;
		result.S2 = s2;
		decimal? s3 = low - 2m * (high - num);
		result.S3 = s3;
		decimal? r = num * 2m - low;
		result.R1 = r;
		decimal? r2 = num + high - low;
		result.R2 = r2;
		decimal? r3 = high + 2m * (num - low);
		result.R3 = r3;
		return result;
	}

	internal static TPivotPoint GetPivotPoint<TPivotPoint>(PivotPointType pointType, decimal open, decimal high, decimal low, decimal close) where TPivotPoint : IPivotPoint, new()
	{
		return pointType switch
		{
			PivotPointType.Standard => GetPivotPointStandard<TPivotPoint>(high, low, close), 
			PivotPointType.Camarilla => GetPivotPointCamarilla<TPivotPoint>(high, low, close), 
			PivotPointType.Demark => GetPivotPointDemark<TPivotPoint>(open, high, low, close), 
			PivotPointType.Fibonacci => GetPivotPointFibonacci<TPivotPoint>(high, low, close), 
			PivotPointType.Woodie => GetPivotPointWoodie<TPivotPoint>(open, high, low), 
			_ => throw new ArgumentOutOfRangeException("pointType", pointType, "Invalid pointType provided."), 
		};
	}

	private static int GetWindowNumber(DateTime d, PeriodSize windowSize)
	{
		return windowSize switch
		{
			PeriodSize.Month => d.Month, 
			PeriodSize.Week => invCalendar.GetWeekOfYear(d, invCalendarWeekRule, invFirstDayOfWeek), 
			PeriodSize.Day => d.Day, 
			PeriodSize.OneHour => d.Hour, 
			_ => throw new ArgumentOutOfRangeException("windowSize", windowSize, string.Format(invCulture, "Pivot Points does not support PeriodSize of {0}.  See documentation for valid options.", Enum.GetName(typeof(PeriodSize), windowSize))), 
		};
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<PivotPointsResult> RemoveWarmupPeriods(this IEnumerable<PivotPointsResult> results)
	{
		int removePeriods = results.ToList().FindIndex((PivotPointsResult x) => x.PP.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Pivots is an extended version of Williams Fractal that includes identification of Higher High, Lower Low, Higher Low, and Lower Low trends between pivots in a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Pivots/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="leftSpan">Number of span periods to the left of the evaluation period.</param><param name="rightSpan">Number of span periods to the right of the evaluation period.</param><param name="maxTrendPeriods">Number of periods in the lookback window.</param><param name="endType">Determines use of Close or High/Low wicks for points.</param><returns>Time series of Pivots values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<PivotsResult> GetPivots<TQuote>(this IEnumerable<TQuote> quotes, int leftSpan = 2, int rightSpan = 2, int maxTrendPeriods = 20, EndType endType = EndType.HighLow) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcPivots(leftSpan, rightSpan, maxTrendPeriods, endType);
	}

	internal static List<PivotsResult> CalcPivots<TQuote>(this List<TQuote> quotesList, int leftSpan, int rightSpan, int maxTrendPeriods, EndType endType) where TQuote : IQuote
	{
		ValidatePivots(leftSpan, rightSpan, maxTrendPeriods);
		List<PivotsResult> list = (from x in quotesList.CalcFractal(leftSpan, rightSpan, endType)
			select new PivotsResult(x.Date)
			{
				HighPoint = x.FractalBear,
				LowPoint = x.FractalBull
			}).ToList();
		int? num = null;
		decimal? num2 = null;
		int? num3 = null;
		decimal? num4 = null;
		checked
		{
			for (int num5 = leftSpan; num5 <= list.Count - rightSpan; num5++)
			{
				PivotsResult pivotsResult = list[num5];
				if (num < num5 - maxTrendPeriods)
				{
					num = null;
					num2 = null;
				}
				if (num3 < num5 - maxTrendPeriods)
				{
					num3 = null;
					num4 = null;
				}
				if (pivotsResult.HighPoint.HasValue)
				{
					if (num.HasValue && !(pivotsResult.HighPoint == num2))
					{
						PivotTrend value = ((!(pivotsResult.HighPoint > num2)) ? PivotTrend.LH : PivotTrend.HH);
						list[num.Value].HighLine = num2;
						decimal? num6 = (pivotsResult.HighPoint - num2) / (decimal?)(num5 - num);
						for (int num7 = num.Value + 1; num7 <= num5; num7++)
						{
							list[num7].HighTrend = value;
							list[num7].HighLine = pivotsResult.HighPoint + num6 * (decimal?)(num7 - num5);
						}
					}
					num = num5;
					num2 = pivotsResult.HighPoint;
				}
				if (!pivotsResult.LowPoint.HasValue)
				{
					continue;
				}
				if (num3.HasValue && !(pivotsResult.LowPoint == num4))
				{
					PivotTrend value2 = ((pivotsResult.LowPoint > num4) ? PivotTrend.HL : PivotTrend.LL);
					list[num3.Value].LowLine = num4;
					decimal? num8 = (pivotsResult.LowPoint - num4) / (decimal?)(num5 - num3);
					for (int num9 = num3.Value + 1; num9 <= num5; num9++)
					{
						list[num9].LowTrend = value2;
						list[num9].LowLine = pivotsResult.LowPoint + num8 * (decimal?)(num9 - num5);
					}
				}
				num3 = num5;
				num4 = pivotsResult.LowPoint;
			}
			return list;
		}
	}

	internal static void ValidatePivots(int leftSpan, int rightSpan, int maxTrendPeriods, string caller = "Pivots")
	{
		if (rightSpan < 2)
		{
			throw new ArgumentOutOfRangeException("rightSpan", rightSpan, "Right span must be at least 2 for " + caller + ".");
		}
		if (leftSpan < 2)
		{
			throw new ArgumentOutOfRangeException("leftSpan", leftSpan, "Left span must be at least 2 for " + caller + ".");
		}
		if (maxTrendPeriods <= leftSpan)
		{
			throw new ArgumentOutOfRangeException("leftSpan", leftSpan, "Lookback periods must be greater than the Left window span for " + caller + ".");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<PivotsResult> Condense(this IEnumerable<PivotsResult> results)
	{
		List<PivotsResult> list = results.ToList();
		list.RemoveAll((PivotsResult x) => !x.HighPoint.HasValue && !x.LowPoint.HasValue);
		return list.ToSortedList();
	}

	/// <summary>
	///     Price Momentum Oscillator (PMO) is double-smoothed ROC based momentum indicator.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Pmo/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="timePeriods">Number of periods for ROC EMA smoothing.</param><param name="smoothPeriods">Number of periods for PMO EMA smoothing.</param><param name="signalPeriods">Number of periods for Signal line EMA.</param><returns>Time series of PMO values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<PmoResult> GetPmo<TQuote>(this IEnumerable<TQuote> quotes, int timePeriods = 35, int smoothPeriods = 20, int signalPeriods = 10) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcPmo(timePeriods, smoothPeriods, signalPeriods);
	}

	public static IEnumerable<PmoResult> GetPmo(this IEnumerable<IReusableResult> results, int timePeriods = 35, int smoothPeriods = 20, int signalPeriods = 10)
	{
		return results.ToTuple().CalcPmo(timePeriods, smoothPeriods, signalPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<PmoResult> GetPmo(this IEnumerable<(DateTime, double)> priceTuples, int timePeriods = 35, int smoothPeriods = 20, int signalPeriods = 10)
	{
		return priceTuples.ToSortedList().CalcPmo(timePeriods, smoothPeriods, signalPeriods);
	}

	internal static List<PmoResult> CalcPmo(this List<(DateTime, double)> tpList, int timePeriods, int smoothPeriods, int signalPeriods)
	{
		ValidatePmo(timePeriods, smoothPeriods, signalPeriods);
		List<PmoResult> list = tpList.CalcPmoRocEma(timePeriods);
		double num = 2.0 / (double)smoothPeriods;
		double? num2 = null;
		checked
		{
			int num3 = timePeriods + smoothPeriods;
			for (int i = num3 - 1; i < list.Count; i++)
			{
				PmoResult pmoResult = list[i];
				if (i + 1 > num3)
				{
					pmoResult.Pmo = (pmoResult.RocEma - num2) * num + num2;
				}
				else if (i + 1 == num3)
				{
					double? num4 = 0.0;
					for (int j = i + 1 - smoothPeriods; j <= i; j++)
					{
						num4 += list[j].RocEma;
					}
					pmoResult.Pmo = num4 / (double)smoothPeriods;
				}
				num2 = pmoResult.Pmo;
			}
			CalcPmoSignal(list, timePeriods, smoothPeriods, signalPeriods);
			return list;
		}
	}

	private static List<PmoResult> CalcPmoRocEma(this List<(DateTime, double)> tpList, int timePeriods)
	{
		double num = 2.0 / (double)timePeriods;
		double? num2 = null;
		List<RocResult> list = tpList.CalcRoc(1, null).ToList();
		List<PmoResult> list2 = new List<PmoResult>();
		checked
		{
			int num3 = timePeriods + 1;
			for (int i = 0; i < list.Count; i++)
			{
				RocResult rocResult = list[i];
				PmoResult pmoResult = new PmoResult(rocResult.Date);
				list2.Add(pmoResult);
				if (i + 1 > num3)
				{
					pmoResult.RocEma = rocResult.Roc * num + num2 * (1.0 - num);
				}
				else if (i + 1 == num3)
				{
					double? num4 = 0.0;
					for (int j = i + 1 - timePeriods; j <= i; j++)
					{
						num4 += list[j].Roc;
					}
					pmoResult.RocEma = num4 / (double)timePeriods;
				}
				num2 = pmoResult.RocEma;
				pmoResult.RocEma *= 10.0;
			}
			return list2;
		}
	}

	private static void CalcPmoSignal(List<PmoResult> results, int timePeriods, int smoothPeriods, int signalPeriods)
	{
		checked
		{
			double num = 2.0 / (double)(signalPeriods + 1);
			double? num2 = null;
			int num3 = timePeriods + smoothPeriods + signalPeriods - 1;
			for (int i = num3 - 1; i < results.Count; i++)
			{
				PmoResult pmoResult = results[i];
				if (i + 1 > num3)
				{
					pmoResult.Signal = (pmoResult.Pmo - num2) * num + num2;
				}
				else if (i + 1 == num3)
				{
					double? num4 = 0.0;
					for (int j = i + 1 - signalPeriods; j <= i; j++)
					{
						num4 += results[j].Pmo;
					}
					pmoResult.Signal = num4 / (double)signalPeriods;
				}
				num2 = pmoResult.Signal;
			}
		}
	}

	private static void ValidatePmo(int timePeriods, int smoothPeriods, int signalPeriods)
	{
		if (timePeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("timePeriods", timePeriods, "Time periods must be greater than 1 for PMO.");
		}
		if (smoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("smoothPeriods", smoothPeriods, "Smoothing periods must be greater than 0 for PMO.");
		}
		if (signalPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than 0 for PMO.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<PmoResult> RemoveWarmupPeriods(this IEnumerable<PmoResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((PmoResult x) => x.Pmo.HasValue) + 1;
			return results.Remove(num + 250);
		}
	}

	/// <summary>
	///     Price Relative Strength (PRS), also called Comparative Relative Strength,
	///     shows the ratio of two quote histories. It is often used to compare
	///     against a market index or sector ETF. When using the optional lookbackPeriods,
	///     this also return relative percent change over the specified periods.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Prs/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotesEval">Historical price quotes for evaluation.</param><param name="quotesBase">This is usually market index data, but could be any baseline data that you might use for comparison.</param><param name="lookbackPeriods">Optional. Number of periods for % difference.</param><param name="smaPeriods">Optional.  Number of periods for a PRS SMA signal line.</param><returns>Time series of PRS values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Invalid quotes provided.</exception>
	public static IEnumerable<PrsResult> GetPrs<TQuote>(this IEnumerable<TQuote> quotesEval, IEnumerable<TQuote> quotesBase, int? lookbackPeriods = null, int? smaPeriods = null) where TQuote : IQuote
	{
		List<(DateTime, double)> tpListBase = quotesBase.ToTuple(CandlePart.Close);
		return CalcPrs(quotesEval.ToTuple(CandlePart.Close), tpListBase, lookbackPeriods, smaPeriods);
	}

	public static IEnumerable<PrsResult> GetPrs(this IEnumerable<IReusableResult> quotesEval, IEnumerable<IReusableResult> quotesBase, int? lookbackPeriods = null, int? smaPeriods = null)
	{
		List<(DateTime Date, double Value)> tpListEval = quotesEval.ToTuple();
		List<(DateTime, double)> tpListBase = quotesBase.ToTuple();
		return CalcPrs(tpListEval, tpListBase, lookbackPeriods, smaPeriods).SyncIndex(quotesEval, SyncType.Prepend);
	}

	public static IEnumerable<PrsResult> GetPrs(this IEnumerable<(DateTime, double)> tupleEval, IEnumerable<(DateTime, double)> tupleBase, int? lookbackPeriods = null, int? smaPeriods = null)
	{
		List<(DateTime, double)> tpListBase = tupleBase.ToSortedList();
		return CalcPrs(tupleEval.ToSortedList(), tpListBase, lookbackPeriods, smaPeriods);
	}

	internal static List<PrsResult> CalcPrs(List<(DateTime, double)> tpListEval, List<(DateTime, double)> tpListBase, int? lookbackPeriods = null, int? smaPeriods = null)
	{
		ValidatePriceRelative(tpListEval, tpListBase, lookbackPeriods, smaPeriods);
		List<PrsResult> list = new List<PrsResult>(tpListEval.Count);
		checked
		{
			for (int i = 0; i < tpListEval.Count; i++)
			{
				var (dateTime, num) = tpListBase[i];
				var (dateTime2, num2) = tpListEval[i];
				if (dateTime2 != dateTime)
				{
					throw new InvalidQuotesException("tpListEval", dateTime2, "Date sequence does not match.  Price Relative requires matching dates in provided histories.");
				}
				PrsResult prsResult = new PrsResult(dateTime2)
				{
					Prs = ((num == 0.0) ? ((double?)null) : (num2 / num).NaN2Null())
				};
				list.Add(prsResult);
				if (lookbackPeriods.HasValue && i + 1 > lookbackPeriods)
				{
					double item = tpListBase[i - lookbackPeriods.Value].Item2;
					double item2 = tpListEval[i - lookbackPeriods.Value].Item2;
					if (item != 0.0 && item2 != 0.0)
					{
						double? num3 = (num - item) / item;
						prsResult.PrsPercent = (new double?((num2 - item2) / item2) - num3).NaN2Null();
					}
				}
				if (smaPeriods.HasValue && i + 1 >= smaPeriods)
				{
					double? num4 = 0.0;
					for (int j = i + 1 - smaPeriods.Value; j <= i; j++)
					{
						num4 += list[j].Prs;
					}
					prsResult.PrsSma = (num4 / (double?)smaPeriods).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidatePriceRelative(List<(DateTime, double)> quotesEval, List<(DateTime, double)> quotesBase, int? lookbackPeriods, int? smaPeriods)
	{
		if (lookbackPeriods.HasValue && lookbackPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Price Relative Strength.");
		}
		if (smaPeriods.HasValue && smaPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "SMA periods must be greater than 0 for Price Relative Strength.");
		}
		int count = quotesEval.Count;
		int count2 = quotesBase.Count;
		int? num = lookbackPeriods;
		if (num.HasValue && count < num)
		{
			string message = "Insufficient quotes provided for Price Relative Strength.  " + string.Format(invCulture, "You provided {0} periods of quotes when at least {1} are required.", count, num);
			throw new InvalidQuotesException("quotesEval", message);
		}
		if (count2 != count)
		{
			throw new InvalidQuotesException("quotesBase", "Base quotes should have at least as many records as Eval quotes for PRS.");
		}
	}

	/// <summary>
	///     Percentage Volume Oscillator (PVO) is a simple oscillator view of two converging/diverging exponential moving averages of Volume.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Pvo/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="fastPeriods">Number of periods in the Fast moving average.</param><param name="slowPeriods">Number of periods in the Slow moving average.</param><param name="signalPeriods">Number of periods for the PVO SMA signal line.</param><returns>Time series of PVO values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<PvoResult> GetPvo<TQuote>(this IEnumerable<TQuote> quotes, int fastPeriods = 12, int slowPeriods = 26, int signalPeriods = 9) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Volume).CalcPvo(fastPeriods, slowPeriods, signalPeriods);
	}

	internal static List<PvoResult> CalcPvo(this List<(DateTime, double)> tpList, int fastPeriods, int slowPeriods, int signalPeriods)
	{
		ValidatePvo(fastPeriods, slowPeriods, signalPeriods);
		List<EmaResult> list = tpList.CalcEma(fastPeriods);
		List<EmaResult> list2 = tpList.CalcEma(slowPeriods);
		int count = tpList.Count;
		List<(DateTime, double)> list3 = new List<(DateTime, double)>();
		List<PvoResult> list4 = new List<PvoResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				DateTime item = tpList[i].Item1;
				EmaResult emaResult = list[i];
				EmaResult emaResult2 = list2[i];
				PvoResult pvoResult = new PvoResult(item);
				list4.Add(pvoResult);
				if (i >= slowPeriods - 1)
				{
					double? num = (pvoResult.Pvo = ((emaResult2.Ema == 0.0) ? ((double?)null) : (100.0 * ((emaResult.Ema - emaResult2.Ema) / emaResult2.Ema))));
					(DateTime, double) item2 = (item, (!num.HasValue) ? 0.0 : num.Value);
					list3.Add(item2);
				}
			}
			List<EmaResult> list5 = list3.CalcEma(signalPeriods);
			for (int j = slowPeriods - 1; j < count; j++)
			{
				PvoResult pvoResult2 = list4[j];
				EmaResult emaResult3 = list5[j + 1 - slowPeriods];
				pvoResult2.Signal = emaResult3.Ema;
				pvoResult2.Histogram = pvoResult2.Pvo - pvoResult2.Signal;
			}
			return list4;
		}
	}

	private static void ValidatePvo(int fastPeriods, int slowPeriods, int signalPeriods)
	{
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast periods must be greater than 0 for PVO.");
		}
		if (signalPeriods < 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than or equal to 0 for PVO.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow periods must be greater than the fast period for PVO.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<PvoResult> RemoveWarmupPeriods(this IEnumerable<PvoResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((PvoResult x) => x.Signal.HasValue) + 2;
			return results.Remove(num + 250);
		}
	}

	/// <summary>
	///       Renko Chart is a modified Japanese candlestick pattern that uses time-lapsed bricks.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Renko/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="brickSize">Fixed brick size ($).</param><param name="endType">End type.  See documentation.</param><returns>Time series of Renko Chart candlestick values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<RenkoResult> GetRenko<TQuote>(this IEnumerable<TQuote> quotes, decimal brickSize, EndType endType = EndType.Close) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcRenko(brickSize, endType);
	}

	internal static List<RenkoResult> CalcRenko<TQuote>(this List<TQuote> quotesList, decimal brickSize, EndType endType) where TQuote : IQuote
	{
		ValidateRenko(brickSize);
		int count = quotesList.Count;
		List<RenkoResult> list = new List<RenkoResult>(count);
		if (count == 0)
		{
			return list;
		}
		TQuote val = quotesList[0];
		bool flag = true;
		int decimalPlaces = brickSize.GetDecimalPlaces();
		checked
		{
			decimal num = Math.Round(val.Close, Math.Max(decimalPlaces - 1, 0));
			decimal num2 = decimal.MinValue;
			decimal num3 = decimal.MaxValue;
			decimal num4 = default(decimal);
			RenkoResult renkoResult = new RenkoResult(val.Date)
			{
				Open = num,
				Close = num
			};
			for (int i = 1; i < count; i++)
			{
				TQuote q = quotesList[i];
				if (flag)
				{
					num2 = q.High;
					num3 = q.Low;
					num4 = q.Volume;
				}
				else
				{
					num2 = ((q.High > num2) ? q.High : num2);
					num3 = ((q.Low < num3) ? q.Low : num3);
					num4 += q.Volume;
				}
				int newBricks = GetNewBricks(endType, q, renkoResult, brickSize);
				int num5 = Math.Abs(newBricks);
				for (int j = 0; j < num5; j++)
				{
					bool isUp = newBricks >= 0;
					decimal close;
					if (newBricks > 0)
					{
						num = Math.Max(renkoResult.Open, renkoResult.Close);
						close = num + brickSize;
					}
					else
					{
						num = Math.Min(renkoResult.Open, renkoResult.Close);
						close = num - brickSize;
					}
					RenkoResult renkoResult2 = new RenkoResult(q.Date)
					{
						Open = num,
						High = num2,
						Low = num3,
						Close = close,
						Volume = num4 / (decimal)num5,
						IsUp = isUp
					};
					list.Add(renkoResult2);
					renkoResult = renkoResult2;
				}
				flag = num5 != 0;
			}
			return list;
		}
	}

	private static int GetNewBricks<TQuote>(EndType endType, TQuote q, RenkoResult lastBrick, decimal brickSize) where TQuote : IQuote
	{
		decimal num = Math.Max(lastBrick.Open, lastBrick.Close);
		decimal num2 = Math.Min(lastBrick.Open, lastBrick.Close);
		switch (endType)
		{
		case EndType.Close:
			return (q.Close > num) ? ((int)((q.Close - num) / brickSize)) : ((q.Close < num2) ? ((int)((q.Close - num2) / brickSize)) : 0);
		case EndType.HighLow:
		{
			decimal num3 = (q.High - num) / brickSize;
			decimal num4 = (num2 - q.Low) / brickSize;
			return (int)((num3 >= num4) ? num3 : (-num4));
		}
		default:
			throw new ArgumentOutOfRangeException("endType");
		}
	}

	private static void ValidateRenko(decimal brickSize)
	{
		if (brickSize <= 0m)
		{
			throw new ArgumentOutOfRangeException("brickSize", brickSize, "Brick size must be greater than 0 for Renko Charts.");
		}
	}

	/// <summary>
	///       The ATR Renko Chart is a modified Japanese candlestick pattern based on Average True Range brick size.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Renko/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="atrPeriods">Lookback periods for the ATR evaluation.</param><param name="endType">End type.  See documentation.</param><returns>Time series of Renko Chart candlestick values.</returns>
	public static IEnumerable<RenkoResult> GetRenkoAtr<TQuote>(this IEnumerable<TQuote> quotes, int atrPeriods, EndType endType = EndType.Close) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcRenkoAtr(atrPeriods, endType);
	}

	internal static List<RenkoResult> CalcRenkoAtr<TQuote>(this List<TQuote> quotesList, int atrPeriods, EndType endType = EndType.Close) where TQuote : IQuote
	{
		double? num = quotesList.ToQuoteD().CalcAtr(atrPeriods).LastOrDefault()?.Atr;
		decimal num2 = ((!num.HasValue) ? 0m : ((decimal)num.Value));
		if (!(num2 == 0m))
		{
			return quotesList.CalcRenko(num2, endType);
		}
		return new List<RenkoResult>();
	}

	/// <summary>
	///       Rate of Change (ROC), also known as Momentum Oscillator, is the percent change of price over a lookback window.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Roc/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="smaPeriods">Optional.  Number of periods for an ROC SMA signal line.</param><returns>Time series of ROC values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<RocResult> GetRoc<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, int? smaPeriods = null) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcRoc(lookbackPeriods, smaPeriods);
	}

	public static IEnumerable<RocResult> GetRoc(this IEnumerable<IReusableResult> results, int lookbackPeriods, int? smaPeriods = null)
	{
		return results.ToTuple().CalcRoc(lookbackPeriods, smaPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<RocResult> GetRoc(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, int? smaPeriods = null)
	{
		return priceTuples.ToSortedList().CalcRoc(lookbackPeriods, smaPeriods);
	}

	internal static List<RocResult> CalcRoc(this List<(DateTime, double)> tpList, int lookbackPeriods, int? smaPeriods)
	{
		ValidateRoc(lookbackPeriods, smaPeriods);
		List<RocResult> list = new List<RocResult>(tpList.Count);
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				RocResult rocResult = new RocResult(item);
				list.Add(rocResult);
				if (i + 1 > lookbackPeriods)
				{
					double item3 = tpList[i - lookbackPeriods].Item2;
					rocResult.Momentum = (item2 - item3).NaN2Null();
					rocResult.Roc = ((item3 == 0.0) ? ((double?)null) : (100.0 * rocResult.Momentum / item3).NaN2Null());
				}
				if (smaPeriods.HasValue && i >= lookbackPeriods + smaPeriods - 1)
				{
					double? num = 0.0;
					for (int j = i + 1 - smaPeriods.Value; j <= i; j++)
					{
						num += list[j].Roc;
					}
					rocResult.RocSma = num / (double?)smaPeriods;
				}
			}
			return list;
		}
	}

	private static void ValidateRoc(int lookbackPeriods, int? smaPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for ROC.");
		}
		if (smaPeriods.HasValue && smaPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "SMA periods must be greater than 0 for ROC.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<RocResult> RemoveWarmupPeriods(this IEnumerable<RocResult> results)
	{
		int removePeriods = results.ToList().FindIndex((RocResult x) => x.Roc.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///       Rate of Change with Bands (ROCWB) is the percent change of price over a lookback window with standard deviation bands.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Roc/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="emaPeriods">Number of periods for the ROC EMA line.</param><param name="stdDevPeriods">Number of periods the standard deviation for upper/lower band lines.</param><returns>Time series of ROCWB values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<RocWbResult> GetRocWb<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, int emaPeriods, int stdDevPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcRocWb(lookbackPeriods, emaPeriods, stdDevPeriods);
	}

	public static IEnumerable<RocWbResult> GetRocWb(this IEnumerable<IReusableResult> results, int lookbackPeriods, int emaPeriods, int stdDevPeriods)
	{
		return results.ToTuple().CalcRocWb(lookbackPeriods, emaPeriods, stdDevPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<RocWbResult> GetRocWb(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, int emaPeriods, int stdDevPeriods)
	{
		return priceTuples.ToSortedList().CalcRocWb(lookbackPeriods, emaPeriods, stdDevPeriods);
	}

	internal static List<RocWbResult> CalcRocWb(this List<(DateTime, double)> tpList, int lookbackPeriods, int emaPeriods, int stdDevPeriods)
	{
		ValidateRocWb(lookbackPeriods, emaPeriods, stdDevPeriods);
		List<RocWbResult> list = (from x in tpList.CalcRoc(lookbackPeriods, null)
			select new RocWbResult(x.Date)
			{
				Roc = x.Roc
			}).ToList();
		checked
		{
			double num = 2.0 / (double)(emaPeriods + 1);
			double? num2 = 0.0;
			int count = list.Count;
			if (count > lookbackPeriods)
			{
				int num3 = Math.Min(lookbackPeriods + emaPeriods, count);
				for (int num4 = lookbackPeriods; num4 < num3; num4++)
				{
					num2 += list[num4].Roc;
				}
				num2 /= (double)emaPeriods;
			}
			double?[] array = list.Select((RocWbResult x) => x.Roc * x.Roc).ToArray();
			for (int num5 = lookbackPeriods; num5 < count; num5++)
			{
				RocWbResult rocWbResult = list[num5];
				if (num5 + 1 > lookbackPeriods + emaPeriods)
				{
					rocWbResult.RocEma = num2 + num * (rocWbResult.Roc - num2);
					num2 = rocWbResult.RocEma;
				}
				else if (num5 + 1 == lookbackPeriods + emaPeriods)
				{
					rocWbResult.RocEma = num2;
				}
				if (num5 + 1 >= lookbackPeriods + stdDevPeriods)
				{
					double? num6 = 0.0;
					for (int num7 = num5 - stdDevPeriods + 1; num7 <= num5; num7++)
					{
						num6 += array[num7];
					}
					if (num6.HasValue)
					{
						rocWbResult.LowerBand = 0.0 - (rocWbResult.UpperBand = Math.Sqrt(num6.Value / (double)stdDevPeriods));
					}
				}
			}
			return list;
		}
	}

	private static void ValidateRocWb(int lookbackPeriods, int emaPeriods, int stdDevPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for ROC with Bands.");
		}
		if (emaPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("emaPeriods", emaPeriods, "EMA periods must be greater than 0 for ROC.");
		}
		if (stdDevPeriods <= 0 || stdDevPeriods > lookbackPeriods)
		{
			throw new ArgumentOutOfRangeException("stdDevPeriods", stdDevPeriods, "Standard Deviation periods must be greater than 0 and less than lookback period for ROC with Bands.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<RocWbResult> RemoveWarmupPeriods(this IEnumerable<RocWbResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((RocWbResult x) => x.RocEma.HasValue) + 1;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///     Rolling Pivot Points is a modern update to traditional fixed calendar window Pivot Points.
	///     It depicts support and resistance levels, based on a defined rolling window and offset.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/RollingPivots/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="windowPeriods">Number of periods in the evaluation window.</param><param name="offsetPeriods">Number of periods to offset the window from the current period.</param><param name="pointType">Pivot Point type.</param><returns>Time series of Rolling Pivot Points values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<RollingPivotsResult> GetRollingPivots<TQuote>(this IEnumerable<TQuote> quotes, int windowPeriods, int offsetPeriods, PivotPointType pointType = PivotPointType.Standard) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcRollingPivots(windowPeriods, offsetPeriods, pointType);
	}

	internal static List<RollingPivotsResult> CalcRollingPivots<TQuote>(this List<TQuote> quotesList, int windowPeriods, int offsetPeriods, PivotPointType pointType) where TQuote : IQuote
	{
		ValidateRollingPivots(windowPeriods, offsetPeriods);
		int count = quotesList.Count;
		List<RollingPivotsResult> list = new List<RollingPivotsResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				TQuote val = quotesList[i];
				RollingPivotsResult rollingPivotsResult = new RollingPivotsResult
				{
					Date = val.Date
				};
				if (i >= windowPeriods + offsetPeriods)
				{
					int num = i - windowPeriods - offsetPeriods;
					TQuote val2 = quotesList[num];
					decimal num2 = val2.High;
					decimal num3 = val2.Low;
					decimal close = quotesList[i - offsetPeriods - 1].Close;
					for (int j = num; j <= i - offsetPeriods - 1; j++)
					{
						TQuote val3 = quotesList[j];
						num2 = ((val3.High > num2) ? val3.High : num2);
						num3 = ((val3.Low < num3) ? val3.Low : num3);
					}
					RollingPivotsResult pivotPoint = GetPivotPoint<RollingPivotsResult>(pointType, val.Open, num2, num3, close);
					rollingPivotsResult.PP = pivotPoint.PP;
					rollingPivotsResult.S1 = pivotPoint.S1;
					rollingPivotsResult.S2 = pivotPoint.S2;
					rollingPivotsResult.S3 = pivotPoint.S3;
					rollingPivotsResult.S4 = pivotPoint.S4;
					rollingPivotsResult.R1 = pivotPoint.R1;
					rollingPivotsResult.R2 = pivotPoint.R2;
					rollingPivotsResult.R3 = pivotPoint.R3;
					rollingPivotsResult.R4 = pivotPoint.R4;
				}
				list.Add(rollingPivotsResult);
			}
			return list;
		}
	}

	private static void ValidateRollingPivots(int windowPeriods, int offsetPeriods)
	{
		if (windowPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("windowPeriods", windowPeriods, "Window periods must be greater than 0 for Rolling Pivot Points.");
		}
		if (offsetPeriods < 0)
		{
			throw new ArgumentOutOfRangeException("offsetPeriods", offsetPeriods, "Offset periods must be greater than or equal to 0 for Rolling Pivot Points.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<RollingPivotsResult> RemoveWarmupPeriods(this IEnumerable<RollingPivotsResult> results)
	{
		int removePeriods = results.ToList().FindIndex((RollingPivotsResult x) => x.PP.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Relative Strength Index (RSI) measures strength of the winning/losing streak over N lookback periods
	///     on a scale of 0 to 100, to depict overbought and oversold conditions.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Rsi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of RSI values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<RsiResult> GetRsi<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcRsi(lookbackPeriods);
	}

	public static IEnumerable<RsiResult> GetRsi(this IEnumerable<IReusableResult> results, int lookbackPeriods = 14)
	{
		return results.ToTuple().CalcRsi(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<RsiResult> GetRsi(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods = 14)
	{
		return priceTuples.ToSortedList().CalcRsi(lookbackPeriods);
	}

	internal static List<RsiResult> CalcRsi(this List<(DateTime Date, double Value)> tpList, int lookbackPeriods)
	{
		ValidateRsi(lookbackPeriods);
		int count = tpList.Count;
		double num = 0.0;
		double num2 = 0.0;
		List<RsiResult> list = new List<RsiResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		if (count == 0)
		{
			return list;
		}
		double num3 = tpList[0].Value;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				(DateTime Date, double Value) tuple = tpList[i];
				DateTime item = tuple.Date;
				double item2 = tuple.Value;
				RsiResult rsiResult = new RsiResult(item);
				list.Add(rsiResult);
				array[i] = ((item2 > num3) ? (item2 - num3) : 0.0);
				array2[i] = ((item2 < num3) ? (num3 - item2) : 0.0);
				num3 = item2;
				if (i > lookbackPeriods)
				{
					num = (num * (double)(lookbackPeriods - 1) + array[i]) / (double)lookbackPeriods;
					num2 = (num2 * (double)(lookbackPeriods - 1) + array2[i]) / (double)lookbackPeriods;
					if (num2 > 0.0)
					{
						double num4 = num / num2;
						rsiResult.Rsi = 100.0 - 100.0 / (1.0 + num4);
					}
					else
					{
						rsiResult.Rsi = 100.0;
					}
				}
				else if (i == lookbackPeriods)
				{
					double num5 = 0.0;
					double num6 = 0.0;
					for (int j = 1; j <= lookbackPeriods; j++)
					{
						num5 += array[j];
						num6 += array2[j];
					}
					num = num5 / (double)lookbackPeriods;
					num2 = num6 / (double)lookbackPeriods;
					rsiResult.Rsi = ((num2 > 0.0) ? (100.0 - 100.0 / (1.0 + num / num2)) : 100.0);
				}
			}
			return list;
		}
	}

	private static void ValidateRsi(int lookbackPeriods)
	{
		if (lookbackPeriods < 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for RSI.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<RsiResult> RemoveWarmupPeriods(this IEnumerable<RsiResult> results)
	{
		int num = results.ToList().FindIndex((RsiResult x) => x.Rsi.HasValue);
		return results.Remove(checked(10 * num));
	}

	/// <summary>
	///     Slope of the best fit line is determined by an ordinary least-squares simple linear regression on price.
	///     It can be used to help identify trend strength and direction.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Slope/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Slope values, including Slope, Standard Deviation, R², and a best-fit Line (for the last lookback segment).</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SlopeResult> GetSlope<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcSlope(lookbackPeriods);
	}

	public static IEnumerable<SlopeResult> GetSlope(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcSlope(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<SlopeResult> GetSlope(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcSlope(lookbackPeriods);
	}

	internal static List<SlopeResult> CalcSlope(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateSlope(lookbackPeriods);
		int count = tpList.Count;
		List<SlopeResult> list = new List<SlopeResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				SlopeResult slopeResult = new SlopeResult(tpList[i].Item1);
				list.Add(slopeResult);
				if (i + 1 >= lookbackPeriods)
				{
					double num = 0.0;
					double num2 = 0.0;
					for (int j = i - lookbackPeriods + 1; j <= i; j++)
					{
						double item = tpList[j].Item2;
						num += (double)j + 1.0;
						num2 += item;
					}
					double num3 = num / (double)lookbackPeriods;
					double num4 = num2 / (double)lookbackPeriods;
					double num5 = 0.0;
					double num6 = 0.0;
					double num7 = 0.0;
					for (int k = i - lookbackPeriods + 1; k <= i; k++)
					{
						double item2 = tpList[k].Item2;
						double num8 = (double)k + 1.0 - num3;
						double num9 = item2 - num4;
						num5 += num8 * num8;
						num6 += num9 * num9;
						num7 += num8 * num9;
					}
					slopeResult.Slope = (num7 / num5).NaN2Null();
					slopeResult.Intercept = (num4 - slopeResult.Slope * num3).NaN2Null();
					double num10 = Math.Sqrt(num5 / (double)lookbackPeriods);
					double num11 = Math.Sqrt(num6 / (double)lookbackPeriods);
					slopeResult.StdDev = num11.NaN2Null();
					if (num10 * num11 != 0.0)
					{
						double num12 = num7 / (num10 * num11) / (double)lookbackPeriods;
						slopeResult.RSquared = (num12 * num12).NaN2Null();
					}
				}
			}
			if (count >= lookbackPeriods)
			{
				SlopeResult slopeResult2 = list.LastOrDefault();
				for (int l = count - lookbackPeriods; l < count; l++)
				{
					list[l].Line = (decimal?)(slopeResult2?.Slope * (double)(l + 1) + slopeResult2?.Intercept).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateSlope(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Slope/Linear Regression.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SlopeResult> RemoveWarmupPeriods(this IEnumerable<SlopeResult> results)
	{
		int removePeriods = results.ToList().FindIndex((SlopeResult x) => x.Slope.HasValue);
		return results.Remove(removePeriods);
	}

	internal static IEnumerable<SmaAnalysis> CalcSmaAnalysis(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		List<SmaAnalysis> list = (from x in tpList.CalcSma(lookbackPeriods)
			select new SmaAnalysis(x.Date)
			{
				Sma = x.Sma
			}).ToList();
		checked
		{
			for (int num = lookbackPeriods - 1; num < list.Count; num++)
			{
				SmaAnalysis smaAnalysis = list[num];
				double num2 = ((!smaAnalysis.Sma.HasValue) ? double.NaN : smaAnalysis.Sma.Value);
				double num3 = 0.0;
				double num4 = 0.0;
				double num5 = 0.0;
				for (int num6 = num + 1 - lookbackPeriods; num6 <= num; num6++)
				{
					double item = tpList[num6].Item2;
					num3 += Math.Abs(item - num2);
					num4 += (item - num2) * (item - num2);
					num5 += ((item == 0.0) ? double.NaN : (Math.Abs(item - num2) / item));
				}
				smaAnalysis.Mad = (num3 / (double)lookbackPeriods).NaN2Null();
				smaAnalysis.Mse = (num4 / (double)lookbackPeriods).NaN2Null();
				smaAnalysis.Mape = (num5 / (double)lookbackPeriods).NaN2Null();
			}
			return list;
		}
	}

	/// <summary>
	///       Simple Moving Average (SMA) of the price.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Sma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of SMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SmaResult> GetSma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcSma(lookbackPeriods);
	}

	public static IEnumerable<SmaResult> GetSma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcSma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<SmaResult> GetSma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcSma(lookbackPeriods);
	}

	/// <summary>
	///       Simple Moving Average (SMA) is the average of price over a lookback window.  This extended variant includes mean absolute deviation (MAD), mean square error (MSE), and mean absolute percentage error (MAPE).
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Sma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of SMA, MAD, MSE, and MAPE values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SmaAnalysis> GetSmaAnalysis<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcSmaAnalysis(lookbackPeriods);
	}

	public static IEnumerable<SmaAnalysis> GetSmaAnalysis(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcSmaAnalysis(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<SmaAnalysis> GetSmaAnalysis(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcSmaAnalysis(lookbackPeriods);
	}

	internal static List<SmaResult> CalcSma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateSma(lookbackPeriods);
		List<SmaResult> list = new List<SmaResult>(tpList.Count);
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				SmaResult smaResult = new SmaResult(tpList[i].Item1);
				list.Add(smaResult);
				if (i + 1 >= lookbackPeriods)
				{
					double num = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						double item = tpList[j].Item2;
						num += item;
					}
					smaResult.Sma = (num / (double)lookbackPeriods).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateSma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for SMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SmaResult> RemoveWarmupPeriods(this IEnumerable<SmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((SmaResult x) => x.Sma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SmaAnalysis> RemoveWarmupPeriods(this IEnumerable<SmaAnalysis> results)
	{
		int removePeriods = results.ToList().FindIndex((SmaAnalysis x) => x.Sma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///       Stochastic Momentum Index is a double-smoothed variant of the Stochastic Oscillator on a scale from -100 to 100.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Smi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the Stochastic lookback.</param><param name="firstSmoothPeriods">Number of periods in the first smoothing.</param><param name="secondSmoothPeriods">Number of periods in the second smoothing.</param><param name="signalPeriods">Number of periods in the EMA of SMI.</param><returns>Time series of Stochastic Momentum Index values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SmiResult> GetSmi<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 13, int firstSmoothPeriods = 25, int secondSmoothPeriods = 2, int signalPeriods = 3) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcSmi(lookbackPeriods, firstSmoothPeriods, secondSmoothPeriods, signalPeriods);
	}

	internal static List<SmiResult> CalcSmi(this List<QuoteD> qdList, int lookbackPeriods, int firstSmoothPeriods, int secondSmoothPeriods, int signalPeriods)
	{
		ValidateSmi(lookbackPeriods, firstSmoothPeriods, secondSmoothPeriods, signalPeriods);
		int count = qdList.Count;
		List<SmiResult> list = new List<SmiResult>(count);
		checked
		{
			double num = 2.0 / (double)(firstSmoothPeriods + 1);
			double num2 = 2.0 / (double)(secondSmoothPeriods + 1);
			double num3 = 2.0 / (double)(signalPeriods + 1);
			double num4 = 0.0;
			double num5 = 0.0;
			double num6 = 0.0;
			double num7 = 0.0;
			double num8 = 0.0;
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				SmiResult smiResult = new SmiResult(quoteD.Date);
				list.Add(smiResult);
				if (i + 1 < lookbackPeriods)
				{
					continue;
				}
				double num9 = double.MinValue;
				double num10 = double.MaxValue;
				for (int j = i + 1 - lookbackPeriods; j <= i; j++)
				{
					QuoteD quoteD2 = qdList[j];
					if (quoteD2.High > num9)
					{
						num9 = quoteD2.High;
					}
					if (quoteD2.Low < num10)
					{
						num10 = quoteD2.Low;
					}
				}
				double num11 = quoteD.Close - 0.5 * (num9 + num10);
				double num12 = num9 - num10;
				if (i + 1 == lookbackPeriods)
				{
					num4 = num11;
					num5 = num4;
					num6 = num12;
					num7 = num6;
				}
				double num13 = num4 + num * (num11 - num4);
				double num14 = num6 + num * (num12 - num6);
				double num15 = num5 + num2 * (num13 - num5);
				double num16 = num7 + num2 * (num14 - num7);
				double num17 = 100.0 * (num15 / (0.5 * num16));
				smiResult.Smi = num17;
				if (i + 1 == lookbackPeriods)
				{
					num8 = num17;
				}
				double num18 = num8 + num3 * (num17 - num8);
				smiResult.Signal = num18;
				num4 = num13;
				num5 = num15;
				num6 = num14;
				num7 = num16;
				num8 = num18;
			}
			return list;
		}
	}

	private static void ValidateSmi(int lookbackPeriods, int firstSmoothPeriods, int secondSmoothPeriods, int signalPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for SMI.");
		}
		if (firstSmoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("firstSmoothPeriods", firstSmoothPeriods, "Smoothing periods must be greater than 0 for SMI.");
		}
		if (secondSmoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("secondSmoothPeriods", secondSmoothPeriods, "Smoothing periods must be greater than 0 for SMI.");
		}
		if (signalPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than 0 for SMI.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SmiResult> RemoveWarmupPeriods(this IEnumerable<SmiResult> results)
	{
		int num = results.ToList().FindIndex((SmiResult x) => x.Smi.HasValue);
		return results.Remove(checked(num + 2 + 100));
	}

	/// <summary>
	///       Smoothed Moving Average (SMMA) is the average of price over a lookback window using a smoothing method.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Smma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of SMMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SmmaResult> GetSmma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcSmma(lookbackPeriods);
	}

	public static IEnumerable<SmmaResult> GetSmma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcSmma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<SmmaResult> GetSmma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcSmma(lookbackPeriods);
	}

	internal static List<SmmaResult> CalcSmma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateSmma(lookbackPeriods);
		int count = tpList.Count;
		List<SmmaResult> list = new List<SmmaResult>(count);
		double num = double.NaN;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				double num2 = double.NaN;
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				SmmaResult smmaResult = new SmmaResult(item);
				list.Add(smmaResult);
				if (i + 1 > lookbackPeriods)
				{
					num2 = (num * (double)(lookbackPeriods - 1) + item2) / (double)lookbackPeriods;
					smmaResult.Smma = num2.NaN2Null();
				}
				else if (i + 1 == lookbackPeriods)
				{
					double num3 = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						double item3 = tpList[j].Item2;
						num3 += item3;
					}
					num2 = num3 / (double)lookbackPeriods;
					smmaResult.Smma = num2.NaN2Null();
				}
				num = num2;
			}
			return list;
		}
	}

	private static void ValidateSmma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for SMMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SmmaResult> RemoveWarmupPeriods(this IEnumerable<SmmaResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((SmmaResult x) => x.Smma.HasValue) + 1;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///     Stoller Average Range Channel (STARC) Bands, are based on an SMA centerline and ATR band widths.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/StarcBands/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="smaPeriods">Number of periods for the centerline SMA.</param><param name="multiplier">ATR multiplier sets the width of the channel.</param><param name="atrPeriods">Number of periods in the ATR evaluation.</param><returns>Time series of STARC Bands values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StarcBandsResult> GetStarcBands<TQuote>(this IEnumerable<TQuote> quotes, int smaPeriods, double multiplier = 2.0, int atrPeriods = 10) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcStarcBands(smaPeriods, multiplier, atrPeriods);
	}

	internal static List<StarcBandsResult> CalcStarcBands(this List<QuoteD> qdList, int smaPeriods, double multiplier, int atrPeriods)
	{
		ValidateStarcBands(smaPeriods, multiplier, atrPeriods);
		List<AtrResult> list = qdList.CalcAtr(atrPeriods);
		List<StarcBandsResult> list2 = (from x in qdList.ToTuple(CandlePart.Close).CalcSma(smaPeriods)
			select new StarcBandsResult(x.Date)
			{
				Centerline = x.Sma
			}).ToList();
		checked
		{
			for (int num = Math.Max(smaPeriods, atrPeriods) - 1; num < list2.Count; num++)
			{
				StarcBandsResult starcBandsResult = list2[num];
				AtrResult atrResult = list[num];
				starcBandsResult.UpperBand = starcBandsResult.Centerline + multiplier * atrResult.Atr;
				starcBandsResult.LowerBand = starcBandsResult.Centerline - multiplier * atrResult.Atr;
			}
			return list2;
		}
	}

	private static void ValidateStarcBands(int smaPeriods, double multiplier, int atrPeriods)
	{
		if (smaPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "EMA periods must be greater than 1 for STARC Bands.");
		}
		if (atrPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("atrPeriods", atrPeriods, "ATR periods must be greater than 1 for STARC Bands.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "Multiplier must be greater than 0 for STARC Bands.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<StarcBandsResult> Condense(this IEnumerable<StarcBandsResult> results)
	{
		List<StarcBandsResult> list = results.ToList();
		list.RemoveAll((StarcBandsResult x) => !x.UpperBand.HasValue && !x.LowerBand.HasValue && !x.Centerline.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StarcBandsResult> RemoveWarmupPeriods(this IEnumerable<StarcBandsResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((StarcBandsResult x) => x.UpperBand.HasValue || x.LowerBand.HasValue) + 1;
			return results.Remove(num + 150);
		}
	}

	/// <summary>
	///     Schaff Trend Cycle is a stochastic oscillator view of two converging/diverging exponential moving averages.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Stc/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="cyclePeriods">Number of periods for the Trend Cycle.</param><param name="fastPeriods">Number of periods in the Fast EMA.</param><param name="slowPeriods">Number of periods in the Slow EMA.</param><returns>Time series of MACD values, including MACD, Signal, and Histogram.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StcResult> GetStc<TQuote>(this IEnumerable<TQuote> quotes, int cyclePeriods = 10, int fastPeriods = 23, int slowPeriods = 50) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcStc(cyclePeriods, fastPeriods, slowPeriods);
	}

	public static IEnumerable<StcResult> GetStc(this IEnumerable<IReusableResult> results, int cyclePeriods = 10, int fastPeriods = 23, int slowPeriods = 50)
	{
		return results.ToTuple().CalcStc(cyclePeriods, fastPeriods, slowPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<StcResult> GetStc(this IEnumerable<(DateTime, double)> priceTuples, int cyclePeriods = 10, int fastPeriods = 23, int slowPeriods = 50)
	{
		return priceTuples.ToSortedList().CalcStc(cyclePeriods, fastPeriods, slowPeriods);
	}

	internal static List<StcResult> CalcStc(this List<(DateTime, double)> tpList, int cyclePeriods, int fastPeriods, int slowPeriods)
	{
		ValidateStc(cyclePeriods, fastPeriods, slowPeriods);
		int count = tpList.Count;
		checked
		{
			int num = Math.Min(slowPeriods - 1, count);
			List<StcResult> list = new List<StcResult>(count);
			for (int i = 0; i < num; i++)
			{
				DateTime item = tpList[i].Item1;
				list.Add(new StcResult(item));
			}
			List<StochResult> list2 = (from x in tpList.CalcMacd(fastPeriods, slowPeriods, 1).Remove(num)
				select new QuoteD
				{
					Date = x.Date,
					High = x.Macd.Null2NaN(),
					Low = x.Macd.Null2NaN(),
					Close = x.Macd.Null2NaN()
				}).ToList().CalcStoch(cyclePeriods, 1, 3, 3.0, 2.0, MaType.SMA);
			for (int num2 = 0; num2 < list2.Count; num2++)
			{
				StochResult stochResult = list2[num2];
				list.Add(new StcResult(stochResult.Date)
				{
					Stc = stochResult.Oscillator
				});
			}
			return list;
		}
	}

	private static void ValidateStc(int cyclePeriods, int fastPeriods, int slowPeriods)
	{
		if (cyclePeriods < 0)
		{
			throw new ArgumentOutOfRangeException("cyclePeriods", cyclePeriods, "Trend Cycle periods must be greater than or equal to 0 for STC.");
		}
		if (fastPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("fastPeriods", fastPeriods, "Fast periods must be greater than 0 for STC.");
		}
		if (slowPeriods <= fastPeriods)
		{
			throw new ArgumentOutOfRangeException("slowPeriods", slowPeriods, "Slow periods must be greater than the fast period for STC.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StcResult> RemoveWarmupPeriods(this IEnumerable<StcResult> results)
	{
		int num = results.ToList().FindIndex((StcResult x) => x.Stc.HasValue);
		return results.Remove(checked(num + 250));
	}

	/// <summary>
	///     Rolling Standard Deviation of price over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/StdDev/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="smaPeriods">Optional.  Number of periods in the Standard Deviation SMA signal line.</param><returns>Time series of Standard Deviations values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StdDevResult> GetStdDev<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, int? smaPeriods = null) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcStdDev(lookbackPeriods, smaPeriods);
	}

	public static IEnumerable<StdDevResult> GetStdDev(this IEnumerable<IReusableResult> results, int lookbackPeriods, int? smaPeriods = null)
	{
		return results.ToTuple().CalcStdDev(lookbackPeriods, smaPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<StdDevResult> GetStdDev(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, int? smaPeriods = null)
	{
		return priceTuples.ToSortedList().CalcStdDev(lookbackPeriods, smaPeriods);
	}

	internal static List<StdDevResult> CalcStdDev(this List<(DateTime, double)> tpList, int lookbackPeriods, int? smaPeriods)
	{
		ValidateStdDev(lookbackPeriods, smaPeriods);
		int count = tpList.Count;
		List<StdDevResult> list = new List<StdDevResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				StdDevResult stdDevResult = new StdDevResult(item);
				list.Add(stdDevResult);
				if (i + 1 >= lookbackPeriods)
				{
					double[] array = new double[lookbackPeriods];
					double num = 0.0;
					int num2 = 0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						num += (array[num2] = tpList[j].Item2);
						num2++;
					}
					double num3 = num / (double)lookbackPeriods;
					stdDevResult.StdDev = array.StdDev().NaN2Null();
					stdDevResult.Mean = num3.NaN2Null();
					stdDevResult.ZScore = ((stdDevResult.StdDev == 0.0) ? ((double?)null) : ((item2 - num3) / stdDevResult.StdDev));
				}
				if (smaPeriods.HasValue && i >= lookbackPeriods + smaPeriods - 2)
				{
					double? num4 = 0.0;
					for (int k = i + 1 - smaPeriods.Value; k <= i; k++)
					{
						num4 += list[k].StdDev;
					}
					stdDevResult.StdDevSma = (num4 / (double?)smaPeriods).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateStdDev(int lookbackPeriods, int? smaPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Standard Deviation.");
		}
		if (smaPeriods.HasValue && smaPeriods.GetValueOrDefault() <= 0)
		{
			throw new ArgumentOutOfRangeException("smaPeriods", smaPeriods, "SMA periods must be greater than 0 for Standard Deviation.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StdDevResult> RemoveWarmupPeriods(this IEnumerable<StdDevResult> results)
	{
		int removePeriods = results.ToList().FindIndex((StdDevResult x) => x.StdDev.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Standard Deviation Channels are based on an linear regression centerline and standard deviations band widths.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/StdDevChannels/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Size of the evaluation window.</param><param name="stdDeviations">Width of bands. Number of Standard Deviations from the regression line.</param><returns>Time series of Standard Deviation Channels values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StdDevChannelsResult> GetStdDevChannels<TQuote>(this IEnumerable<TQuote> quotes, int? lookbackPeriods = 20, double stdDeviations = 2.0) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcStdDevChannels(lookbackPeriods, stdDeviations);
	}

	public static IEnumerable<StdDevChannelsResult> GetStdDevChannels(this IEnumerable<IReusableResult> results, int? lookbackPeriods = 20, double stdDeviations = 2.0)
	{
		return results.ToTuple().CalcStdDevChannels(lookbackPeriods, stdDeviations).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<StdDevChannelsResult> GetStdDevChannels(this IEnumerable<(DateTime, double)> priceTuples, int? lookbackPeriods = 20, double stdDeviations = 2.0)
	{
		return priceTuples.ToSortedList().CalcStdDevChannels(lookbackPeriods, stdDeviations);
	}

	internal static List<StdDevChannelsResult> CalcStdDevChannels(this List<(DateTime, double)> tpList, int? lookbackPeriods, double stdDeviations)
	{
		int valueOrDefault = lookbackPeriods.GetValueOrDefault();
		if (!lookbackPeriods.HasValue)
		{
			valueOrDefault = tpList.Count;
			lookbackPeriods = valueOrDefault;
		}
		ValidateStdDevChannels(lookbackPeriods, stdDeviations);
		List<SlopeResult> list = tpList.CalcSlope(lookbackPeriods.Value);
		int count = list.Count;
		List<StdDevChannelsResult> list2 = list.Select((SlopeResult x) => new StdDevChannelsResult(x.Date)).ToList();
		checked
		{
			for (int num = count - 1; num >= lookbackPeriods - 1; num -= lookbackPeriods.Value)
			{
				SlopeResult slopeResult = list[num];
				double? num2 = stdDeviations * slopeResult.StdDev;
				for (int num3 = num - lookbackPeriods.Value + 1; num3 <= num; num3++)
				{
					if (num3 >= 0)
					{
						StdDevChannelsResult stdDevChannelsResult = list2[num3];
						stdDevChannelsResult.Centerline = slopeResult.Slope * (double)(num3 + 1) + slopeResult.Intercept;
						stdDevChannelsResult.UpperChannel = stdDevChannelsResult.Centerline + num2;
						stdDevChannelsResult.LowerChannel = stdDevChannelsResult.Centerline - num2;
						stdDevChannelsResult.BreakPoint = num3 == num - lookbackPeriods + 1;
					}
				}
			}
			return list2;
		}
	}

	private static void ValidateStdDevChannels(int? lookbackPeriods, double stdDeviations)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Standard Deviation Channels.");
		}
		if (stdDeviations <= 0.0)
		{
			throw new ArgumentOutOfRangeException("stdDeviations", stdDeviations, "Standard Deviations must be greater than 0 for Standard Deviation Channels.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<StdDevChannelsResult> Condense(this IEnumerable<StdDevChannelsResult> results)
	{
		List<StdDevChannelsResult> list = results.ToList();
		list.RemoveAll((StdDevChannelsResult x) => !x.UpperChannel.HasValue && !x.LowerChannel.HasValue && !x.Centerline.HasValue && !x.BreakPoint);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StdDevChannelsResult> RemoveWarmupPeriods(this IEnumerable<StdDevChannelsResult> results)
	{
		int removePeriods = results.ToList().FindIndex((StdDevChannelsResult x) => x.UpperChannel.HasValue || x.LowerChannel.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///       Stochastic Oscillator is a momentum indicator that looks back N periods to produce a scale of 0 to 100.
	///       %J is also included for the KDJ Index extension.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Stoch/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the Oscillator.</param><param name="signalPeriods">Smoothing period for the %D signal line.</param><param name="smoothPeriods">Smoothing period for the %K Oscillator.  Use 3 for Slow or 1 for Fast.</param><returns>Time series of Stochastic Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StochResult> GetStoch<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14, int signalPeriods = 3, int smoothPeriods = 3) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcStoch(lookbackPeriods, signalPeriods, smoothPeriods, 3.0, 2.0, MaType.SMA);
	}

	/// <summary>
	///       Stochastic Oscillator is a momentum indicator that looks back N periods to produce a scale of 0 to 100.
	///       %J is also included for the KDJ Index extension.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/Stoch/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the Oscillator.</param><param name="signalPeriods">Smoothing period for the %D signal line.</param><param name="smoothPeriods">Smoothing period for the %K Oscillator.  Use 3 for Slow or 1 for Fast.</param><param name="kFactor">Weight of %K in the %J calculation.  Default is 3.</param><param name="dFactor">Weight of %K in the %J calculation.  Default is 2.</param><param name="movingAverageType">Type of moving average to use.  Default is MaType.SMA.  See docs for instructions and options.</param><returns>Time series of Stochastic Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StochResult> GetStoch<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, int signalPeriods, int smoothPeriods, double kFactor, double dFactor, MaType movingAverageType) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcStoch(lookbackPeriods, signalPeriods, smoothPeriods, kFactor, dFactor, movingAverageType);
	}

	internal static List<StochResult> CalcStoch(this List<QuoteD> qdList, int lookbackPeriods, int signalPeriods, int smoothPeriods, double kFactor, double dFactor, MaType movingAverageType)
	{
		ValidateStoch(lookbackPeriods, signalPeriods, smoothPeriods, kFactor, dFactor, movingAverageType);
		int count = qdList.Count;
		List<StochResult> list = new List<StochResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				StochResult stochResult = new StochResult(quoteD.Date);
				list.Add(stochResult);
				if (i + 1 < lookbackPeriods)
				{
					continue;
				}
				double num = double.MinValue;
				double num2 = double.MaxValue;
				for (int j = i + 1 - lookbackPeriods; j <= i; j++)
				{
					QuoteD quoteD2 = qdList[j];
					if (quoteD2.High > num)
					{
						num = quoteD2.High;
					}
					if (quoteD2.Low < num2)
					{
						num2 = quoteD2.Low;
					}
				}
				stochResult.Oscillator = ((num2 != num) ? (100.0 * (quoteD.Close - num2) / (num - num2)) : 0.0);
				stochResult.Oscillator = stochResult.Oscillator.NaN2Null();
			}
			if (smoothPeriods > 1)
			{
				list = SmoothOscillator(list, count, lookbackPeriods, smoothPeriods, movingAverageType);
			}
			if (count < lookbackPeriods - 1)
			{
				return list;
			}
			int num3 = lookbackPeriods + smoothPeriods + signalPeriods - 2;
			double? num4 = null;
			for (int k = lookbackPeriods - 1; k < count; k++)
			{
				StochResult stochResult2 = list[k];
				if (signalPeriods <= 1)
				{
					stochResult2.Signal = stochResult2.Oscillator;
				}
				else if (k + 1 >= num3 && movingAverageType == MaType.SMA)
				{
					double? num5 = 0.0;
					for (int l = k + 1 - signalPeriods; l <= k; l++)
					{
						num5 += list[l].Oscillator;
					}
					stochResult2.Signal = num5 / (double)signalPeriods;
				}
				else if (k >= lookbackPeriods - 1 && movingAverageType == MaType.SMMA)
				{
					double? num6 = num4;
					if (!num6.HasValue)
					{
						num4 = list[k].Oscillator;
					}
					num4 = (stochResult2.Signal = (num4 * (double)(signalPeriods - 1) + list[k].Oscillator) / (double)signalPeriods);
				}
				stochResult2.PercentJ = kFactor * stochResult2.Oscillator - dFactor * stochResult2.Signal;
			}
			return list;
		}
	}

	private static List<StochResult> SmoothOscillator(List<StochResult> results, int length, int lookbackPeriods, int smoothPeriods, MaType movingAverageType)
	{
		double?[] array = new double?[length];
		checked
		{
			switch (movingAverageType)
			{
			case MaType.SMA:
			{
				for (int j = lookbackPeriods + smoothPeriods - 2; j < length; j++)
				{
					double? num3 = 0.0;
					for (int k = j + 1 - smoothPeriods; k <= j; k++)
					{
						num3 += results[k].Oscillator;
					}
					array[j] = num3 / (double)smoothPeriods;
				}
				break;
			}
			case MaType.SMMA:
			{
				double? num = results[lookbackPeriods - 1].Oscillator;
				for (int i = lookbackPeriods - 1; i < length; i++)
				{
					double? num2 = num;
					if (!num2.HasValue)
					{
						num = results[i].Oscillator;
					}
					num = (array[i] = (num * (double)(smoothPeriods - 1) + results[i].Oscillator) / (double)smoothPeriods);
				}
				break;
			}
			}
			for (int l = 0; l < length; l++)
			{
				results[l].Oscillator = array[l];
			}
			return results;
		}
	}

	private static void ValidateStoch(int lookbackPeriods, int signalPeriods, int smoothPeriods, double kFactor, double dFactor, MaType movingAverageType)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Stochastic.");
		}
		if (signalPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than 0 for Stochastic.");
		}
		if (smoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("smoothPeriods", smoothPeriods, "Smooth periods must be greater than 0 for Stochastic.");
		}
		if (kFactor <= 0.0)
		{
			throw new ArgumentOutOfRangeException("kFactor", kFactor, "kFactor must be greater than 0 for Stochastic.");
		}
		if (dFactor <= 0.0)
		{
			throw new ArgumentOutOfRangeException("dFactor", dFactor, "dFactor must be greater than 0 for Stochastic.");
		}
		if (movingAverageType != MaType.SMA && movingAverageType != MaType.SMMA)
		{
			throw new ArgumentOutOfRangeException("dFactor", dFactor, "Stochastic only supports SMA and SMMA moving average types.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StochResult> RemoveWarmupPeriods(this IEnumerable<StochResult> results)
	{
		int removePeriods = results.ToList().FindIndex((StochResult x) => x.Oscillator.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Stochastic RSI is a Stochastic interpretation of the Relative Strength Index.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/StochRsi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="rsiPeriods">Number of periods for the RSI.</param><param name="stochPeriods">Number of periods for the Stochastic.</param><param name="signalPeriods">Number of periods for the Stochastic RSI SMA signal line.</param><param name="smoothPeriods">Number of periods for Stochastic Smoothing.  Use 1 for Fast or 3 for Slow.</param><returns>Time series of Stochastic RSI values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<StochRsiResult> GetStochRsi<TQuote>(this IEnumerable<TQuote> quotes, int rsiPeriods, int stochPeriods, int signalPeriods, int smoothPeriods = 1) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcStochRsi(rsiPeriods, stochPeriods, signalPeriods, smoothPeriods);
	}

	public static IEnumerable<StochRsiResult> GetStochRsi(this IEnumerable<IReusableResult> results, int rsiPeriods, int stochPeriods, int signalPeriods, int smoothPeriods)
	{
		return results.ToTuple().CalcStochRsi(rsiPeriods, stochPeriods, signalPeriods, smoothPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<StochRsiResult> GetStochRsi(this IEnumerable<(DateTime, double)> priceTuples, int rsiPeriods, int stochPeriods, int signalPeriods, int smoothPeriods)
	{
		return priceTuples.ToSortedList().CalcStochRsi(rsiPeriods, stochPeriods, signalPeriods, smoothPeriods);
	}

	internal static List<StochRsiResult> CalcStochRsi(this List<(DateTime, double)> tpList, int rsiPeriods, int stochPeriods, int signalPeriods, int smoothPeriods)
	{
		ValidateStochRsi(rsiPeriods, stochPeriods, signalPeriods, smoothPeriods);
		int count = tpList.Count;
		checked
		{
			int num = Math.Min(rsiPeriods + stochPeriods - 1, count);
			List<StochRsiResult> list = new List<StochRsiResult>(count);
			for (int i = 0; i < num; i++)
			{
				DateTime item = tpList[i].Item1;
				list.Add(new StochRsiResult(item));
			}
			List<StochResult> list2 = (from x in tpList.CalcRsi(rsiPeriods).Remove(Math.Min(rsiPeriods, count))
				select new QuoteD
				{
					Date = x.Date,
					High = x.Rsi.Null2NaN(),
					Low = x.Rsi.Null2NaN(),
					Close = x.Rsi.Null2NaN()
				}).ToList().CalcStoch(stochPeriods, signalPeriods, smoothPeriods, 3.0, 2.0, MaType.SMA).ToList();
			for (int num2 = rsiPeriods + stochPeriods - 1; num2 < count; num2++)
			{
				StochResult stochResult = list2[num2 - rsiPeriods];
				list.Add(new StochRsiResult(stochResult.Date)
				{
					StochRsi = stochResult.Oscillator,
					Signal = stochResult.Signal
				});
			}
			return list;
		}
	}

	private static void ValidateStochRsi(int rsiPeriods, int stochPeriods, int signalPeriods, int smoothPeriods)
	{
		if (rsiPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("rsiPeriods", rsiPeriods, "RSI periods must be greater than 0 for Stochastic RSI.");
		}
		if (stochPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("stochPeriods", stochPeriods, "STOCH periods must be greater than 0 for Stochastic RSI.");
		}
		if (signalPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than 0 for Stochastic RSI.");
		}
		if (smoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("smoothPeriods", smoothPeriods, "Smooth periods must be greater than 0 for Stochastic RSI.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<StochRsiResult> RemoveWarmupPeriods(this IEnumerable<StochRsiResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((StochRsiResult x) => x.StochRsi.HasValue) + 2;
			return results.Remove(num + 100);
		}
	}

	/// <summary>
	///     SuperTrend attempts to determine the primary trend of prices by using
	///     Average True Range (ATR) band thresholds around an HL2 midline. It can indicate a buy/sell signal or a
	///     trailing stop when the trend changes.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/SuperTrend/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for ATR.</param><param name="multiplier">Multiplier sets the ATR band width.</param><returns>Time series of SuperTrend values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<SuperTrendResult> GetSuperTrend<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 10, double multiplier = 3.0) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcSuperTrend(lookbackPeriods, multiplier);
	}

	internal static List<SuperTrendResult> CalcSuperTrend(this List<QuoteD> qdList, int lookbackPeriods, double multiplier)
	{
		ValidateSuperTrend(lookbackPeriods, multiplier);
		List<SuperTrendResult> list = new List<SuperTrendResult>(qdList.Count);
		List<AtrResult> list2 = qdList.CalcAtr(lookbackPeriods);
		bool flag = true;
		double? num = null;
		double? num2 = null;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				SuperTrendResult superTrendResult = new SuperTrendResult(quoteD.Date);
				list.Add(superTrendResult);
				if (i >= lookbackPeriods)
				{
					double? num3 = (quoteD.High + quoteD.Low) / 2.0;
					double? atr = list2[i].Atr;
					double? num4 = qdList[i - 1].Close;
					double? num5 = num3 + multiplier * atr;
					double? num6 = num3 - multiplier * atr;
					if (i == lookbackPeriods)
					{
						flag = quoteD.Close >= num3;
						num = num5;
						num2 = num6;
					}
					if (num5 < num || num4 > num)
					{
						num = num5;
					}
					if (num6 > num2 || num4 < num2)
					{
						num2 = num6;
					}
					if (quoteD.Close <= (flag ? num2 : num))
					{
						superTrendResult.SuperTrend = (decimal?)num;
						superTrendResult.UpperBand = (decimal?)num;
						flag = false;
					}
					else
					{
						superTrendResult.SuperTrend = (decimal?)num2;
						superTrendResult.LowerBand = (decimal?)num2;
						flag = true;
					}
				}
			}
			return list;
		}
	}

	private static void ValidateSuperTrend(int lookbackPeriods, double multiplier)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for SuperTrend.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "Multiplier must be greater than 0 for SuperTrend.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<SuperTrendResult> Condense(this IEnumerable<SuperTrendResult> results)
	{
		List<SuperTrendResult> list = results.ToList();
		list.RemoveAll((SuperTrendResult x) => !x.SuperTrend.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<SuperTrendResult> RemoveWarmupPeriods(this IEnumerable<SuperTrendResult> results)
	{
		int removePeriods = results.ToList().FindIndex((SuperTrendResult x) => x.SuperTrend.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Tillson T3 is a smooth moving average that reduces both lag and overshooting.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/T3/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the EMA smoothing.</param><param name="volumeFactor">Size of the Volume Factor.</param><returns>Time series of T3 values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<T3Result> GetT3<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 5, double volumeFactor = 0.7) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcT3(lookbackPeriods, volumeFactor);
	}

	public static IEnumerable<T3Result> GetT3(this IEnumerable<IReusableResult> results, int lookbackPeriods = 5, double volumeFactor = 0.7)
	{
		return results.ToTuple().CalcT3(lookbackPeriods, volumeFactor).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<T3Result> GetT3(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods = 5, double volumeFactor = 0.7)
	{
		return priceTuples.ToSortedList().CalcT3(lookbackPeriods, volumeFactor);
	}

	internal static List<T3Result> CalcT3(this List<(DateTime, double)> tpList, int lookbackPeriods, double volumeFactor)
	{
		ValidateT3(lookbackPeriods, volumeFactor);
		int count = tpList.Count;
		List<T3Result> list = new List<T3Result>(count);
		if (count == 0)
		{
			return list;
		}
		checked
		{
			double num = 2.0 / (double)(lookbackPeriods + 1);
			double num2 = (0.0 - volumeFactor) * volumeFactor * volumeFactor;
			double num3 = 3.0 * volumeFactor * volumeFactor + 3.0 * volumeFactor * volumeFactor * volumeFactor;
			double num4 = -6.0 * volumeFactor * volumeFactor - 3.0 * volumeFactor - 3.0 * volumeFactor * volumeFactor * volumeFactor;
			double num5 = 1.0 + 3.0 * volumeFactor + volumeFactor * volumeFactor * volumeFactor + 3.0 * volumeFactor * volumeFactor;
			(DateTime, double) tuple = tpList[0];
			double? num6 = tuple.Item2;
			double? num8;
			double? num9;
			double? num10;
			double? num11;
			double? num7 = (num8 = (num9 = (num10 = (num11 = num6))));
			list.Add(new T3Result(tuple.Item1)
			{
				T3 = tuple.Item2
			});
			for (int i = 1; i < count; i++)
			{
				(DateTime, double) tuple2 = tpList[i];
				DateTime item = tuple2.Item1;
				double item2 = tuple2.Item2;
				T3Result t3Result = new T3Result(item);
				list.Add(t3Result);
				num7 += num * (item2 - num7);
				num8 += num * (num7 - num8);
				num9 += num * (num8 - num9);
				num10 += num * (num9 - num10);
				num11 += num * (num10 - num11);
				num6 += num * (num11 - num6);
				t3Result.T3 = (num2 * num6 + num3 * num11 + num4 * num10 + num5 * num9).NaN2Null();
			}
			return list;
		}
	}

	private static void ValidateT3(int lookbackPeriods, double volumeFactor)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for T3.");
		}
		if (volumeFactor <= 0.0)
		{
			throw new ArgumentOutOfRangeException("volumeFactor", volumeFactor, "Volume Factor must be greater than 0 for T3.");
		}
	}

	/// <summary>
	///     Triple Exponential Moving Average (TEMA) of the price.  Note: TEMA is often confused with the alternative TRIX oscillator.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Tema/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Triple EMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<TemaResult> GetTema<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcTema(lookbackPeriods);
	}

	public static IEnumerable<TemaResult> GetTema(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcTema(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<TemaResult> GetTema(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcTema(lookbackPeriods);
	}

	internal static List<TemaResult> CalcTema(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateTema(lookbackPeriods);
		int count = tpList.Count;
		List<TemaResult> list = new List<TemaResult>(count);
		checked
		{
			double num = 2.0 / (double)(lookbackPeriods + 1);
			double? num2 = 0.0;
			int num3 = Math.Min(lookbackPeriods, count);
			for (int i = 0; i < num3; i++)
			{
				num2 += tpList[i].Item2;
			}
			num2 /= (double)lookbackPeriods;
			double? num5;
			double? num4 = (num5 = num2);
			for (int j = 0; j < count; j++)
			{
				(DateTime, double) tuple = tpList[j];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				TemaResult temaResult = new TemaResult(item);
				list.Add(temaResult);
				if (j > lookbackPeriods - 1)
				{
					double? num6 = num2 + num * (item2 - num2);
					double? num7 = num4 + num * (num6 - num4);
					double? num8 = num5 + num * (num7 - num5);
					temaResult.Tema = (3.0 * num6 - 3.0 * num7 + num8).NaN2Null();
					num2 = num6;
					num4 = num7;
					num5 = num8;
				}
				else if (j == lookbackPeriods - 1)
				{
					temaResult.Tema = (3.0 * num2 - 3.0 * num4 + num5).NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateTema(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for TEMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<TemaResult> RemoveWarmupPeriods(this IEnumerable<TemaResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((TemaResult x) => x.Tema.HasValue) + 1;
			return results.Remove(3 * num + 100);
		}
	}

	/// <summary>
	///     True Range (TR) is a measure of volatility that captures gaps and limits between periods.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Atr/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><returns>Time series of True Range (TR) values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<TrResult> GetTr<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcTr();
	}

	internal static List<TrResult> CalcTr(this List<QuoteD> qdList)
	{
		List<TrResult> list = new List<TrResult>(qdList.Count);
		double num = double.NaN;
		for (int i = 0; i < qdList.Count; i = checked(i + 1))
		{
			QuoteD quoteD = qdList[i];
			TrResult trResult = new TrResult(quoteD.Date);
			list.Add(trResult);
			if (i == 0)
			{
				num = quoteD.Close;
				continue;
			}
			double val = Math.Abs(quoteD.High - num);
			double val2 = Math.Abs(quoteD.Low - num);
			trResult.Tr = Math.Max(quoteD.High - quoteD.Low, Math.Max(val, val2));
			num = quoteD.Close;
		}
		return list;
	}

	/// <summary>
	///     Triple EMA Oscillator (TRIX) is the rate of change for a 3 EMA smoothing of the price over a lookback window. TRIX is often confused with TEMA.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Trix/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="signalPeriods">Optional.  Number of periods for a TRIX SMA signal line.</param><returns>Time series of TRIX values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<TrixResult> GetTrix<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods, int? signalPeriods = null) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcTrix(lookbackPeriods, signalPeriods);
	}

	public static IEnumerable<TrixResult> GetTrix(this IEnumerable<IReusableResult> results, int lookbackPeriods, int? signalPeriods = null)
	{
		return results.ToTuple().CalcTrix(lookbackPeriods, signalPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<TrixResult> GetTrix(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods, int? signalPeriods = null)
	{
		return priceTuples.ToSortedList().CalcTrix(lookbackPeriods, signalPeriods);
	}

	internal static List<TrixResult> CalcTrix(this List<(DateTime, double)> tpList, int lookbackPeriods, int? signalPeriods)
	{
		ValidateTrix(lookbackPeriods);
		int count = tpList.Count;
		List<TrixResult> list = new List<TrixResult>(count);
		checked
		{
			double num = 2.0 / (double)(lookbackPeriods + 1);
			double? num2 = 0.0;
			int num3 = Math.Min(lookbackPeriods, count);
			for (int i = 0; i < num3; i++)
			{
				num2 += tpList[i].Item2;
			}
			num2 /= (double)num3;
			double? num5;
			double? num4 = (num5 = num2);
			for (int j = 0; j < count; j++)
			{
				(DateTime, double) tuple = tpList[j];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				TrixResult trixResult = new TrixResult(item);
				list.Add(trixResult);
				if (j >= lookbackPeriods)
				{
					double? num6 = num2 + num * (item2 - num2);
					double? obj = num4 + num * (num6 - num4);
					double? num7 = num5 + num * (obj - num5);
					trixResult.Ema3 = num7.NaN2Null();
					trixResult.Trix = (100.0 * (num7 - num5) / num5).NaN2Null();
					num2 = num6;
					num4 = obj;
					num5 = num7;
				}
				CalcTrixSignal(signalPeriods, j, lookbackPeriods, list);
			}
			return list;
		}
	}

	private static void CalcTrixSignal(int? signalPeriods, int i, int lookbackPeriods, List<TrixResult> results)
	{
		checked
		{
			if (signalPeriods.HasValue && i >= lookbackPeriods + signalPeriods - 1)
			{
				double? num = 0.0;
				for (int j = i + 1 - signalPeriods.Value; j <= i; j++)
				{
					num += results[j].Trix;
				}
				results[i].Signal = num / (double?)signalPeriods;
			}
		}
	}

	private static void ValidateTrix(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for TRIX.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<TrixResult> RemoveWarmupPeriods(this IEnumerable<TrixResult> results)
	{
		int num = results.ToList().FindIndex((TrixResult x) => x.Trix.HasValue);
		return results.Remove(checked(3 * num + 100));
	}

	/// <summary>
	///     True Strength Index (TSI) is a momentum oscillator that depicts trends in price changes.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Tsi/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods for the first EMA.</param><param name="smoothPeriods">Number of periods in the second smoothing.</param><param name="signalPeriods">Number of periods in the TSI SMA signal line.</param><returns>Time series of TSI values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<TsiResult> GetTsi<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 25, int smoothPeriods = 13, int signalPeriods = 7) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcTsi(lookbackPeriods, smoothPeriods, signalPeriods);
	}

	public static IEnumerable<TsiResult> GetTsi(this IEnumerable<IReusableResult> results, int lookbackPeriods = 25, int smoothPeriods = 13, int signalPeriods = 7)
	{
		return results.ToTuple().CalcTsi(lookbackPeriods, smoothPeriods, signalPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<TsiResult> GetTsi(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods = 25, int smoothPeriods = 13, int signalPeriods = 7)
	{
		return priceTuples.ToSortedList().CalcTsi(lookbackPeriods, smoothPeriods, signalPeriods);
	}

	internal static List<TsiResult> CalcTsi(this List<(DateTime, double)> tpList, int lookbackPeriods, int smoothPeriods, int signalPeriods)
	{
		ValidateTsi(lookbackPeriods, smoothPeriods, signalPeriods);
		int count = tpList.Count;
		checked
		{
			double num = 2.0 / (double)(lookbackPeriods + 1);
			double num2 = 2.0 / (double)(smoothPeriods + 1);
			double num3 = 2.0 / (double)(signalPeriods + 1);
			double num4 = 0.0;
			List<TsiResult> list = new List<TsiResult>(count);
			double[] array = new double[count];
			double[] array2 = new double[count];
			double[] array3 = new double[count];
			double num5 = 0.0;
			double num6 = 0.0;
			double[] array4 = new double[count];
			double[] array5 = new double[count];
			double[] array6 = new double[count];
			double num7 = 0.0;
			double num8 = 0.0;
			for (int i = 0; i < count; i++)
			{
				(DateTime, double) tuple = tpList[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				TsiResult tsiResult = new TsiResult(item);
				list.Add(tsiResult);
				if (i == 0)
				{
					continue;
				}
				array[i] = item2 - tpList[i - 1].Item2;
				array4[i] = Math.Abs(array[i]);
				if (i > lookbackPeriods)
				{
					array2[i] = (array[i] - array2[i - 1]) * num + array2[i - 1];
					array5[i] = (array4[i] - array5[i - 1]) * num + array5[i - 1];
					if (i + 1 > lookbackPeriods + smoothPeriods)
					{
						array3[i] = (array2[i] - array3[i - 1]) * num2 + array3[i - 1];
						array6[i] = (array5[i] - array6[i - 1]) * num2 + array6[i - 1];
						double num9 = ((array6[i] != 0.0) ? (100.0 * (array3[i] / array6[i])) : double.NaN);
						tsiResult.Tsi = num9.NaN2Null();
						if (signalPeriods > 0)
						{
							int num10 = lookbackPeriods + smoothPeriods + signalPeriods - 1;
							if (i >= num10)
							{
								tsiResult.Signal = ((num9 - list[i - 1].Signal) * num3).NaN2Null() + list[i - 1].Signal;
							}
							else if (i == num10 - 1)
							{
								num4 += num9;
								tsiResult.Signal = num4 / (double)signalPeriods;
							}
							else
							{
								num4 += num9;
							}
						}
					}
					else
					{
						num6 += array2[i];
						num8 += array5[i];
						if (i + 1 == lookbackPeriods + smoothPeriods)
						{
							array3[i] = num6 / (double)smoothPeriods;
							array6[i] = num8 / (double)smoothPeriods;
							double num11 = ((array6[i] != 0.0) ? (100.0 * array3[i] / array6[i]) : double.NaN);
							tsiResult.Tsi = num11;
							num4 = num11;
						}
					}
				}
				else
				{
					num5 += array[i];
					num7 += array4[i];
					if (i == lookbackPeriods)
					{
						array2[i] = num5 / (double)lookbackPeriods;
						array5[i] = num7 / (double)lookbackPeriods;
						num6 = array2[i];
						num8 = array5[i];
					}
				}
			}
			return list;
		}
	}

	private static void ValidateTsi(int lookbackPeriods, int smoothPeriods, int signalPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for TSI.");
		}
		if (smoothPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("smoothPeriods", smoothPeriods, "Smoothing periods must be greater than 0 for TSI.");
		}
		if (signalPeriods < 0)
		{
			throw new ArgumentOutOfRangeException("signalPeriods", signalPeriods, "Signal periods must be greater than or equal to 0 for TSI.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<TsiResult> RemoveWarmupPeriods(this IEnumerable<TsiResult> results)
	{
		checked
		{
			int num = results.ToList().FindIndex((TsiResult x) => x.Tsi.HasValue) + 1;
			return results.Remove(num + 250);
		}
	}

	/// <summary>
	///     Ulcer Index (UI) is a measure of downside price volatility over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/UlcerIndex/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Ulcer Index values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<UlcerIndexResult> GetUlcerIndex<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcUlcerIndex(lookbackPeriods);
	}

	public static IEnumerable<UlcerIndexResult> GetUlcerIndex(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcUlcerIndex(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<UlcerIndexResult> GetUlcerIndex(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcUlcerIndex(lookbackPeriods);
	}

	internal static List<UlcerIndexResult> CalcUlcerIndex(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateUlcer(lookbackPeriods);
		List<UlcerIndexResult> list = new List<UlcerIndexResult>(tpList.Count);
		checked
		{
			for (int i = 0; i < tpList.Count; i++)
			{
				UlcerIndexResult ulcerIndexResult = new UlcerIndexResult(tpList[i].Item1);
				list.Add(ulcerIndexResult);
				if (i + 1 < lookbackPeriods)
				{
					continue;
				}
				double num = 0.0;
				for (int j = i + 1 - lookbackPeriods; j <= i; j++)
				{
					double item = tpList[j].Item2;
					int num2 = j + 1;
					double num3 = 0.0;
					for (int k = i + 1 - lookbackPeriods; k < num2; k++)
					{
						double item2 = tpList[k].Item2;
						if (item2 > num3)
						{
							num3 = item2;
						}
					}
					double num4 = ((num3 == 0.0) ? double.NaN : (100.0 * ((item - num3) / num3)));
					num += num4 * num4;
				}
				ulcerIndexResult.UI = Math.Sqrt(num / (double)lookbackPeriods).NaN2Null();
			}
			return list;
		}
	}

	private static void ValidateUlcer(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Ulcer Index.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<UlcerIndexResult> RemoveWarmupPeriods(this IEnumerable<UlcerIndexResult> results)
	{
		int removePeriods = results.ToList().FindIndex((UlcerIndexResult x) => x.UI.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Ultimate Oscillator uses several lookback periods to weigh buying power against True Range price to produce on oversold / overbought oscillator.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Ultimate/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="shortPeriods">Number of periods in the smallest window.</param><param name="middlePeriods">Number of periods in the middle-sized window.</param><param name="longPeriods">Number of periods in the largest window.</param><returns>Time series of Ultimate Oscillator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<UltimateResult> GetUltimate<TQuote>(this IEnumerable<TQuote> quotes, int shortPeriods = 7, int middlePeriods = 14, int longPeriods = 28) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcUltimate(shortPeriods, middlePeriods, longPeriods);
	}

	internal static List<UltimateResult> CalcUltimate(this List<QuoteD> qdList, int shortPeriods, int middlePeriods, int longPeriods)
	{
		ValidateUltimate(shortPeriods, middlePeriods, longPeriods);
		int count = qdList.Count;
		List<UltimateResult> list = new List<UltimateResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		double val = 0.0;
		checked
		{
			for (int i = 0; i < qdList.Count; i++)
			{
				QuoteD quoteD = qdList[i];
				UltimateResult ultimateResult = new UltimateResult(quoteD.Date);
				list.Add(ultimateResult);
				if (i > 0)
				{
					array[i] = quoteD.Close - Math.Min(quoteD.Low, val);
					array2[i] = Math.Max(quoteD.High, val) - Math.Min(quoteD.Low, val);
				}
				if (i >= longPeriods)
				{
					double num = 0.0;
					double num2 = 0.0;
					double num3 = 0.0;
					double num4 = 0.0;
					double num5 = 0.0;
					double num6 = 0.0;
					for (int j = i + 1 - longPeriods; j <= i; j++)
					{
						int num7 = j + 1;
						if (num7 > i + 1 - shortPeriods)
						{
							num += array[j];
							num4 += array2[j];
						}
						if (num7 > i + 1 - middlePeriods)
						{
							num2 += array[j];
							num5 += array2[j];
						}
						num3 += array[j];
						num6 += array2[j];
					}
					double num8 = ((num4 == 0.0) ? double.NaN : (num / num4));
					double num9 = ((num5 == 0.0) ? double.NaN : (num2 / num5));
					double num10 = ((num6 == 0.0) ? double.NaN : (num3 / num6));
					ultimateResult.Ultimate = (100.0 * (4.0 * num8 + 2.0 * num9 + num10) / 7.0).NaN2Null();
				}
				val = quoteD.Close;
			}
			return list;
		}
	}

	private static void ValidateUltimate(int shortPeriods, int middleAverage, int longPeriods)
	{
		if (shortPeriods <= 0 || middleAverage <= 0 || longPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("longPeriods", longPeriods, "Average periods must be greater than 0 for Ultimate Oscillator.");
		}
		if (shortPeriods >= middleAverage || middleAverage >= longPeriods)
		{
			throw new ArgumentOutOfRangeException("middleAverage", middleAverage, "Average periods must be increasingly larger than each other for Ultimate Oscillator.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<UltimateResult> RemoveWarmupPeriods(this IEnumerable<UltimateResult> results)
	{
		int removePeriods = results.ToList().FindIndex((UltimateResult x) => x.Ultimate.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Volatility Stop is an ATR based indicator used to determine trend direction, stops, and reversals.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/VolatilityStop/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><param name="multiplier">ATR offset amount.</param><returns>Time series of Volatility Stop values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<VolatilityStopResult> GetVolatilityStop<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 7, double multiplier = 3.0) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcVolatilityStop(lookbackPeriods, multiplier);
	}

	internal static List<VolatilityStopResult> CalcVolatilityStop(this List<QuoteD> qdList, int lookbackPeriods, double multiplier)
	{
		List<(DateTime, double)> list = qdList.ToTuple(CandlePart.Close);
		ValidateVolatilityStop(lookbackPeriods, multiplier);
		int count = list.Count;
		List<VolatilityStopResult> list2 = new List<VolatilityStopResult>(count);
		if (count == 0)
		{
			return list2;
		}
		List<AtrResult> list3 = qdList.CalcAtr(lookbackPeriods);
		int num = Math.Min(count, lookbackPeriods);
		double num2 = list[0].Item2;
		checked
		{
			bool flag = list[num - 1].Item2 > num2;
			for (int i = 0; i < num; i++)
			{
				(DateTime, double) tuple = list[i];
				DateTime item = tuple.Item1;
				double item2 = tuple.Item2;
				num2 = (flag ? Math.Max(num2, item2) : Math.Min(num2, item2));
				list2.Add(new VolatilityStopResult(item));
			}
			for (int j = lookbackPeriods; j < count; j++)
			{
				(DateTime, double) tuple2 = list[j];
				DateTime item3 = tuple2.Item1;
				double item4 = tuple2.Item2;
				double? num3 = list3[j - 1].Atr * multiplier;
				VolatilityStopResult volatilityStopResult = new VolatilityStopResult(item3)
				{
					Sar = ((!flag) ? (num2 + num3) : (num2 - num3))
				};
				list2.Add(volatilityStopResult);
				if (flag)
				{
					volatilityStopResult.LowerBand = volatilityStopResult.Sar;
				}
				else
				{
					volatilityStopResult.UpperBand = volatilityStopResult.Sar;
				}
				if ((flag && item4 < volatilityStopResult.Sar) || (!flag && item4 > volatilityStopResult.Sar))
				{
					volatilityStopResult.IsStop = true;
					num2 = item4;
					flag = !flag;
				}
				else
				{
					volatilityStopResult.IsStop = false;
					num2 = (flag ? Math.Max(num2, item4) : Math.Min(num2, item4));
				}
			}
			VolatilityStopResult volatilityStopResult2 = (from x in list2
				where x.IsStop == true
				orderby x.Date
				select x).FirstOrDefault();
			if (volatilityStopResult2 != null)
			{
				int num4 = list2.IndexOf(volatilityStopResult2);
				for (int num5 = 0; num5 <= num4; num5++)
				{
					VolatilityStopResult volatilityStopResult3 = list2[num5];
					volatilityStopResult3.Sar = null;
					volatilityStopResult3.UpperBand = null;
					volatilityStopResult3.LowerBand = null;
					volatilityStopResult3.IsStop = null;
				}
			}
			return list2;
		}
	}

	private static void ValidateVolatilityStop(int lookbackPeriods, double multiplier)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for Volatility Stop.");
		}
		if (multiplier <= 0.0)
		{
			throw new ArgumentOutOfRangeException("multiplier", multiplier, "ATR Multiplier must be greater than 0 for Volatility Stop.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<VolatilityStopResult> RemoveWarmupPeriods(this IEnumerable<VolatilityStopResult> results)
	{
		int val = results.ToList().FindIndex((VolatilityStopResult x) => x.Sar.HasValue);
		val = Math.Max(100, val);
		return results.Remove(val);
	}

	/// <summary>
	///     Vortex Indicator (VI) is a measure of price directional movement.
	///     It includes positive and negative indicators, and is often used to identify trends and reversals.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Vortex/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of VI+ and VI- vortex movement indicator values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<VortexResult> GetVortex<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcVortex(lookbackPeriods);
	}

	internal static List<VortexResult> CalcVortex(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateVortex(lookbackPeriods);
		int count = qdList.Count;
		List<VortexResult> list = new List<VortexResult>(count);
		double[] array = new double[count];
		double[] array2 = new double[count];
		double[] array3 = new double[count];
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				QuoteD quoteD = qdList[i];
				VortexResult vortexResult = new VortexResult(quoteD.Date);
				list.Add(vortexResult);
				if (i == 0)
				{
					num = quoteD.High;
					num2 = quoteD.Low;
					num3 = quoteD.Close;
					continue;
				}
				double val = Math.Abs(quoteD.High - num3);
				double val2 = Math.Abs(quoteD.Low - num3);
				array[i] = Math.Max(quoteD.High - quoteD.Low, Math.Max(val, val2));
				array2[i] = Math.Abs(quoteD.High - num2);
				array3[i] = Math.Abs(quoteD.Low - num);
				num = quoteD.High;
				num2 = quoteD.Low;
				num3 = quoteD.Close;
				if (i + 1 > lookbackPeriods)
				{
					double num4 = 0.0;
					double num5 = 0.0;
					double num6 = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						num4 += array[j];
						num5 += array2[j];
						num6 += array3[j];
					}
					if (num4 != 0.0)
					{
						vortexResult.Pvi = num5 / num4;
						vortexResult.Nvi = num6 / num4;
					}
				}
			}
			return list;
		}
	}

	private static void ValidateVortex(int lookbackPeriods)
	{
		if (lookbackPeriods <= 1)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 1 for VI.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<VortexResult> Condense(this IEnumerable<VortexResult> results)
	{
		List<VortexResult> list = results.ToList();
		list.RemoveAll((VortexResult x) => !x.Pvi.HasValue && !x.Nvi.HasValue);
		return list.ToSortedList();
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<VortexResult> RemoveWarmupPeriods(this IEnumerable<VortexResult> results)
	{
		int removePeriods = results.ToList().FindIndex((VortexResult x) => x.Pvi.HasValue || x.Nvi.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Volume Weighted Average Price (VWAP) is a Volume weighted average of price, typically used on intraday data.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Vwap/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="startDate">Optional anchor date.  If not provided, the first date in quotes is used.</param><returns>Time series of VWAP values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<VwapResult> GetVwap<TQuote>(this IEnumerable<TQuote> quotes, DateTime? startDate = null) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcVwap(startDate);
	}

	internal static List<VwapResult> CalcVwap(this List<QuoteD> qdList, DateTime? startDate = null)
	{
		ValidateVwap(qdList, startDate);
		int count = qdList.Count;
		List<VwapResult> list = new List<VwapResult>(count);
		if (count == 0)
		{
			return list;
		}
		DateTime valueOrDefault = startDate.GetValueOrDefault();
		if (!startDate.HasValue)
		{
			valueOrDefault = qdList[0].Date;
			startDate = valueOrDefault;
		}
		double? num = 0.0;
		double? num2 = 0.0;
		for (int i = 0; i < count; i = checked(i + 1))
		{
			QuoteD quoteD = qdList[i];
			double? num3 = quoteD.Volume;
			double? num4 = quoteD.High;
			double? num5 = quoteD.Low;
			double? num6 = quoteD.Close;
			VwapResult vwapResult = new VwapResult(quoteD.Date);
			list.Add(vwapResult);
			valueOrDefault = quoteD.Date;
			DateTime? dateTime = startDate;
			if (valueOrDefault >= dateTime)
			{
				num += num3;
				num2 += num3 * (num4 + num5 + num6) / 3.0;
				vwapResult.Vwap = ((num == 0.0) ? ((double?)null) : (num2 / num));
			}
		}
		return list;
	}

	private static void ValidateVwap(List<QuoteD> quotesList, DateTime? startDate)
	{
		if (quotesList.Count == 0 || !(startDate < quotesList[0].Date))
		{
			return;
		}
		throw new ArgumentOutOfRangeException("startDate", startDate, "Start Date must be within the quotes range for VWAP.");
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<VwapResult> RemoveWarmupPeriods(this IEnumerable<VwapResult> results)
	{
		int removePeriods = results.ToList().FindIndex((VwapResult x) => x.Vwap.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Volume Weighted Moving Average is the volume adjusted average price over a lookback window.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Vwma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Volume Weighted Moving Average values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<VwmaResult> GetVwma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcVwma(lookbackPeriods);
	}

	internal static List<VwmaResult> CalcVwma(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateVwma(lookbackPeriods);
		int count = qdList.Count;
		List<VwmaResult> list = new List<VwmaResult>(count);
		checked
		{
			for (int i = 0; i < count; i++)
			{
				VwmaResult vwmaResult = new VwmaResult(qdList[i].Date);
				list.Add(vwmaResult);
				if (i + 1 >= lookbackPeriods)
				{
					double? num = 0.0;
					double? num2 = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						QuoteD quoteD = qdList[j];
						double? num3 = quoteD.Close;
						double? num4 = quoteD.Volume;
						num += num3 * num4;
						num2 += num4;
					}
					vwmaResult.Vwma = ((num2 == 0.0) ? ((double?)null) : (num / num2));
				}
			}
			return list;
		}
	}

	private static void ValidateVwma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for Vwma.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<VwmaResult> RemoveWarmupPeriods(this IEnumerable<VwmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((VwmaResult x) => x.Vwma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Williams %R momentum indicator is a stochastic oscillator with scale of -100 to 0. It is exactly the same as the Fast variant of Stochastic Oscillator, but with a different scaling.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/WilliamsR/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of Williams %R values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<WilliamsResult> GetWilliamsR<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods = 14) where TQuote : IQuote
	{
		return quotes.ToQuoteD().CalcWilliamsR(lookbackPeriods);
	}

	internal static List<WilliamsResult> CalcWilliamsR(this List<QuoteD> qdList, int lookbackPeriods)
	{
		ValidateWilliam(lookbackPeriods);
		return (from s in qdList.CalcStoch(lookbackPeriods, 1, 1, 3.0, 2.0, MaType.SMA)
			select new WilliamsResult(s.Date)
			{
				WilliamsR = s.Oscillator - 100.0
			}).ToList();
	}

	private static void ValidateWilliam(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for William %R.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<WilliamsResult> RemoveWarmupPeriods(this IEnumerable<WilliamsResult> results)
	{
		int removePeriods = results.ToList().FindIndex((WilliamsResult x) => x.WilliamsR.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Weighted Moving Average (WMA) is the linear weighted average of price over N lookback periods. This also called Linear Weighted Moving Average (LWMA).
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/Wma/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="lookbackPeriods">Number of periods in the lookback window.</param><returns>Time series of WMA values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<WmaResult> GetWma<TQuote>(this IEnumerable<TQuote> quotes, int lookbackPeriods) where TQuote : IQuote
	{
		return quotes.ToTuple(CandlePart.Close).CalcWma(lookbackPeriods);
	}

	public static IEnumerable<WmaResult> GetWma(this IEnumerable<IReusableResult> results, int lookbackPeriods)
	{
		return results.ToTuple().CalcWma(lookbackPeriods).SyncIndex(results, SyncType.Prepend);
	}

	public static IEnumerable<WmaResult> GetWma(this IEnumerable<(DateTime, double)> priceTuples, int lookbackPeriods)
	{
		return priceTuples.ToSortedList().CalcWma(lookbackPeriods);
	}

	internal static List<WmaResult> CalcWma(this List<(DateTime, double)> tpList, int lookbackPeriods)
	{
		ValidateWma(lookbackPeriods);
		List<WmaResult> list = new List<WmaResult>(tpList.Count);
		checked
		{
			double num = (double)lookbackPeriods * (double)(lookbackPeriods + 1) / 2.0;
			for (int i = 0; i < tpList.Count; i++)
			{
				WmaResult wmaResult = new WmaResult(tpList[i].Item1);
				list.Add(wmaResult);
				if (i + 1 >= lookbackPeriods)
				{
					double num2 = 0.0;
					for (int j = i + 1 - lookbackPeriods; j <= i; j++)
					{
						double item = tpList[j].Item2;
						num2 += item * (double)(lookbackPeriods - (i + 1 - j - 1)) / num;
					}
					wmaResult.Wma = num2.NaN2Null();
				}
			}
			return list;
		}
	}

	private static void ValidateWma(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for WMA.");
		}
	}

	/// <summary> Removes the recommended quantity of results from the beginning of the results list
	///       using a reverse-engineering approach. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator
	///       results to evaluate.</param><returns>Time
	///       series of results, pruned.</returns>
	public static IEnumerable<WmaResult> RemoveWarmupPeriods(this IEnumerable<WmaResult> results)
	{
		int removePeriods = results.ToList().FindIndex((WmaResult x) => x.Wma.HasValue);
		return results.Remove(removePeriods);
	}

	/// <summary>
	///     Zig Zag is a price chart overlay that simplifies the up and down movements and transitions based on a percent change smoothing threshold.
	///     <para>
	///       See
	///       <see href="https://dotnet.StockIndicators.dev/indicators/ZigZag/#content?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///       for more information.
	///     </para>
	///   </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="endType">Determines use of Close or High/Low wicks for extreme points.</param><param name="percentChange">Percent price change to set threshold for minimum size movements.</param><returns>Time series of Zig Zag values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<ZigZagResult> GetZigZag<TQuote>(this IEnumerable<TQuote> quotes, EndType endType = EndType.Close, decimal percentChange = 5m) where TQuote : IQuote
	{
		return quotes.ToSortedList().CalcZigZag(endType, percentChange);
	}

	internal static List<ZigZagResult> CalcZigZag<TQuote>(this List<TQuote> quotesList, EndType endType = EndType.Close, decimal percentChange = 5m) where TQuote : IQuote
	{
		ValidateZigZag(percentChange);
		int count = quotesList.Count;
		List<ZigZagResult> list = new List<ZigZagResult>(count);
		if (count == 0)
		{
			return list;
		}
		TQuote q = quotesList[0];
		ZigZagEval zigZagEval = GetZigZagEval(endType, 1, q);
		decimal num = percentChange / 100m;
		ZigZagPoint zigZagPoint = new ZigZagPoint
		{
			Index = zigZagEval.Index,
			Value = q.Close,
			PointType = "U"
		};
		ZigZagPoint zigZagPoint2 = new ZigZagPoint
		{
			Index = zigZagEval.Index,
			Value = zigZagEval.High,
			PointType = "H"
		};
		ZigZagPoint zigZagPoint3 = new ZigZagPoint
		{
			Index = zigZagEval.Index,
			Value = zigZagEval.Low,
			PointType = "L"
		};
		int num2 = count;
		checked
		{
			for (int i = 0; i < count; i++)
			{
				TQuote q2 = quotesList[i];
				int index = i + 1;
				zigZagEval = GetZigZagEval(endType, index, q2);
				decimal? value = zigZagPoint3.Value;
				decimal? num3 = (((value.GetValueOrDefault() == default(decimal)) & value.HasValue) ? ((decimal?)null) : ((zigZagEval.High - zigZagPoint3.Value) / zigZagPoint3.Value));
				decimal? value2 = zigZagPoint2.Value;
				decimal? num4 = (((value2.GetValueOrDefault() == default(decimal)) & value2.HasValue) ? ((decimal?)null) : ((zigZagPoint2.Value - zigZagEval.Low) / zigZagPoint2.Value));
				value = num3;
				decimal num5 = num;
				if (((value.GetValueOrDefault() >= num5) & value.HasValue) && num3 > num4)
				{
					zigZagPoint.Index = zigZagPoint3.Index;
					zigZagPoint.Value = zigZagPoint3.Value;
					zigZagPoint.PointType = zigZagPoint3.PointType;
					break;
				}
				value2 = num4;
				num5 = num;
				if (((value2.GetValueOrDefault() >= num5) & value2.HasValue) && num4 > num3)
				{
					zigZagPoint.Index = zigZagPoint2.Index;
					zigZagPoint.Value = zigZagPoint2.Value;
					zigZagPoint.PointType = zigZagPoint2.PointType;
					break;
				}
			}
			ZigZagResult item = new ZigZagResult(q.Date);
			list.Add(item);
			while (zigZagPoint.Index < num2)
			{
				ZigZagPoint nextPoint = EvaluateNextPoint(quotesList, endType, num, zigZagPoint);
				string pointType = zigZagPoint.PointType;
				DrawZigZagLine(list, quotesList, zigZagPoint, nextPoint);
				DrawRetraceLine(list, pointType, zigZagPoint3, zigZagPoint2, nextPoint);
			}
			return list;
		}
	}

	private static ZigZagPoint EvaluateNextPoint<TQuote>(List<TQuote> quotesList, EndType endType, decimal changeThreshold, ZigZagPoint lastPoint) where TQuote : IQuote
	{
		bool flag = lastPoint.PointType == "L";
		ZigZagPoint zigZagPoint = new ZigZagPoint
		{
			Index = lastPoint.Index,
			Value = lastPoint.Value,
			PointType = (flag ? "H" : "L")
		};
		checked
		{
			for (int i = lastPoint.Index; i < quotesList.Count; i++)
			{
				TQuote q = quotesList[i];
				int num = i + 1;
				ZigZagEval zigZagEval = GetZigZagEval(endType, num, q);
				decimal? num2;
				if (flag)
				{
					if (zigZagEval.High >= zigZagPoint.Value)
					{
						zigZagPoint.Index = zigZagEval.Index;
						zigZagPoint.Value = zigZagEval.High;
						num2 = default(decimal);
					}
					else
					{
						decimal? value = zigZagPoint.Value;
						num2 = (((value.GetValueOrDefault() == default(decimal)) & value.HasValue) ? ((decimal?)null) : ((zigZagPoint.Value - zigZagEval.Low) / zigZagPoint.Value));
					}
				}
				else if (zigZagEval.Low <= zigZagPoint.Value)
				{
					zigZagPoint.Index = zigZagEval.Index;
					zigZagPoint.Value = zigZagEval.Low;
					num2 = default(decimal);
				}
				else
				{
					decimal? value = zigZagPoint.Value;
					num2 = (((value.GetValueOrDefault() == default(decimal)) & value.HasValue) ? ((decimal?)null) : ((zigZagEval.High - zigZagPoint.Value) / zigZagPoint.Value));
				}
				decimal? num3 = num2;
				decimal num4 = changeThreshold;
				if ((num3.GetValueOrDefault() >= num4) & num3.HasValue)
				{
					return zigZagPoint;
				}
				if (num == quotesList.Count)
				{
					zigZagPoint.Index = num;
					zigZagPoint.Value = (flag ? zigZagEval.High : zigZagEval.Low);
					zigZagPoint.PointType = null;
				}
			}
			return zigZagPoint;
		}
	}

	private static void DrawZigZagLine<TQuote>(List<ZigZagResult> results, List<TQuote> quotesList, ZigZagPoint lastPoint, ZigZagPoint nextPoint) where TQuote : IQuote
	{
		checked
		{
			if (nextPoint.Index != lastPoint.Index)
			{
				decimal? num = (nextPoint.Value - lastPoint.Value) / (decimal?)(nextPoint.Index - lastPoint.Index);
				for (int i = lastPoint.Index; i < nextPoint.Index; i++)
				{
					TQuote val = quotesList[i];
					int num2 = i + 1;
					ZigZagResult item = new ZigZagResult(val.Date)
					{
						ZigZag = ((lastPoint.Index == 1 && num2 != nextPoint.Index) ? ((decimal?)null) : (lastPoint.Value + num * (decimal?)(num2 - lastPoint.Index))),
						PointType = ((num2 == nextPoint.Index) ? nextPoint.PointType : null)
					};
					results.Add(item);
				}
			}
			lastPoint.Index = nextPoint.Index;
			lastPoint.Value = nextPoint.Value;
			lastPoint.PointType = nextPoint.PointType;
		}
	}

	private static void DrawRetraceLine(List<ZigZagResult> results, string lastDirection, ZigZagPoint lastLowPoint, ZigZagPoint lastHighPoint, ZigZagPoint nextPoint)
	{
		ZigZagPoint zigZagPoint = new ZigZagPoint();
		if (lastDirection == "L")
		{
			zigZagPoint.Index = lastHighPoint.Index;
			zigZagPoint.Value = lastHighPoint.Value;
			lastHighPoint.Index = nextPoint.Index;
			lastHighPoint.Value = nextPoint.Value;
		}
		else if (lastDirection == "H")
		{
			zigZagPoint.Index = lastLowPoint.Index;
			zigZagPoint.Value = lastLowPoint.Value;
			lastLowPoint.Index = nextPoint.Index;
			lastLowPoint.Value = nextPoint.Value;
		}
		if (lastDirection == "U" || zigZagPoint.Index == 1 || nextPoint.Index == zigZagPoint.Index)
		{
			return;
		}
		checked
		{
			decimal? num = (nextPoint.Value - zigZagPoint.Value) / (decimal?)(nextPoint.Index - zigZagPoint.Index);
			for (int i = zigZagPoint.Index - 1; i < nextPoint.Index; i++)
			{
				ZigZagResult zigZagResult = results[i];
				int num2 = i + 1;
				if (lastDirection == "L")
				{
					zigZagResult.RetraceHigh = zigZagPoint.Value + num * (decimal?)(num2 - zigZagPoint.Index);
				}
				else if (lastDirection == "H")
				{
					zigZagResult.RetraceLow = zigZagPoint.Value + num * (decimal?)(num2 - zigZagPoint.Index);
				}
			}
		}
	}

	private static ZigZagEval GetZigZagEval<TQuote>(EndType endType, int index, TQuote q) where TQuote : IQuote
	{
		ZigZagEval zigZagEval = new ZigZagEval
		{
			Index = index
		};
		switch (endType)
		{
		case EndType.Close:
			zigZagEval.Low = q.Close;
			zigZagEval.High = q.Close;
			break;
		case EndType.HighLow:
			zigZagEval.Low = q.Low;
			zigZagEval.High = q.High;
			break;
		default:
			throw new ArgumentOutOfRangeException("endType");
		}
		return zigZagEval;
	}

	private static void ValidateZigZag(decimal percentChange)
	{
		if (percentChange <= 0m)
		{
			throw new ArgumentOutOfRangeException("percentChange", percentChange, "Percent change must be greater than 0 for ZIGZAG.");
		}
	}

	/// <summary> Removes non-essential records containing null values with unique consideration for
	///       this indicator. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="results">Indicator results to evaluate.</param><returns>Time series of
	///       indicator results, condensed.</returns>
	public static IEnumerable<ZigZagResult> Condense(this IEnumerable<ZigZagResult> results)
	{
		List<ZigZagResult> list = results.ToList();
		list.RemoveAll((ZigZagResult x) => x.PointType == null);
		return list.ToSortedList();
	}

	[ExcludeFromCodeCoverage]
	[Obsolete("'ToBasicTuple(..)' was deprecated.", false)]
	public static List<(DateTime, double)> ToBasicTuple<TQuote>(this IEnumerable<TQuote> quotes, CandlePart candlePart) where TQuote : IQuote
	{
		return quotes.ToTuple(candlePart);
	}

	[ExcludeFromCodeCoverage]
	[Obsolete("Rename 'ToResultTuple(..)' to 'ToTuple(..)' to fix.", false)]
	public static List<(DateTime Date, double Value)> ToResultTuple(this IEnumerable<IReusableResult> basicData)
	{
		return basicData.ToTuple();
	}

	[ExcludeFromCodeCoverage]
	[Obsolete("Rename 'ToTupleCollection(..)' to 'ToTupleChainable(..)' to fix.", false)]
	public static Collection<(DateTime Date, double Value)> ToTupleCollection(this IEnumerable<IReusableResult> reusable)
	{
		return reusable.ToTupleChainable();
	}

	[ExcludeFromCodeCoverage]
	[Obsolete("Rename 'ToTupleCollection(NullTo..)' to either 'ToTupleNaN(..)' or 'ToTupleNull(..)' to fix.", false)]
	public static Collection<(DateTime Date, double? Value)> ToTupleCollection(this IEnumerable<IReusableResult> reusable, NullTo nullTo)
	{
		List<IReusableResult> list = reusable.ToSortedList();
		int count = list.Count;
		Collection<(DateTime, double?)> collection = new Collection<(DateTime, double?)>();
		for (int i = 0; i < count; i = checked(i + 1))
		{
			IReusableResult reusableResult = list[i];
			collection.Add((reusableResult.Date, reusableResult.Value.Null2NaN()));
		}
		return collection;
	}

	[ExcludeFromCodeCoverage]
	[Obsolete("Change 'GetStarcBands()' to 'GetStarcBands(20)' to fix.", false)]
	public static IEnumerable<StarcBandsResult> GetStarcBands<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return quotes.GetStarcBands(20);
	}
}
[Serializable]
public sealed class AdlResult : ResultBase, IReusableResult, ISeries
{
	public double? MoneyFlowMultiplier { get; set; }

	public double? MoneyFlowVolume { get; set; }

	public double Adl { get; set; }

	public double? AdlSma { get; set; }

	double? IReusableResult.Value => Adl;

	public AdlResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AdxResult : ResultBase, IReusableResult, ISeries
{
	public double? Pdi { get; set; }

	public double? Mdi { get; set; }

	public double? Adx { get; set; }

	public double? Adxr { get; set; }

	double? IReusableResult.Value => Adx;

	public AdxResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AlligatorResult : ResultBase
{
	public double? Jaw { get; set; }

	public double? Teeth { get; set; }

	public double? Lips { get; set; }

	public AlligatorResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AlmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Alma { get; set; }

	double? IReusableResult.Value => Alma;

	public AlmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AroonResult : ResultBase, IReusableResult, ISeries
{
	public double? AroonUp { get; set; }

	public double? AroonDown { get; set; }

	public double? Oscillator { get; set; }

	double? IReusableResult.Value => Oscillator;

	public AroonResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AtrResult : ResultBase, IReusableResult, ISeries
{
	public double? Tr { get; set; }

	public double? Atr { get; set; }

	public double? Atrp { get; set; }

	double? IReusableResult.Value => Atrp;

	public AtrResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AtrStopResult : ResultBase
{
	public decimal? AtrStop { get; set; }

	public decimal? BuyStop { get; set; }

	public decimal? SellStop { get; set; }

	public AtrStopResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class AwesomeResult : ResultBase, IReusableResult, ISeries
{
	public double? Oscillator { get; set; }

	public double? Normalized { get; set; }

	double? IReusableResult.Value => Oscillator;

	public AwesomeResult(DateTime date)
	{
		base.Date = date;
	}
}
public interface IBasicData
{
	DateTime Date { get; }

	double Value { get; }
}
public class BasicData : ISeries, IBasicData, IReusableResult
{
	public DateTime Date { get; set; }

	public double Value { get; set; }

	double? IReusableResult.Value => Value;
}
[Serializable]
public sealed class BetaResult : ResultBase, IReusableResult, ISeries
{
	public double? Beta { get; set; }

	public double? BetaUp { get; set; }

	public double? BetaDown { get; set; }

	public double? Ratio { get; set; }

	public double? Convexity { get; set; }

	public double? ReturnsEval { get; set; }

	public double? ReturnsMrkt { get; set; }

	double? IReusableResult.Value => Beta;

	public BetaResult(DateTime date)
	{
		base.Date = date;
	}
}
public enum BetaType
{
	Standard,
	Up,
	Down,
	All
}
[Serializable]
public sealed class BollingerBandsResult : ResultBase, IReusableResult, ISeries
{
	public double? Sma { get; set; }

	public double? UpperBand { get; set; }

	public double? LowerBand { get; set; }

	public double? PercentB { get; set; }

	public double? ZScore { get; set; }

	public double? Width { get; set; }

	double? IReusableResult.Value => PercentB;

	public BollingerBandsResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class BopResult : ResultBase, IReusableResult, ISeries
{
	public double? Bop { get; set; }

	double? IReusableResult.Value => Bop;

	public BopResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class CciResult : ResultBase, IReusableResult, ISeries
{
	public double? Cci { get; set; }

	double? IReusableResult.Value => Cci;

	public CciResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ChaikinOscResult : ResultBase, IReusableResult, ISeries
{
	public double? MoneyFlowMultiplier { get; set; }

	public double? MoneyFlowVolume { get; set; }

	public double? Adl { get; set; }

	public double? Oscillator { get; set; }

	double? IReusableResult.Value => Oscillator;

	public ChaikinOscResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ChandelierResult : ResultBase, IReusableResult, ISeries
{
	public double? ChandelierExit { get; set; }

	double? IReusableResult.Value => ChandelierExit;

	public ChandelierResult(DateTime date)
	{
		base.Date = date;
	}
}
public enum ChandelierType
{
	Long,
	Short
}
[Serializable]
public sealed class ChopResult : ResultBase, IReusableResult, ISeries
{
	public double? Chop { get; set; }

	double? IReusableResult.Value => Chop;

	public ChopResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class CmfResult : ResultBase, IReusableResult, ISeries
{
	public double? MoneyFlowMultiplier { get; set; }

	public double? MoneyFlowVolume { get; set; }

	public double? Cmf { get; set; }

	double? IReusableResult.Value => Cmf;

	public CmfResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class CmoResult : ResultBase, IReusableResult, ISeries
{
	public double? Cmo { get; set; }

	double? IReusableResult.Value => Cmo;

	public CmoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ConnorsRsiResult : ResultBase, IReusableResult, ISeries
{
	public double? Rsi { get; set; }

	public double? RsiStreak { get; set; }

	public double? PercentRank { get; set; }

	public double? ConnorsRsi { get; set; }

	internal int Streak { get; set; }

	double? IReusableResult.Value => ConnorsRsi;

	public ConnorsRsiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class CorrResult : ResultBase, IReusableResult, ISeries
{
	public double? VarianceA { get; set; }

	public double? VarianceB { get; set; }

	public double? Covariance { get; set; }

	public double? Correlation { get; set; }

	public double? RSquared { get; set; }

	double? IReusableResult.Value => Correlation;

	public CorrResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class DemaResult : ResultBase, IReusableResult, ISeries
{
	public double? Dema { get; set; }

	double? IReusableResult.Value => Dema;

	public DemaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class DonchianResult : ResultBase
{
	public decimal? UpperBand { get; set; }

	public decimal? Centerline { get; set; }

	public decimal? LowerBand { get; set; }

	public decimal? Width { get; set; }

	public DonchianResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class DpoResult : ResultBase, IReusableResult, ISeries
{
	public double? Sma { get; set; }

	public double? Dpo { get; set; }

	double? IReusableResult.Value => Dpo;

	public DpoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class DynamicResult : ResultBase, IReusableResult, ISeries
{
	public double? Dynamic { get; set; }

	double? IReusableResult.Value => Dynamic;

	public DynamicResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ElderRayResult : ResultBase, IReusableResult, ISeries
{
	public double? Ema { get; set; }

	public double? BullPower { get; set; }

	public double? BearPower { get; set; }

	double? IReusableResult.Value => BullPower + BearPower;

	public ElderRayResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class EmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Ema { get; set; }

	double? IReusableResult.Value => Ema;

	public EmaResult(DateTime date)
	{
		base.Date = date;
	}
}
public class EmaBase
{
	internal double K { get; set; }

	internal List<EmaResult> ProtectedResults { get; set; }

	public IEnumerable<EmaResult> Results => ProtectedResults;

	internal EmaBase(IEnumerable<(DateTime, double)> tpQuotes, int lookbackPeriods)
	{
		K = 2.0 / (double)checked(lookbackPeriods + 1);
		ProtectedResults = tpQuotes.ToSortedList().CalcEma(lookbackPeriods);
	}

	public IEnumerable<EmaResult> Add(Quote quote, CandlePart candlePart = CandlePart.Close)
	{
		if (quote == null)
		{
			throw new InvalidQuotesException("quote", quote, "No quote provided.");
		}
		(DateTime, double) tuple = quote.ToTuple(candlePart);
		return Add(tuple);
	}

	public IEnumerable<EmaResult> Add((DateTime Date, double Value) tuple)
	{
		checked
		{
			int num = ProtectedResults.Count - 1;
			EmaResult emaResult = ProtectedResults[num];
			if (tuple.Date == emaResult.Date)
			{
				EmaResult emaResult2 = ProtectedResults[num - 1];
				double lastEma = ((!emaResult2.Ema.HasValue) ? double.NaN : emaResult2.Ema.Value);
				emaResult.Ema = Increment(tuple.Value, lastEma, K);
			}
			else if (tuple.Date > emaResult.Date)
			{
				double lastEma2 = ((!emaResult.Ema.HasValue) ? double.NaN : emaResult.Ema.Value);
				double value = Increment(tuple.Value, lastEma2, K);
				EmaResult item = new EmaResult(tuple.Date)
				{
					Ema = value
				};
				ProtectedResults.Add(item);
			}
			return Results;
		}
	}

	internal static double Increment(double newValue, double lastEma, double k)
	{
		return lastEma + k * (newValue - lastEma);
	}

	internal static void Validate(int lookbackPeriods)
	{
		if (lookbackPeriods <= 0)
		{
			throw new ArgumentOutOfRangeException("lookbackPeriods", lookbackPeriods, "Lookback periods must be greater than 0 for EMA.");
		}
	}
}
[Serializable]
public sealed class EpmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Epma { get; set; }

	double? IReusableResult.Value => Epma;

	public EpmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class FcbResult : ResultBase
{
	public decimal? UpperBand { get; set; }

	public decimal? LowerBand { get; set; }

	public FcbResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class FisherTransformResult : ResultBase, IReusableResult, ISeries
{
	public double? Fisher { get; set; }

	public double? Trigger { get; set; }

	double? IReusableResult.Value => Fisher;

	public FisherTransformResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ForceIndexResult : ResultBase, IReusableResult, ISeries
{
	public double? ForceIndex { get; set; }

	double? IReusableResult.Value => ForceIndex;

	public ForceIndexResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class FractalResult : ResultBase
{
	public decimal? FractalBear { get; set; }

	public decimal? FractalBull { get; set; }

	public FractalResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public class GatorResult : ResultBase
{
	public double? Upper { get; set; }

	public double? Lower { get; set; }

	public bool? UpperIsExpanding { get; set; }

	public bool? LowerIsExpanding { get; set; }

	public GatorResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class HeikinAshiResult : ResultBase, IQuote, ISeries
{
	public decimal Open { get; set; }

	public decimal High { get; set; }

	public decimal Low { get; set; }

	public decimal Close { get; set; }

	public decimal Volume { get; set; }

	public HeikinAshiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class HmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Hma { get; set; }

	double? IReusableResult.Value => Hma;

	public HmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class HtlResult : ResultBase, IReusableResult, ISeries
{
	public int? DcPeriods { get; set; }

	public double? Trendline { get; set; }

	public double? SmoothPrice { get; set; }

	double? IReusableResult.Value => Trendline;

	public HtlResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class HurstResult : ResultBase, IReusableResult, ISeries
{
	public double? HurstExponent { get; set; }

	double? IReusableResult.Value => HurstExponent;

	public HurstResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class IchimokuResult : ResultBase
{
	public decimal? TenkanSen { get; set; }

	public decimal? KijunSen { get; set; }

	public decimal? SenkouSpanA { get; set; }

	public decimal? SenkouSpanB { get; set; }

	public decimal? ChikouSpan { get; set; }

	public IchimokuResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class KamaResult : ResultBase, IReusableResult, ISeries
{
	public double? ER { get; set; }

	public double? Kama { get; set; }

	double? IReusableResult.Value => Kama;

	public KamaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class KeltnerResult : ResultBase
{
	public double? UpperBand { get; set; }

	public double? Centerline { get; set; }

	public double? LowerBand { get; set; }

	public double? Width { get; set; }

	public KeltnerResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class KvoResult : ResultBase, IReusableResult, ISeries
{
	public double? Oscillator { get; set; }

	public double? Signal { get; set; }

	double? IReusableResult.Value => Oscillator;

	internal KvoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class MacdResult : ResultBase, IReusableResult, ISeries
{
	public double? Macd { get; set; }

	public double? Signal { get; set; }

	public double? Histogram { get; set; }

	public double? FastEma { get; set; }

	public double? SlowEma { get; set; }

	double? IReusableResult.Value => Macd;

	public MacdResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class MaEnvelopeResult : ResultBase
{
	public double? Centerline { get; set; }

	public double? UpperEnvelope { get; set; }

	public double? LowerEnvelope { get; set; }

	public MaEnvelopeResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class MamaResult : ResultBase, IReusableResult, ISeries
{
	public double? Mama { get; set; }

	public double? Fama { get; set; }

	double? IReusableResult.Value => Mama;

	public MamaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class MfiResult : ResultBase, IReusableResult, ISeries
{
	public double? Mfi { get; set; }

	double? IReusableResult.Value => Mfi;

	public MfiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ObvResult : ResultBase, IReusableResult, ISeries
{
	public double Obv { get; set; }

	public double? ObvSma { get; set; }

	double? IReusableResult.Value => Obv;

	public ObvResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ParabolicSarResult : ResultBase, IReusableResult, ISeries
{
	public double? Sar { get; set; }

	public bool? IsReversal { get; set; }

	double? IReusableResult.Value => Sar;

	public ParabolicSarResult(DateTime date)
	{
		base.Date = date;
	}
}
internal interface IPivotPoint
{
	decimal? R4 { get; set; }

	decimal? R3 { get; set; }

	decimal? R2 { get; set; }

	decimal? R1 { get; set; }

	decimal? PP { get; set; }

	decimal? S1 { get; set; }

	decimal? S2 { get; set; }

	decimal? S3 { get; set; }

	decimal? S4 { get; set; }
}
[Serializable]
public sealed class PivotPointsResult : ResultBase, IPivotPoint
{
	public decimal? R4 { get; set; }

	public decimal? R3 { get; set; }

	public decimal? R2 { get; set; }

	public decimal? R1 { get; set; }

	public decimal? PP { get; set; }

	public decimal? S1 { get; set; }

	public decimal? S2 { get; set; }

	public decimal? S3 { get; set; }

	public decimal? S4 { get; set; }
}
public enum PivotPointType
{
	Standard,
	Camarilla,
	Demark,
	Fibonacci,
	Woodie
}
[Serializable]
public class PivotsResult : ResultBase
{
	public decimal? HighPoint { get; set; }

	public decimal? LowPoint { get; set; }

	public decimal? HighLine { get; set; }

	public decimal? LowLine { get; set; }

	public PivotTrend? HighTrend { get; set; }

	public PivotTrend? LowTrend { get; set; }

	public PivotsResult(DateTime date)
	{
		base.Date = date;
	}
}
public enum PivotTrend
{
	HH,
	LH,
	HL,
	LL
}
[Serializable]
public sealed class PmoResult : ResultBase, IReusableResult, ISeries
{
	public double? Pmo { get; set; }

	public double? Signal { get; set; }

	internal double? RocEma { get; set; }

	double? IReusableResult.Value => Pmo;

	public PmoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class PrsResult : ResultBase, IReusableResult, ISeries
{
	public double? Prs { get; set; }

	public double? PrsSma { get; set; }

	public double? PrsPercent { get; set; }

	double? IReusableResult.Value => Prs;

	public PrsResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class PvoResult : ResultBase, IReusableResult, ISeries
{
	public double? Pvo { get; set; }

	public double? Signal { get; set; }

	public double? Histogram { get; set; }

	double? IReusableResult.Value => Pvo;

	public PvoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class RenkoResult : ResultBase, IQuote, ISeries
{
	public decimal Open { get; set; }

	public decimal High { get; set; }

	public decimal Low { get; set; }

	public decimal Close { get; set; }

	public decimal Volume { get; set; }

	public bool IsUp { get; set; }

	public RenkoResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class RocResult : ResultBase, IReusableResult, ISeries
{
	public double? Momentum { get; set; }

	public double? Roc { get; set; }

	public double? RocSma { get; set; }

	double? IReusableResult.Value => Roc;

	public RocResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class RocWbResult : ResultBase, IReusableResult, ISeries
{
	public double? Roc { get; set; }

	public double? RocEma { get; set; }

	public double? UpperBand { get; set; }

	public double? LowerBand { get; set; }

	double? IReusableResult.Value => Roc;

	public RocWbResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class RollingPivotsResult : ResultBase, IPivotPoint
{
	public decimal? R4 { get; set; }

	public decimal? R3 { get; set; }

	public decimal? R2 { get; set; }

	public decimal? R1 { get; set; }

	public decimal? PP { get; set; }

	public decimal? S1 { get; set; }

	public decimal? S2 { get; set; }

	public decimal? S3 { get; set; }

	public decimal? S4 { get; set; }
}
[Serializable]
public sealed class RsiResult : ResultBase, IReusableResult, ISeries
{
	public double? Rsi { get; set; }

	double? IReusableResult.Value => Rsi;

	public RsiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SlopeResult : ResultBase, IReusableResult, ISeries
{
	public double? Slope { get; set; }

	public double? Intercept { get; set; }

	public double? StdDev { get; set; }

	public double? RSquared { get; set; }

	public decimal? Line { get; set; }

	double? IReusableResult.Value => Slope;

	public SlopeResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Sma { get; set; }

	double? IReusableResult.Value => Sma;

	public SmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SmaAnalysis : ResultBase, IReusableResult, ISeries
{
	public double? Sma { get; set; }

	public double? Mad { get; set; }

	public double? Mse { get; set; }

	public double? Mape { get; set; }

	double? IReusableResult.Value => Sma;

	public SmaAnalysis(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SmiResult : ResultBase, IReusableResult, ISeries
{
	public double? Smi { get; set; }

	public double? Signal { get; set; }

	double? IReusableResult.Value => Smi;

	public SmiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SmmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Smma { get; set; }

	double? IReusableResult.Value => Smma;

	public SmmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class StarcBandsResult : ResultBase
{
	public double? UpperBand { get; set; }

	public double? Centerline { get; set; }

	public double? LowerBand { get; set; }

	public StarcBandsResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class StcResult : ResultBase, IReusableResult, ISeries
{
	public double? Stc { get; set; }

	double? IReusableResult.Value => Stc;

	public StcResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class StdDevResult : ResultBase, IReusableResult, ISeries
{
	public double? StdDev { get; set; }

	public double? Mean { get; set; }

	public double? ZScore { get; set; }

	public double? StdDevSma { get; set; }

	double? IReusableResult.Value => StdDev;

	public StdDevResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class StdDevChannelsResult : ResultBase
{
	public double? Centerline { get; set; }

	public double? UpperChannel { get; set; }

	public double? LowerChannel { get; set; }

	public bool BreakPoint { get; set; }

	public StdDevChannelsResult(DateTime date)
	{
		base.Date = date;
	}
}
/// <summary>
///       Stochastic indicator results includes aliases for those who prefer the simpler K,D,J outputs.
///       <para>
///         See
///         <see href="https://dotnet.StockIndicators.dev/indicators/Stoch/#response?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
///         for more information.
///       </para>
///     </summary><remarks>
///       Standard output properties:
///       <list type="table">
///         <item>
///           <term>Oscillator</term>
///           <description>%K Oscillator over prior lookback periods.</description>
///         </item>
///         <item>
///           <term>Signal</term>
///           <description>%D Simple moving average of %K Oscillator.</description>
///         </item>
///         <item>
///           <term>PercentJ</term>
///           <description>
///             %J is the weighted divergence of %K and %D: %J=3×%K-2×%D
///           </description>
///         </item>
///       </list>
///       These are the aliases of the above properties:
///       <list type="table">
///         <item>
///           <term>K</term>
///           <description>Same as Oscillator.</description>
///         </item>
///         <item>
///           <term>D</term>
///           <description>Same as Signal.</description>
///         </item>
///         <item>
///           <term>J</term>
///           <description>Same as PercentJ.</description>
///         </item>
///       </list>
///     </remarks>
[Serializable]
public sealed class StochResult : ResultBase, IReusableResult, ISeries
{
	public double? Oscillator { get; set; }

	public double? Signal { get; set; }

	public double? PercentJ { get; set; }

	public double? K => Oscillator;

	public double? D => Signal;

	public double? J => PercentJ;

	double? IReusableResult.Value => Oscillator;

	public StochResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class StochRsiResult : ResultBase, IReusableResult, ISeries
{
	public double? StochRsi { get; set; }

	public double? Signal { get; set; }

	double? IReusableResult.Value => StochRsi;

	public StochRsiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class SuperTrendResult : ResultBase
{
	public decimal? SuperTrend { get; set; }

	public decimal? UpperBand { get; set; }

	public decimal? LowerBand { get; set; }

	public SuperTrendResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class T3Result : ResultBase, IReusableResult, ISeries
{
	public double? T3 { get; set; }

	double? IReusableResult.Value => T3;

	public T3Result(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class TemaResult : ResultBase, IReusableResult, ISeries
{
	public double? Tema { get; set; }

	double? IReusableResult.Value => Tema;

	public TemaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class TrResult : ResultBase, IReusableResult, ISeries
{
	public double? Tr { get; set; }

	double? IReusableResult.Value => Tr;

	public TrResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class TrixResult : ResultBase, IReusableResult, ISeries
{
	public double? Ema3 { get; set; }

	public double? Trix { get; set; }

	public double? Signal { get; set; }

	double? IReusableResult.Value => Trix;

	public TrixResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class TsiResult : ResultBase, IReusableResult, ISeries
{
	public double? Tsi { get; set; }

	public double? Signal { get; set; }

	double? IReusableResult.Value => Tsi;

	public TsiResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class UlcerIndexResult : ResultBase, IReusableResult, ISeries
{
	public double? UI { get; set; }

	double? IReusableResult.Value => UI;

	public UlcerIndexResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class UltimateResult : ResultBase, IReusableResult, ISeries
{
	public double? Ultimate { get; set; }

	double? IReusableResult.Value => Ultimate;

	public UltimateResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class VolatilityStopResult : ResultBase, IReusableResult, ISeries
{
	public double? Sar { get; set; }

	public bool? IsStop { get; set; }

	public double? UpperBand { get; set; }

	public double? LowerBand { get; set; }

	double? IReusableResult.Value => Sar;

	public VolatilityStopResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class VortexResult : ResultBase
{
	public double? Pvi { get; set; }

	public double? Nvi { get; set; }

	public VortexResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class VwapResult : ResultBase, IReusableResult, ISeries
{
	public double? Vwap { get; set; }

	double? IReusableResult.Value => Vwap;

	public VwapResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class VwmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Vwma { get; set; }

	double? IReusableResult.Value => Vwma;

	public VwmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class WilliamsResult : ResultBase, IReusableResult, ISeries
{
	public double? WilliamsR { get; set; }

	double? IReusableResult.Value => WilliamsR;

	public WilliamsResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class WmaResult : ResultBase, IReusableResult, ISeries
{
	public double? Wma { get; set; }

	double? IReusableResult.Value => Wma;

	public WmaResult(DateTime date)
	{
		base.Date = date;
	}
}
[Serializable]
public sealed class ZigZagResult : ResultBase, IReusableResult, ISeries
{
	public decimal? ZigZag { get; set; }

	public string? PointType { get; set; }

	public decimal? RetraceHigh { get; set; }

	public decimal? RetraceLow { get; set; }

	double? IReusableResult.Value => (double?)ZigZag;

	public ZigZagResult(DateTime date)
	{
		base.Date = date;
	}
}
internal class ZigZagEval
{
	internal int Index { get; set; }

	internal decimal? High { get; set; }

	internal decimal? Low { get; set; }
}
internal class ZigZagPoint
{
	internal int Index { get; set; }

	internal decimal? Value { get; set; }

	internal string? PointType { get; set; }
}
public static class Candlesticks
{
	public static IEnumerable<CandleResult> Condense(this IEnumerable<CandleResult> candleResults)
	{
		return candleResults.Where((CandleResult candle) => candle.Match != Match.None).ToList();
	}

	public static CandleProperties ToCandle<TQuote>(this TQuote quote) where TQuote : IQuote
	{
		return new CandleProperties
		{
			Date = quote.Date,
			Open = quote.Open,
			High = quote.High,
			Low = quote.Low,
			Close = quote.Close,
			Volume = quote.Volume
		};
	}

	public static IEnumerable<CandleProperties> ToCandles<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return (from x in quotes
			select x.ToCandle() into x
			orderby x.Date
			select x).ToList();
	}

	internal static List<CandleResult> ToCandleResults<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return (from x in quotes
			select new CandleResult(x.Date)
			{
				Match = Match.None,
				Candle = x.ToCandle()
			} into x
			orderby x.Date
			select x).ToList();
	}
}
[Serializable]
public class CandleProperties : Quote
{
	public decimal? Size => base.High - base.Low;

	public decimal? Body => (base.Open > base.Close) ? (base.Open - base.Close) : (base.Close - base.Open);

	public decimal? UpperWick => base.High - ((base.Open > base.Close) ? base.Open : base.Close);

	public decimal? LowerWick => ((base.Open > base.Close) ? base.Close : base.Open) - base.Low;

	public double? BodyPct
	{
		get
		{
			decimal? size = Size;
			if ((size.GetValueOrDefault() == default(decimal)) & size.HasValue)
			{
				return 1.0;
			}
			return (double?)(Body / Size);
		}
	}

	public double? UpperWickPct
	{
		get
		{
			decimal? size = Size;
			if ((size.GetValueOrDefault() == default(decimal)) & size.HasValue)
			{
				return 1.0;
			}
			return (double?)(UpperWick / Size);
		}
	}

	public double? LowerWickPct
	{
		get
		{
			decimal? size = Size;
			if ((size.GetValueOrDefault() == default(decimal)) & size.HasValue)
			{
				return 1.0;
			}
			return (double?)(LowerWick / Size);
		}
	}

	public bool IsBullish => base.Close > base.Open;

	public bool IsBearish => base.Close < base.Open;
}
[Serializable]
public class CandleResult : ResultBase
{
	public decimal? Price { get; set; }

	public Match Match { get; set; }

	public CandleProperties Candle { get; set; }

	public CandleResult(DateTime date)
	{
		base.Date = date;
		Candle = new CandleProperties();
	}
}
public enum CandlePart
{
	Open,
	High,
	Low,
	Close,
	Volume,
	HL2,
	HLC3,
	OC2,
	OHL3,
	OHLC4
}
public enum EndType
{
	Close,
	HighLow
}
public enum Match
{
	BullConfirmed = 200,
	BullSignal = 100,
	BullBasis = 10,
	Neutral = 1,
	None = 0,
	BearBasis = -10,
	BearSignal = -100,
	BearConfirmed = -200
}
public enum MaType
{
	ALMA,
	DEMA,
	EPMA,
	EMA,
	HMA,
	KAMA,
	MAMA,
	SMA,
	SMMA,
	TEMA,
	WMA
}
public enum PeriodSize
{
	Month,
	Week,
	Day,
	FourHours,
	TwoHours,
	OneHour,
	ThirtyMinutes,
	FifteenMinutes,
	FiveMinutes,
	ThreeMinutes,
	TwoMinutes,
	OneMinute
}
public enum SyncType
{
	Prepend,
	AppendOnly,
	RemoveOnly,
	FullMatch
}
public static class Pruning
{
	/// <summary> Removes a specific quantity from the beginning of the time series list.
	///       <para>
	///         See <see href="https://dotnet.StockIndicators.dev/utilities/#remove-warmup-periods?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see> for more information.
	///       </para>
	///     </summary><typeparam name="T">Any series type.</typeparam><param name="series">Collection to evaluate.</param><param name="removePeriods">Exact quantity to remove from the beginning of the series.</param><returns>Time series, pruned.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<T> RemoveWarmupPeriods<T>(this IEnumerable<T> series, int removePeriods)
	{
		if (removePeriods >= 0)
		{
			return series.Remove(removePeriods);
		}
		throw new ArgumentOutOfRangeException("removePeriods", removePeriods, "If specified, the Remove Periods value must be greater than or equal to 0.");
	}

	internal static List<T> Remove<T>(this IEnumerable<T> series, int removePeriods)
	{
		List<T> list = series.ToList();
		if (list.Count <= removePeriods)
		{
			return new List<T>();
		}
		if (removePeriods > 0)
		{
			for (int i = 0; i < removePeriods; i = checked(i + 1))
			{
				list.RemoveAt(0);
			}
		}
		return list;
	}
}
public static class Seeking
{
	/// <summary> Finds time series values on a specific date.
	///       <para>
	///         See <see href="https://dotnet.StockIndicators.dev/utilities/#find-indicator-result-by-date?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see> for more information.
	///       </para>
	///     </summary><typeparam name="TSeries">Any series type.</typeparam><param name="series">Time series to evaluate.</param><param name="lookupDate">Exact date to lookup.</param><returns>First
	///       record in the series on the date specified.</returns>
	public static TSeries? Find<TSeries>(this IEnumerable<TSeries> series, DateTime lookupDate) where TSeries : ISeries
	{
		return series.FirstOrDefault((TSeries x) => x.Date == lookupDate);
	}
}
public interface ISeries
{
	DateTime Date { get; }
}
public static class Sorting
{
	public static Collection<TSeries> ToSortedCollection<TSeries>(this IEnumerable<TSeries> series) where TSeries : ISeries
	{
		return series.OrderBy((TSeries x) => x.Date).ToCollection();
	}

	internal static List<TSeries> ToSortedList<TSeries>(this IEnumerable<TSeries> series) where TSeries : ISeries
	{
		return series.OrderBy((TSeries x) => x.Date).ToList();
	}
}
internal static class Transforms
{
	internal static Collection<T> ToCollection<T>(this IEnumerable<T> source)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		Collection<T> collection = new Collection<T>();
		foreach (T item in source)
		{
			collection.Add(item);
		}
		return collection;
	}
}
/// <summary>
/// Nullable <c>System.<see cref="T:System.Math" /></c> functions.
/// </summary>
/// <remarks>
/// <c>System.Math</c> infamously does not allow
/// or handle nullable input values.
/// Instead of adding repetitive inline defensive code,
/// we're using these equivalents.  Most are simple wrappers.
/// </remarks>
public static class NullMath
{
	/// <summary>
	/// Returns the absolute value of a nullable double.
	/// </summary>
	/// <param name="value">The nullable double value.</param>
	/// <returns>The absolute value, or null if the input is null.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double? Abs(this double? value)
	{
		if (!value.HasValue)
		{
			return null;
		}
		if (!(value.GetValueOrDefault() < 0.0))
		{
			return value;
		}
		return 0.0 - value.GetValueOrDefault();
	}

	/// <summary>
	/// Rounds a nullable decimal value to a specified number of fractional digits.
	/// </summary>
	/// <param name="value">The nullable decimal value.</param>
	/// <param name="digits">The number of fractional digits.</param>
	/// <returns>The rounded value, or null if the input is null.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal? Round(this decimal? value, int digits)
	{
		if (!value.HasValue)
		{
			return null;
		}
		return Math.Round(value.GetValueOrDefault(), digits);
	}

	/// <summary>
	/// Rounds a nullable double value to a specified number of fractional digits.
	/// </summary>
	/// <param name="value">The nullable double value.</param>
	/// <param name="digits">The number of fractional digits.</param>
	/// <returns>The rounded value, or null if the input is null.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double? Round(this double? value, int digits)
	{
		if (!value.HasValue)
		{
			return null;
		}
		return Math.Round(value.GetValueOrDefault(), digits);
	}

	/// <summary>
	/// Rounds a double value to a specified number of fractional digits.
	/// It is an extension alias of <see cref="M:System.Math.Round(System.Double,System.Int32)" />
	/// </summary>
	/// <param name="value">The double value.</param>
	/// <param name="digits">The number of fractional digits.</param>
	/// <returns>The rounded value.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Round(this double value, int digits)
	{
		return Math.Round(value, digits);
	}

	/// <summary>
	/// Rounds a decimal value to a specified number of fractional digits.
	/// It is an extension alias of <see cref="M:System.Math.Round(System.Decimal,System.Int32)" />
	/// </summary>
	/// <param name="value">The decimal value.</param>
	/// <param name="digits">The number of fractional digits.</param>
	/// <returns>The rounded value.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal Round(this decimal value, int digits)
	{
		return Math.Round(value, digits);
	}

	/// <summary>
	/// Converts a nullable double value to NaN if it is null.
	/// </summary>
	/// <param name="value">The nullable double value.</param>
	/// <returns>The value, or NaN if the input is null.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Null2NaN(this double? value)
	{
		return value ?? double.NaN;
	}

	/// <summary>
	/// Converts a nullable decimal value to NaN if it is null.
	/// </summary>
	/// <param name="value">The nullable decimal value.</param>
	/// <returns>The value as a double, or NaN if the input is null.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Null2NaN(this decimal? value)
	{
		return ((double?)value) ?? double.NaN;
	}

	/// <summary>
	/// Converts a nullable double value to null if it is NaN.
	/// </summary>
	/// <param name="value">The nullable double value.</param>
	/// <returns>The value, or null if the input is NaN.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double? NaN2Null(this double? value)
	{
		if (!value.HasValue || !double.IsNaN(value.GetValueOrDefault()))
		{
			return value;
		}
		return null;
	}

	/// <summary>
	/// Converts a double value to null if it is NaN.
	/// </summary>
	/// <param name="value">The double value.</param>
	/// <returns>The value, or null if the input is NaN.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double? NaN2Null(this double value)
	{
		if (!double.IsNaN(value))
		{
			return value;
		}
		return null;
	}
}
public static class Numerix
{
	public static double StdDev(this double[] values)
	{
		if (values == null)
		{
			throw new ArgumentNullException("values", "StdDev values cannot be null.");
		}
		double result = 0.0;
		int num = values.Length;
		checked
		{
			if (num > 1)
			{
				double num2 = 0.0;
				for (int i = 0; i < num; i++)
				{
					num2 += values[i];
				}
				double num3 = num2 / (double)num;
				double num4 = 0.0;
				for (int j = 0; j < num; j++)
				{
					double num5 = values[j];
					num4 += (num5 - num3) * (num5 - num3);
				}
				result = Math.Sqrt(num4 / (double)num);
			}
			return result;
		}
	}

	public static double Slope(double[] x, double[] y)
	{
		if (x == null)
		{
			throw new ArgumentNullException("x", "Slope X values cannot be null.");
		}
		if (y == null)
		{
			throw new ArgumentNullException("y", "Slope Y values cannot be null.");
		}
		if (x.Length != y.Length)
		{
			throw new ArgumentException("Slope X and Y arrays must be the same size.");
		}
		int num = x.Length;
		double num2 = 0.0;
		double num3 = 0.0;
		checked
		{
			for (int i = 0; i < num; i++)
			{
				num2 += x[i];
				num3 += y[i];
			}
			double num4 = num2 / (double)num;
			double num5 = num3 / (double)num;
			double num6 = 0.0;
			double num7 = 0.0;
			for (int j = 0; j < num; j++)
			{
				double num8 = x[j] - num4;
				double num9 = y[j] - num5;
				num6 += num8 * num8;
				num7 += num8 * num9;
			}
			return num7 / num6;
		}
	}

	internal static DateTime RoundDown(this DateTime dateTime, TimeSpan interval)
	{
		checked
		{
			if (!(interval == TimeSpan.Zero))
			{
				return dateTime.AddTicks(-unchecked(dateTime.Ticks % interval.Ticks));
			}
			return dateTime;
		}
	}

	internal static TimeSpan ToTimeSpan(this PeriodSize periodSize)
	{
		return periodSize switch
		{
			PeriodSize.OneMinute => TimeSpan.FromMinutes(1L), 
			PeriodSize.TwoMinutes => TimeSpan.FromMinutes(2L), 
			PeriodSize.ThreeMinutes => TimeSpan.FromMinutes(3L), 
			PeriodSize.FiveMinutes => TimeSpan.FromMinutes(5L), 
			PeriodSize.FifteenMinutes => TimeSpan.FromMinutes(15L), 
			PeriodSize.ThirtyMinutes => TimeSpan.FromMinutes(30L), 
			PeriodSize.OneHour => TimeSpan.FromHours(1), 
			PeriodSize.TwoHours => TimeSpan.FromHours(2), 
			PeriodSize.FourHours => TimeSpan.FromHours(4), 
			PeriodSize.Day => TimeSpan.FromDays(1), 
			PeriodSize.Week => TimeSpan.FromDays(7), 
			_ => TimeSpan.Zero, 
		};
	}

	internal static int GetDecimalPlaces(this decimal n)
	{
		n = Math.Abs(n);
		n -= (decimal)(int)n;
		int num = 0;
		while (n > 0m)
		{
			num = checked(num + 1);
			n *= 10m;
			n -= (decimal)(int)n;
		}
		return num;
	}
}
public enum NullTo
{
	NaN,
	Null
}
public static class QuoteUtility
{
	private static readonly CultureInfo invCulture = CultureInfo.InvariantCulture;

	/// <summary>
	///       Converts historical quotes into larger bar sizes.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/utilities/#resize-quote-history?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="newSize">PeriodSize enum representing the new bar size.</param><returns>Time series of historical quote values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<Quote> Aggregate<TQuote>(this IEnumerable<TQuote> quotes, PeriodSize newSize) where TQuote : IQuote
	{
		if (newSize != PeriodSize.Month)
		{
			TimeSpan timeSpan = newSize.ToTimeSpan();
			return quotes.Aggregate(timeSpan);
		}
		return from x in quotes
			orderby x.Date
			group x by new DateTime(x.Date.Year, x.Date.Month, 1) into x
			select new Quote
			{
				Date = x.Key,
				Open = x.First().Open,
				High = x.Max((TQuote t) => t.High),
				Low = x.Min((TQuote t) => t.Low),
				Close = x.Last().Close,
				Volume = x.Sum((TQuote t) => t.Volume)
			};
	}

	/// <summary>
	///       Converts historical quotes into larger bar sizes.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/utilities/#resize-quote-history?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="timeSpan">TimeSpan representing the new bar size.</param><returns>Time series of historical quote values.</returns><exception cref="T:System.ArgumentOutOfRangeException">Invalid parameter value provided.</exception>
	public static IEnumerable<Quote> Aggregate<TQuote>(this IEnumerable<TQuote> quotes, TimeSpan timeSpan) where TQuote : IQuote
	{
		if (timeSpan <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException("timeSpan", timeSpan, "Quotes Aggregation must use a usable new size value (see documentation for options).");
		}
		return from x in quotes
			orderby x.Date
			group x by x.Date.RoundDown(timeSpan) into x
			select new Quote
			{
				Date = x.Key,
				Open = x.First().Open,
				High = x.Max((TQuote t) => t.High),
				Low = x.Min((TQuote t) => t.Low),
				Close = x.Last().Close,
				Volume = x.Sum((TQuote t) => t.Volume)
			};
	}

	/// <summary>
	///       Optionally select which candle part to use in the calculation.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/indicators/BasicQuote/#candlepart-options?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><param name="candlePart">The OHLCV element or simply calculated value type.</param><returns>Time series of Quote tuple values.</returns><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Invalid candle part provided.</exception>
	public static IEnumerable<(DateTime Date, double Value)> Use<TQuote>(this IEnumerable<TQuote> quotes, CandlePart candlePart = CandlePart.Close) where TQuote : IQuote
	{
		return quotes.Select((TQuote x) => x.ToTuple(candlePart));
	}

	public static Collection<(DateTime, double)> ToTupleCollection<TQuote>(this IEnumerable<TQuote> quotes, CandlePart candlePart) where TQuote : IQuote
	{
		return quotes.ToTuple(candlePart).ToCollection();
	}

	internal static List<(DateTime, double)> ToTuple<TQuote>(this IEnumerable<TQuote> quotes, CandlePart candlePart) where TQuote : IQuote
	{
		return (from x in quotes
			orderby x.Date
			select x.ToTuple(candlePart)).ToList();
	}

	public static Collection<(DateTime, double)> ToSortedCollection(this IEnumerable<(DateTime date, double value)> tuples)
	{
		return tuples.ToSortedList().ToCollection();
	}

	internal static List<(DateTime, double)> ToSortedList(this IEnumerable<(DateTime date, double value)> tuples)
	{
		return tuples.OrderBy(((DateTime date, double value) x) => x.date).ToList();
	}

	internal static List<QuoteD> ToQuoteD<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		return (from x in quotes
			select new QuoteD
			{
				Date = x.Date,
				Open = (double)x.Open,
				High = (double)x.High,
				Low = (double)x.Low,
				Close = (double)x.Close,
				Volume = (double)x.Volume
			} into x
			orderby x.Date
			select x).ToList();
	}

	internal static List<(DateTime, double)> ToTuple(this List<QuoteD> qdList, CandlePart candlePart)
	{
		return (from x in qdList
			orderby x.Date
			select x.ToTuple(candlePart)).ToList();
	}

	internal static (DateTime date, double value) ToTuple<TQuote>(this TQuote q, CandlePart candlePart) where TQuote : IQuote
	{
		return candlePart switch
		{
			CandlePart.Open => (date: q.Date, value: (double)q.Open), 
			CandlePart.High => (date: q.Date, value: (double)q.High), 
			CandlePart.Low => (date: q.Date, value: (double)q.Low), 
			CandlePart.Close => (date: q.Date, value: (double)q.Close), 
			CandlePart.Volume => (date: q.Date, value: (double)q.Volume), 
			CandlePart.HL2 => (date: q.Date, value: (double)(q.High + q.Low) / 2.0), 
			CandlePart.HLC3 => (date: q.Date, value: (double)(q.High + q.Low + q.Close) / 3.0), 
			CandlePart.OC2 => (date: q.Date, value: (double)(q.Open + q.Close) / 2.0), 
			CandlePart.OHL3 => (date: q.Date, value: (double)(q.Open + q.High + q.Low) / 3.0), 
			CandlePart.OHLC4 => (date: q.Date, value: (double)(q.Open + q.High + q.Low + q.Close) / 4.0), 
			_ => throw new ArgumentOutOfRangeException("candlePart", candlePart, "Invalid candlePart provided."), 
		};
	}

	internal static BasicData ToBasicData<TQuote>(this TQuote q, CandlePart candlePart) where TQuote : IQuote
	{
		return candlePart switch
		{
			CandlePart.Open => new BasicData
			{
				Date = q.Date,
				Value = (double)q.Open
			}, 
			CandlePart.High => new BasicData
			{
				Date = q.Date,
				Value = (double)q.High
			}, 
			CandlePart.Low => new BasicData
			{
				Date = q.Date,
				Value = (double)q.Low
			}, 
			CandlePart.Close => new BasicData
			{
				Date = q.Date,
				Value = (double)q.Close
			}, 
			CandlePart.Volume => new BasicData
			{
				Date = q.Date,
				Value = (double)q.Volume
			}, 
			CandlePart.HL2 => new BasicData
			{
				Date = q.Date,
				Value = (double)(q.High + q.Low) / 2.0
			}, 
			CandlePart.HLC3 => new BasicData
			{
				Date = q.Date,
				Value = (double)(q.High + q.Low + q.Close) / 3.0
			}, 
			CandlePart.OC2 => new BasicData
			{
				Date = q.Date,
				Value = (double)(q.Open + q.Close) / 2.0
			}, 
			CandlePart.OHL3 => new BasicData
			{
				Date = q.Date,
				Value = (double)(q.Open + q.High + q.Low) / 3.0
			}, 
			CandlePart.OHLC4 => new BasicData
			{
				Date = q.Date,
				Value = (double)(q.Open + q.High + q.Low + q.Close) / 4.0
			}, 
			_ => throw new ArgumentOutOfRangeException("candlePart", candlePart, "Invalid candlePart provided."), 
		};
	}

	internal static (DateTime, double) ToTuple(this QuoteD q, CandlePart candlePart)
	{
		return candlePart switch
		{
			CandlePart.Open => (q.Date, q.Open), 
			CandlePart.High => (q.Date, q.High), 
			CandlePart.Low => (q.Date, q.Low), 
			CandlePart.Close => (q.Date, q.Close), 
			CandlePart.Volume => (q.Date, q.Volume), 
			CandlePart.HL2 => (q.Date, (q.High + q.Low) / 2.0), 
			CandlePart.HLC3 => (q.Date, (q.High + q.Low + q.Close) / 3.0), 
			CandlePart.OC2 => (q.Date, (q.Open + q.Close) / 2.0), 
			CandlePart.OHL3 => (q.Date, (q.Open + q.High + q.Low) / 3.0), 
			CandlePart.OHLC4 => (q.Date, (q.Open + q.High + q.Low + q.Close) / 4.0), 
			_ => throw new ArgumentOutOfRangeException("candlePart", candlePart, "Invalid candlePart provided."), 
		};
	}

	/// <summary>
	///       Validate historical quotes.
	///       <para>
	///         See
	///         <see href="https://dotnet.StockIndicators.dev/utilities/#validate-quote-history?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">documentation</see>
	///         for more information.
	///       </para>
	///     </summary><typeparam name="TQuote">Configurable Quote type.  See Guide for more information.</typeparam><param name="quotes">Historical price quotes.</param><returns>Time series of historical quote values.</returns><exception cref="T:Skender.Stock.Indicators.InvalidQuotesException">Validation check failed.</exception>
	public static IEnumerable<TQuote> Validate<TQuote>(this IEnumerable<TQuote> quotes) where TQuote : IQuote
	{
		List<TQuote> list = quotes.ToSortedList();
		DateTime dateTime = DateTime.MinValue;
		foreach (TQuote item in list)
		{
			if (dateTime == item.Date)
			{
				throw new InvalidQuotesException("Duplicate date found on " + item.Date.ToString("o", invCulture) + ".");
			}
			dateTime = item.Date;
		}
		return list;
	}
}
public class InvalidQuotesException : ArgumentOutOfRangeException
{
	public InvalidQuotesException()
	{
	}

	public InvalidQuotesException(string? paramName)
		: base(paramName)
	{
	}

	public InvalidQuotesException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}

	public InvalidQuotesException(string? paramName, string? message)
		: base(paramName, message)
	{
	}

	public InvalidQuotesException(string? paramName, object? actualValue, string? message)
		: base(paramName, actualValue, message)
	{
	}
}
public interface IQuote : ISeries
{
	decimal Open { get; }

	decimal High { get; }

	decimal Low { get; }

	decimal Close { get; }

	decimal Volume { get; }
}
[Serializable]
public class Quote : IQuote, ISeries
{
	public DateTime Date { get; set; }

	public decimal Open { get; set; }

	public decimal High { get; set; }

	public decimal Low { get; set; }

	public decimal Close { get; set; }

	public decimal Volume { get; set; }
}
[Serializable]
internal class QuoteD
{
	internal DateTime Date { get; set; }

	internal double Open { get; set; }

	internal double High { get; set; }

	internal double Low { get; set; }

	internal double Close { get; set; }

	internal double Volume { get; set; }
}
public interface IReusableResult : ISeries
{
	double? Value { get; }
}
[Serializable]
public abstract class ResultBase : ISeries
{
	public DateTime Date { get; set; }
}
public static class ResultUtility
{
	/// <summary>
	///       Forces indicator results to have the same date-based records as another result baseline.
	///       <para>
	///         This utility is undocumented.
	///       </para>
	///     </summary><typeparam name="TResultA">Any indicator result series type to be transformed.</typeparam><typeparam name="TResultB">Any indicator result series type to be matched.</typeparam><param name="syncMe">The indicator result series to be modified.</param><param name="toMatch">The indicator result series to compare for matching.</param><param name="syncType">Synchronization behavior  See options in SyncType enum.</param><returns>Indicator result series, synchronized to a comparator match.</returns><exception cref="T:System.ArgumentOutOfRangeException">
	///       Invalid parameter value provided.
	///     </exception>
	public static IEnumerable<TResultA> SyncIndex<TResultA, TResultB>(this IEnumerable<TResultA> syncMe, IEnumerable<TResultB> toMatch, SyncType syncType = SyncType.FullMatch) where TResultA : ISeries where TResultB : ISeries
	{
		List<TResultA> list = syncMe.ToSortedList();
		List<TResultB> list2 = toMatch.ToSortedList();
		if (list.Count == 0 || list2.Count == 0)
		{
			return new List<TResultA>();
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		switch (syncType)
		{
		case SyncType.Prepend:
			flag = true;
			break;
		case SyncType.AppendOnly:
			flag = (flag2 = true);
			break;
		case SyncType.RemoveOnly:
			flag3 = true;
			break;
		case SyncType.FullMatch:
			flag = (flag2 = (flag3 = true));
			break;
		default:
			throw new ArgumentOutOfRangeException("syncType");
		}
		Type type = list[0].GetType();
		if (flag || flag2)
		{
			List<TResultA> list3 = new List<TResultA>();
			for (int i = 0; i < list2.Count; i = checked(i + 1))
			{
				TResultB val = list2[i];
				if (list.Find(val.Date) == null)
				{
					TResultA val2 = (TResultA)Activator.CreateInstance(type, val.Date);
					if (val2 != null)
					{
						list3.Add(val2);
					}
				}
				else if (!flag2)
				{
					break;
				}
			}
			list.AddRange(list3);
		}
		if (flag3)
		{
			List<TResultA> list4 = new List<TResultA>();
			for (int j = 0; j < list.Count; j = checked(j + 1))
			{
				TResultA item = list[j];
				if (list2.Find(item.Date) == null)
				{
					list4.Add(item);
				}
			}
			list.RemoveAll(list4.Contains);
		}
		return list.ToSortedList();
	}

	/// <summary> Removes non-essential records containing null or NaN values. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#condense?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><typeparam name="TResult">Any result
	///       type.</typeparam><param name="results">Indicator results to evaluate.</param><returns>Time series of indicator results,
	///       condensed.</returns>
	public static IEnumerable<TResult> Condense<TResult>(this IEnumerable<TResult> results) where TResult : IReusableResult
	{
		List<TResult> list = results.ToList();
		list.RemoveAll(delegate(TResult x)
		{
			double? value = x.Value;
			return (!value.HasValue || double.IsNaN(value.GetValueOrDefault())) ? true : false;
		});
		return list.ToSortedList();
	}

	/// <summary>Converts results into a reusable tuple with warmup periods removed and nulls converted
	///       to NaN. <para> See <see href="https://dotnet.StockIndicators.dev/utilities/#using-tuple-results?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="reusable">Indicator results to evaluate.</param><returns>Collection of non-nullable tuple time series of results, without null warmup periods.</returns>
	public static Collection<(DateTime Date, double Value)> ToTupleChainable(this IEnumerable<IReusableResult> reusable)
	{
		return reusable.ToTuple().ToCollection();
	}

	internal static List<(DateTime Date, double Value)> ToTuple(this IEnumerable<IReusableResult> reusable)
	{
		List<(DateTime, double)> list = new List<(DateTime, double)>();
		List<IReusableResult> list2 = reusable.ToList();
		for (int num = list2.FindIndex((IReusableResult x) => x.Value.HasValue); num < list2.Count; num = checked(num + 1))
		{
			IReusableResult reusableResult = list2[num];
			list.Add((reusableResult.Date, reusableResult.Value.Null2NaN()));
		}
		return list.OrderBy<(DateTime, double), DateTime>(((DateTime date, double value) x) => x.date).ToList();
	}

	/// <summary>Converts results into a tuple collection with non-nullable NaN to replace null values. <para>
	///       See <see href="https://dotnet.StockIndicators.dev/utilities/#using-tuple-results?utm_source=library&amp;utm_medium=inline-help&amp;utm_campaign=embedded">
	///       documentation</see> for more information. </para>
	///     </summary><param name="reusable">Indicator results to evaluate.</param><returns>Collection of tuple time series of
	///       results with specified handling of nulls, without pruning.</returns>
	public static Collection<(DateTime Date, double Value)> ToTupleNaN(this IEnumerable<IReusableResult> reusable)
	{
		List<IReusableResult> list = reusable.ToSortedList();
		int count = list.Count;
		Collection<(DateTime, double)> collection = new Collection<(DateTime, double)>();
		for (int i = 0; i < count; i = checked(i + 1))
		{
			IReusableResult reusableResult = list[i];
			collection.Add((reusableResult.Date, reusableResult.Value.Null2NaN()));
		}
		return collection;
	}
}
