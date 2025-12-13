using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib.Tests;

public static class ValidationHelper
{
    public static void VerifyData<TResult>(TSeries qSeries, List<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = 1e-6)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int start = count - skip;

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData<TResult>(List<double> qResults, List<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = 1e-6)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int start = count - skip;

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData<TResult>(double[] qOutput, List<TResult> sSeries, Func<TResult, double?> selector, int skip = 100, double tolerance = 1e-6)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int start = count - skip;

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = selector(sSeries[i]);

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, tolerance);
        }
    }

    public static void VerifyData(TSeries qSeries, double[] tOutput, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qSeries.Count;
        int start = count - skip;

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

    public static void VerifyData(List<double> qResults, double[] tOutput, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qResults.Count;
        int start = count - skip;

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

    public static void VerifyData(double[] qOutput, double[] tOutput, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qOutput.Length;
        int start = count - skip;

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

    public static void VerifyData(TSeries qSeries, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qSeries.Count;
        int start = count - skip;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = start; i < count; i++)
        {
            double qValue = qSeries[i].Value;

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(List<double> qResults, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qResults.Count;
        int start = count - skip;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = start; i < count; i++)
        {
            double qValue = qResults[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, tolerance);
        }
    }

    public static void VerifyData(double[] qOutput, double[] tOutput, Range outRange, int lookback, int skip = 100, double tolerance = 1e-6)
    {
        int count = qOutput.Length;
        int start = count - skip;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = start; i < count; i++)
        {
            double qValue = qOutput[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, tolerance);
        }
    }
}
