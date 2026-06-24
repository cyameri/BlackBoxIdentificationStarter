using System;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class ResultFactory
{
    public static IdentificationResult Create(
        SignalData originalData,
        SignalData calculationData,
        IdentificationParameters parameters,
        double[] solution,
        SignalNormalizationInfo normalization,
        int equationCount)
    {
        MathValidation.EnsureFinite(solution, "вектор коэффициентов");

        int expected = parameters.ParameterCount;
        if (solution.Length != expected)
        {
            throw new InvalidOperationException(
                $"Размер решения не соответствует модели. Ожидалось {expected}, получено {solution.Length}.");
        }

        double[] linear = solution
            .Take(parameters.FirstKernelBasisCount)
            .ToArray();

        var quadratic = new double[
            parameters.SecondKernelBasisCountS1,
            parameters.SecondKernelBasisCountS2];

        int index = parameters.FirstKernelBasisCount;
        for (int i = 0; i < parameters.SecondKernelBasisCountS1; i++)
        {
            for (int j = 0; j < parameters.SecondKernelBasisCountS2; j++)
                quadratic[i, j] = solution[index++];
        }

        double[] calculatedOutput = VolterraModelEvaluator.Evaluate(
            calculationData,
            parameters,
            linear,
            quadratic);

        MathValidation.EnsureFinite(calculatedOutput, "восстановленный выход");

        var modelOutput = new double[originalData.Count];
        var residual = new double[originalData.Count];

        double sumSquares = 0.0;
        double outputSquares = 0.0;
        double maxAbsoluteError = 0.0;

        for (int i = 0; i < originalData.Count; i++)
        {
            modelOutput[i] = SignalNormalizer.RestoreOutput(calculatedOutput[i], normalization);
            double actual = originalData.Points[i].Output;
            residual[i] = actual - modelOutput[i];

            double absolute = Math.Abs(residual[i]);
            if (absolute > maxAbsoluteError)
                maxAbsoluteError = absolute;

            sumSquares += residual[i] * residual[i];
            outputSquares += actual * actual;
        }

        double rmse = Math.Sqrt(sumSquares / originalData.Count);
        double relative = outputSquares > 1e-14
            ? Math.Sqrt(sumSquares / outputSquares) * 100.0
            : 0.0;

        return new IdentificationResult
        {
            LinearCoefficients = linear,
            QuadraticCoefficients = quadratic,
            ModelOutput = modelOutput,
            Residual = residual,
            Rmse = rmse,
            RelativeErrorPercent = relative,
            MaxAbsoluteError = maxAbsoluteError,
            ParameterCount = parameters.ParameterCount,
            EquationCount = equationCount,
            IsNormalized = normalization.Enabled,
            InputMean = normalization.InputMean,
            InputScale = normalization.InputScale,
            OutputMean = normalization.OutputMean,
            OutputScale = normalization.OutputScale
        };
    }
}
