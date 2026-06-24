using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class LeastSquaresIdentifier : IIdentifier
{
    public IdentificationResult Identify(SignalData data, IdentificationParameters parameters)
    {
        SignalData unitTimeData = SignalPreprocessor.NormalizeTimeToUnitInterval(data);
        NormalizedSignalData normalized = SignalNormalizer.NormalizeForIdentification(
            unitTimeData,
            parameters.NormalizeSignals);

        double[] sampleTimes = DesignMatrixBuilder.GetLeastSquaresTimes(normalized.Data, parameters);
        var (matrix, target, _) = DesignMatrixBuilder.Build(normalized.Data, parameters, sampleTimes);

        double[] solution = LinearAlgebra.SolveLeastSquares(matrix, target, regularization: 1e-8);

        return ResultFactory.Create(
            unitTimeData,
            normalized.Data,
            parameters,
            solution,
            normalized.Info,
            sampleTimes.Length);
    }
}
