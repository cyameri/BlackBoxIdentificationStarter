using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class CollocationIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        SignalData unitTimeData = SignalPreprocessor.NormalizeTimeToUnitInterval(data);
        NormalizedSignalData normalized = SignalNormalizer.NormalizeForIdentification(
            unitTimeData,
            parameters.NormalizeSignals);

        double[] nodes = DesignMatrixBuilder.GetCollocationTimes(parameters);
        var (matrix, target, _) = DesignMatrixBuilder.Build(normalized.Data, parameters, nodes);

        // Классическая коллокация: число уравнений равно числу неизвестных.
        // SVD используется только как численно устойчивый способ решения квадратной системы.
        double[] solution = LinearAlgebra.SolveSquareSystem(matrix, target);

        return ResultFactory.Create(
            unitTimeData,
            normalized.Data,
            parameters,
            solution,
            normalized.Info,
            nodes.Length);
    }
}
