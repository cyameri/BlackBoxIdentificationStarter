using System;
using System.Collections.Generic;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class SignalNormalizationInfo
{
    public required bool Enabled { get; init; }
    public required double InputMean { get; init; }
    public required double InputScale { get; init; }
    public required double OutputMean { get; init; }
    public required double OutputScale { get; init; }
}

public sealed class NormalizedSignalData
{
    public required SignalData Data { get; init; }
    public required SignalNormalizationInfo Info { get; init; }
}

public static class SignalNormalizer
{
    public static NormalizedSignalData NormalizeForIdentification(SignalData source, bool enabled)
    {
        if (!enabled)
        {
            return new NormalizedSignalData
            {
                Data = source,
                Info = new SignalNormalizationInfo
                {
                    Enabled = false,
                    InputMean = 0.0,
                    InputScale = 1.0,
                    OutputMean = 0.0,
                    OutputScale = 1.0
                }
            };
        }

        double inputMean = Mean(source, p => p.Input);
        double outputMean = Mean(source, p => p.Output);

        double inputScale = StandardDeviation(source, p => p.Input, inputMean);
        double outputScale = StandardDeviation(source, p => p.Output, outputMean);

        // Если сигнал почти постоянный, деление на очень маленькое число только ухудшит устойчивость.
        if (inputScale < 1e-12) inputScale = 1.0;
        if (outputScale < 1e-12) outputScale = 1.0;

        var points = new List<SignalPoint>(source.Count);
        foreach (var point in source.Points)
        {
            points.Add(new SignalPoint(
                point.Time,
                (point.Input - inputMean) / inputScale,
                (point.Output - outputMean) / outputScale));
        }

        return new NormalizedSignalData
        {
            Data = new SignalData(points),
            Info = new SignalNormalizationInfo
            {
                Enabled = true,
                InputMean = inputMean,
                InputScale = inputScale,
                OutputMean = outputMean,
                OutputScale = outputScale
            }
        };
    }

    public static double RestoreOutput(double normalizedValue, SignalNormalizationInfo info)
    {
        return info.Enabled
            ? info.OutputMean + normalizedValue * info.OutputScale
            : normalizedValue;
    }

    private static double Mean(SignalData data, Func<SignalPoint, double> selector)
    {
        if (data.Count == 0)
            return 0.0;

        double sum = 0.0;
        foreach (var point in data.Points)
            sum += selector(point);

        return sum / data.Count;
    }

    private static double StandardDeviation(SignalData data, Func<SignalPoint, double> selector, double mean)
    {
        if (data.Count == 0)
            return 1.0;

        double sum = 0.0;
        foreach (var point in data.Points)
        {
            double diff = selector(point) - mean;
            sum += diff * diff;
        }

        return Math.Sqrt(sum / data.Count);
    }
}
