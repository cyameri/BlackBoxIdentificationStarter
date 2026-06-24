using System;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class DesignMatrixBuilder
{
    public static int GetParameterCount(IdentificationParameters parameters)
    {
        ValidateParameters(parameters);
        return parameters.ParameterCount;
    }

    /// <summary>
    /// Узлы классической коллокационной схемы из Maple:
    /// t_k = k / N, k = 1, ..., N,
    /// N = m + m1*m2.
    /// </summary>
    public static double[] GetCollocationTimes(IdentificationParameters parameters)
    {
        int count = GetParameterCount(parameters);
        var times = new double[count];

        for (int k = 1; k <= count; k++)
            times[k - 1] = k / (double)count;

        return times;
    }

    public static double[] GetLeastSquaresTimes(SignalData data, IdentificationParameters parameters)
    {
        int parameterCount = GetParameterCount(parameters);
        int requested = Math.Max(parameters.LeastSquaresPointCount, parameterCount + 1);
        int count = Math.Min(requested, data.Count);

        if (count <= parameterCount)
        {
            throw new InvalidOperationException(
                $"Для МНК требуется больше точек, чем неизвестных коэффициентов. " +
                $"Неизвестных: {parameterCount}, доступно точек: {data.Count}.");
        }

        var times = new double[count];
        for (int k = 1; k <= count; k++)
            times[k - 1] = k / (double)count;

        return times;
    }

    public static (double[,] Matrix, double[] Target, double[] SampleTimes) Build(
        SignalData data,
        IdentificationParameters parameters,
        double[] sampleTimes)
    {
        ValidateParameters(parameters);

        if (data.Count < parameters.ParameterCount)
        {
            throw new InvalidOperationException(
                $"Недостаточно исходных точек для выбранной модели. " +
                $"Точек данных: {data.Count}, неизвестных коэффициентов: {parameters.ParameterCount}.");
        }

        int parameterCount = parameters.ParameterCount;
        var matrix = new double[sampleTimes.Length, parameterCount];
        var target = new double[sampleTimes.Length];
        var interpolator = new SignalInterpolator(data);

        for (int row = 0; row < sampleTimes.Length; row++)
        {
            double time = Math.Clamp(sampleTimes[row], 0.0, 1.0);
            double[] features = BuildFeatureVectorAtTime(interpolator, parameters, time);

            for (int column = 0; column < parameterCount; column++)
                matrix[row, column] = features[column];

            target[row] = interpolator.Output(time);
        }

        MathValidation.EnsureFinite(matrix, "матрица системы");
        MathValidation.EnsureFinite(target, "вектор правой части");

        return (matrix, target, sampleTimes);
    }

    public static double[] BuildFeatureVectorAtTime(
        SignalData data,
        IdentificationParameters parameters,
        double time)
    {
        return BuildFeatureVectorAtTime(new SignalInterpolator(data), parameters, time);
    }

    public static double[] BuildFeatureVectorAtTime(
        SignalInterpolator interpolator,
        IdentificationParameters parameters,
        double time)
    {
        ValidateParameters(parameters);
        time = Math.Clamp(time, 0.0, 1.0);

        int maximumOrder = Math.Max(
            parameters.FirstKernelBasisCount,
            Math.Max(parameters.SecondKernelBasisCountS1, parameters.SecondKernelBasisCountS2));

        double[] beta = ComputeBetaValues(
            interpolator,
            time,
            maximumOrder,
            parameters.QuadratureIntervals);

        var features = new double[parameters.ParameterCount];
        int column = 0;

        for (int i = 0; i < parameters.FirstKernelBasisCount; i++)
            features[column++] = beta[i];

        for (int i = 0; i < parameters.SecondKernelBasisCountS1; i++)
        {
            for (int j = 0; j < parameters.SecondKernelBasisCountS2; j++)
            {
                // Двойной интеграл с разделяемым базисом раскладывается в произведение
                // двух одномерных интегральных коэффициентов.
                features[column++] = beta[i] * beta[j];
            }
        }

        return features;
    }

    private static double[] ComputeBetaValues(
        SignalInterpolator interpolator,
        double time,
        int order,
        int requestedIntervals)
    {
        var beta = new double[order];

        if (time <= 0.0)
            return beta;

        int intervals = Math.Max(20, requestedIntervals);
        if ((intervals & 1) != 0)
            intervals++;

        double h = time / intervals;

        for (int q = 0; q <= intervals; q++)
        {
            double s = q * h;
            double inputValue = interpolator.Input(time - s);
            double weight;

            if (q == 0 || q == intervals)
                weight = 1.0;
            else if ((q & 1) == 0)
                weight = 2.0;
            else
                weight = 4.0;

            for (int i = 0; i < order; i++)
                beta[i] += weight * ChebyshevBasis.T(i, s) * inputValue;
        }

        double multiplier = h / 3.0;
        for (int i = 0; i < order; i++)
            beta[i] *= multiplier;

        return beta;
    }

    private static void ValidateParameters(IdentificationParameters parameters)
    {
        if (parameters.FirstKernelBasisCount <= 0)
            throw new InvalidOperationException("Количество базисных функций K1 должно быть положительным.");

        if (parameters.SecondKernelBasisCountS1 <= 0 || parameters.SecondKernelBasisCountS2 <= 0)
            throw new InvalidOperationException("Количество базисных функций K2 должно быть положительным.");

        if (parameters.ParameterCount > 400)
            throw new InvalidOperationException("Выбрано слишком большое число коэффициентов. Уменьшите m, m1 или m2.");
    }
}
