using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class LeastSquaresIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        var (matrix, target, _) = DesignMatrixBuilder.Build(data, parameters);
        var solution = LinearAlgebra.SolveLeastSquares(matrix, target);
        return ResultFactory.Create(data, parameters, solution);
    }
}
