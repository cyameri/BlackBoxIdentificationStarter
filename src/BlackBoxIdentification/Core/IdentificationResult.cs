namespace BlackBoxIdentification.Core;

public sealed class IdentificationResult
{
    public required double[] LinearCoefficients { get; init; }
    public required double[,] QuadraticCoefficients { get; init; }
    public required double[] ModelOutput { get; init; }
    public required double[] Residual { get; init; }
    public required double Rmse { get; init; }
}
