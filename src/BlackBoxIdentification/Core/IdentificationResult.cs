namespace BlackBoxIdentification.Core;

public sealed class IdentificationResult
{
    public required double[] LinearCoefficients { get; init; }
    public required double[,] QuadraticCoefficients { get; init; }
    public required double[] ModelOutput { get; init; }
    public required double[] Residual { get; init; }
    public required double Rmse { get; init; }
    public required double RelativeErrorPercent { get; init; }
    public required double MaxAbsoluteError { get; init; }

    public required int ParameterCount { get; init; }
    public required int EquationCount { get; init; }

    public required bool IsNormalized { get; init; }
    public required double InputMean { get; init; }
    public required double InputScale { get; init; }
    public required double OutputMean { get; init; }
    public required double OutputScale { get; init; }
}
