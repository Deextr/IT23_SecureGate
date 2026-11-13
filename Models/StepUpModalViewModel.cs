namespace SecureGate1.Models;

public sealed class StepUpModalViewModel
{
    public bool Required { get; init; }
    public string? Error { get; init; }
    public string Code { get; init; } = "";
}
