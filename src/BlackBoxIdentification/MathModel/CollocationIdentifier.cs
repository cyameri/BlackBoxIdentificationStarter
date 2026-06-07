using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class CollocationIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        int[] sampleIndexes = DesignMatrixBuilder.SelectCollocationIndexes(data, parameters);
        var (matrix, target, _) = DesignMatrixBuilder.Build(data, parameters, sampleIndexes);

        // Коллокационная система может быть плохо обусловленной, поэтому решаем ее
        // стабилизированным способом. Если пользователь задаст N равным числу неизвестных,
        // получится близкая к классической квадратная постановка.
        var solution = LinearAlgebra.SolveSquareSystem(matrix, target, regularization: 1e-6);

        return ResultFactory.Create(data, parameters, solution);
    }
}
