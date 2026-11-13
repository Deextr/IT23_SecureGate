namespace SecureGate1.Models;

public sealed class RecoverySubmitModalModel
{
    public string? ErrorMessage { get; init; }
    public RecoverySubmitFilterState? PreserveFilters { get; init; }
}

public sealed class RecoverySubmitFilterState
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Role { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public int PageNumber { get; init; } = 1;
}
