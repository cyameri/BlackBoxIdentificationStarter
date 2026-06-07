using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class LeastSquaresIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        int[] sampleIndexes = DesignMatrixBuilder.SelectLeastSquaresIndexes(data, parameters);
        var (matrix, target, _) = DesignMatrixBuilder.Build(data, parameters, sampleIndexes);

        // Малое регуляризирующее слагаемое стабилизирует решение при близких
        // или зависимых столбцах матрицы.
        var solution = LinearAlgebra.SolveLeastSquares(matrix, target, regularization: 1e-8);

        return ResultFactory.Create(data, parameters, solution);
    }
}
