namespace BlackBoxIdentification.Core;

public sealed class IdentificationParameters
{
    public IdentificationMethod Method { get; init; } = IdentificationMethod.Collocation;
    public int MemoryLength { get; init; } = 50;
    public int LinearOrder { get; init; } = 3;
    public int QuadraticOrder { get; init; } = 3;
    public int NodeCount { get; init; } = 40;
}
