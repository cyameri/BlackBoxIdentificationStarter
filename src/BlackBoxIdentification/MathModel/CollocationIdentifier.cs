using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class CollocationIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        // Временная реализация: общий матричный расчёт.
        // Сюда потом переносим точную коллокацию из Maple.
        var (matrix, target, _) = DesignMatrixBuilder.Build(data, parameters);
        var solution = LinearAlgebra.SolveLeastSquares(matrix, target);
        return ResultFactory.Create(data, parameters, solution);
    }
}
