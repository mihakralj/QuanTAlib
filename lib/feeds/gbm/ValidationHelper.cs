using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib.Tests;

public static class ValidationHelper
{
    public const double DefaultTolerance = 1e-7;
    public const double OoplesTolerance = 1e-7;
    public const double SkenderTolerance = 1e-7;
    public const double TalibTolerance = 1e-7;
    public const double TulipTolerance = 1e-7;
    public const double RelativeTolerance = 0.005;

    public static void VerifyData<TResult>(TSeries qSeries, IReadOnlyList<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = DefaultTolerance)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData<TResult>(IReadOnlyList<double> qResults, IReadOnlyList<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = DefaultTolerance)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData<TResult>(double[] qOutput, List<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = DefaultTolerance)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int start = Math.Max(0, count - skip);

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData(TSeries qSeries, double[] tOutput, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(IReadOnlyList<double> qResults, double[] tOutput, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(double[] qOutput, double[] tOutput, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(TSeries qSeries, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(IReadOnlyList<double> qResults, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(double[] qOutput, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = DefaultTolerance)
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

            Assert.Equal(tValue, qValue, tolerance);
        }
    }
}
