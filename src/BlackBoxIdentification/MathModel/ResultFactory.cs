using System;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class ResultFactory
{
    public static IdentificationResult Create(SignalData data, IdentificationParameters p, double[] solution)
    {
        MathValidation.EnsureFinite(solution, "вектор коэффициентов");

        int expected = DesignMatrixBuilder.GetParameterCount(p);
        if (solution.Length != expected)
            throw new InvalidOperationException($"Размер вектора коэффициентов не соответствует модели. Ожидалось {expected}, получено {solution.Length}.");

        double h0 = solution[0];
        var a = solution.Skip(1).Take(p.LinearOrder).ToArray();
        var c = new double[p.QuadraticOrder, p.QuadraticOrder];

        int k = 1 + p.LinearOrder;
        for (int i = 0; i < p.QuadraticOrder; i++)
        {
            for (int j = 0; j < p.QuadraticOrder; j++)
                c[i, j] = solution[k++];
        }

        var modelOutput = VolterraModelEvaluator.Evaluate(data, p, h0, a, c);
        MathValidation.EnsureFinite(modelOutput, "восстановленный выходной сигнал");

        var residual = new double[data.Count];

        double sumSquares = 0.0;
        double outputNormSquares = 0.0;
        double maxAbsoluteError = 0.0;

        for (int i = 0; i < data.Count; i++)
        {
            double y = data.Points[i].Output;
            residual[i] = y - modelOutput[i];

            if (!MathValidation.IsFinite(residual[i]))
                throw new InvalidOperationException("При расчете невязки получено некорректное значение.");

            double abs = Math.Abs(residual[i]);
            if (abs > maxAbsoluteError)
                maxAbsoluteError = abs;

            sumSquares += residual[i] * residual[i];
            outputNormSquares += y * y;
        }

        double rmse = Math.Sqrt(sumSquares / data.Count);
        double relative = outputNormSquares > 0
            ? Math.Sqrt(sumSquares / outputNormSquares) * 100.0
            : 0.0;

        if (!MathValidation.IsFinite(rmse) || !MathValidation.IsFinite(relative) || !MathValidation.IsFinite(maxAbsoluteError))
            throw new InvalidOperationException("Не удалось корректно рассчитать показатели ошибки.");

        return new IdentificationResult
        {
            ConstantCoefficient = h0,
            LinearCoefficients = a,
            QuadraticCoefficients = c,
            ModelOutput = modelOutput,
            Residual = residual,
            Rmse = rmse,
            RelativeErrorPercent = relative,
            MaxAbsoluteError = maxAbsoluteError
        };
    }
}
