using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Provides validation utilities for comparing indicator results against external libraries.
/// Contains tolerance constants and verification methods for cross-library validation.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Default tolerance for floating-point comparisons (1e-7).
    /// Suitable for most indicator comparisons.
    /// </summary>
    public const double DefaultTolerance = 1e-7;

    /// <summary>
    /// Tolerance for Ooples Finance library comparisons (1e-7).
    /// May need adjustment for specific indicators with different internal precision.
    /// </summary>
    public const double OoplesTolerance = 1e-7;

    /// <summary>
    /// Tolerance for Skender.Stock.Indicators library comparisons (1e-7).
    /// Skender uses decimal internally, so some precision loss is expected.
    /// </summary>
    public const double SkenderTolerance = 1e-7;

    /// <summary>
    /// Tolerance for TA-Lib (TALib.NETCore) library comparisons (1e-7).
    /// TA-Lib uses double precision throughout.
    /// </summary>
    public const double TalibTolerance = 1e-7;

    /// <summary>
    /// Tolerance for Tulip library comparisons (1e-7).
    /// Note: Tulip may have 1-bar shifts due to different initialization strategies.
    /// </summary>
    public const double TulipTolerance = 1e-7;

    /// <summary>
    /// Relative tolerance for percentage-based comparisons (0.5%).
    /// Use when absolute tolerance is not appropriate.
    /// </summary>
    public const double RelativeTolerance = 0.005;

    /// <summary>
    /// Default number of bars to verify from the end of the series.
    /// Using 100 bars ensures we're comparing converged values.
    /// </summary>
    public const int DefaultVerificationCount = 100;

    /// <summary>
    /// Verifies TSeries results against an external library's results.
    /// Compares the last 'skip' values by default.
    /// </summary>
    /// <typeparam name="TResult">The type of results from the external library</typeparam>
    /// <param name="qSeries">QuanTAlib TSeries results</param>
    /// <param name="sSeries">External library results</param>
    /// <param name="selector">Function to extract the comparable value from external results</param>
    /// <param name="skip">Number of values to verify from the end (default: 100)</param>
    /// <param name="tolerance">Tolerance for floating-point comparison</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData<TResult>(
        TSeries qSeries,
        IReadOnlyList<TResult> sSeries,
        Func<TResult, double?> selector,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.True(
                Math.Abs(qValue - sValue.Value) <= tolerance,
                $"Mismatch at index {i}: QuanTAlib={qValue:G17}, External={sValue.Value:G17}, Diff={Math.Abs(qValue - sValue.Value):G17}");
        }
    }

    /// <summary>
    /// Verifies IReadOnlyList results against an external library's results.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData<TResult>(
        IReadOnlyList<double> qResults,
        IReadOnlyList<TResult> sSeries,
        Func<TResult, double?> selector,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.True(
                Math.Abs(qValue - sValue.Value) <= tolerance,
                $"Mismatch at index {i}: QuanTAlib={qValue:G17}, External={sValue.Value:G17}, Diff={Math.Abs(qValue - sValue.Value):G17}");
        }
    }

    /// <summary>
    /// Verifies double array results against an external library's results.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData<TResult>(
        double[] qOutput,
        IReadOnlyList<TResult> sSeries,
        Func<TResult, double?> selector,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.True(
                Math.Abs(qValue - sValue.Value) <= tolerance,
                $"Mismatch at index {i}: QuanTAlib={qValue:G17}, External={sValue.Value:G17}, Diff={Math.Abs(qValue - sValue.Value):G17}");
        }
    }

    /// <summary>
    /// Verifies TSeries results against TA-Lib style output with lookback offset.
    /// </summary>
    /// <param name="qSeries">QuanTAlib TSeries results</param>
    /// <param name="tOutput">TA-Lib output array</param>
    /// <param name="lookback">TA-Lib lookback period (output is shifted by this amount)</param>
    /// <param name="skip">Number of values to verify from the end</param>
    /// <param name="tolerance">Tolerance for floating-point comparison</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        TSeries qSeries,
        double[] tOutput,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qSeries.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies IReadOnlyList results against TA-Lib style output with lookback offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        IReadOnlyList<double> qResults,
        double[] tOutput,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qResults.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies double array results against TA-Lib style output with lookback offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        double[] qOutput,
        double[] tOutput,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qOutput.Length;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies TSeries results against TA-Lib style output with range and lookback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        TSeries qSeries,
        double[] tOutput,
        Range outRange,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qSeries.Count;
        int start = Math.Max(0, count - skip);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;

            if (i < lookback) continue;

            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies IReadOnlyList results against TA-Lib style output with range and lookback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        IReadOnlyList<double> qResults,
        double[] tOutput,
        Range outRange,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qResults.Count;
        int start = Math.Max(0, count - skip);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];

            if (i < lookback) continue;

            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies double array results against TA-Lib style output with range and lookback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VerifyData(
        double[] qOutput,
        double[] tOutput,
        Range outRange,
        int lookback,
        int skip = DefaultVerificationCount,
        double tolerance = DefaultTolerance)
    {
        int count = qOutput.Length;
        int start = Math.Max(0, count - skip);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];

            if (i < lookback) continue;

            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) continue;

            double tValue = tOutput[tIndex];

            Assert.True(
                Math.Abs(qValue - tValue) <= tolerance,
                $"Mismatch at index {i} (TA-Lib index {tIndex}): QuanTAlib={qValue:G17}, TA-Lib={tValue:G17}, Diff={Math.Abs(qValue - tValue):G17}");
        }
    }

    /// <summary>
    /// Verifies that all values in the series are finite (not NaN or Infinity).
    /// </summary>
    /// <param name="series">The series to verify</param>
    /// <param name="startIndex">Starting index for verification (default: 0)</param>
    public static void VerifyAllFinite(TSeries series, int startIndex = 0)
    {
        for (int i = startIndex; i < series.Count; i++)
        {
            Assert.True(
                double.IsFinite(series[i].Value),
                $"Non-finite value at index {i}: {series[i].Value}");
        }
    }

    /// <summary>
    /// Verifies that all values in the array are finite (not NaN or Infinity).
    /// </summary>
    /// <param name="values">The array to verify</param>
    /// <param name="startIndex">Starting index for verification (default: 0)</param>
    public static void VerifyAllFinite(double[] values, int startIndex = 0)
    {
        for (int i = startIndex; i < values.Length; i++)
        {
            Assert.True(
                double.IsFinite(values[i]),
                $"Non-finite value at index {i}: {values[i]}");
        }
    }

    /// <summary>
    /// Verifies that two series produce the same results (for consistency testing).
    /// </summary>
    /// <param name="series1">First series</param>
    /// <param name="series2">Second series</param>
    /// <param name="tolerance">Tolerance for floating-point comparison</param>
    public static void VerifySeriesEqual(TSeries series1, TSeries series2, double tolerance = DefaultTolerance)
    {
        Assert.Equal(series1.Count, series2.Count);

        for (int i = 0; i < series1.Count; i++)
        {
            Assert.True(
                Math.Abs(series1[i].Value - series2[i].Value) <= tolerance,
                $"Mismatch at index {i}: Series1={series1[i].Value:G17}, Series2={series2[i].Value:G17}");
        }
    }

    /// <summary>
    /// Calculates the maximum absolute difference between two series.
    /// Useful for debugging tolerance issues.
    /// </summary>
    public static double MaxAbsoluteDifference<TResult>(
        TSeries qSeries,
        IReadOnlyList<TResult> sSeries,
        Func<TResult, double?> selector)
    {
        if (qSeries.Count != sSeries.Count)
            throw new ArgumentException("Series must have the same count", nameof(sSeries));

        double maxDiff = 0;

        for (int i = 0; i < qSeries.Count; i++)
        {
            double? sValue = selector(sSeries[i]);
            if (!sValue.HasValue) continue;

            double diff = Math.Abs(qSeries[i].Value - sValue.Value);
            if (diff > maxDiff)
                maxDiff = diff;
        }

        return maxDiff;
    }

    /// <summary>
    /// Calculates the maximum relative difference between two series.
    /// Useful for percentage-based tolerance testing.
    /// </summary>
    public static double MaxRelativeDifference<TResult>(
        TSeries qSeries,
        IReadOnlyList<TResult> sSeries,
        Func<TResult, double?> selector)
    {
        if (qSeries.Count != sSeries.Count)
            throw new ArgumentException("Series must have the same count", nameof(sSeries));

        double maxDiff = 0;

        for (int i = 0; i < qSeries.Count; i++)
        {
            double? sValue = selector(sSeries[i]);
            if (!sValue.HasValue || sValue.Value == 0) continue;

            double relDiff = Math.Abs((qSeries[i].Value - sValue.Value) / sValue.Value);
            if (relDiff > maxDiff)
                maxDiff = relDiff;
        }

        return maxDiff;
    }
}
