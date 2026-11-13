namespace SecureGate1.Services;

public enum DashboardKind
{
    Administrator,
    SecurityOfficer,
    Employee
}

public sealed class DashboardChartSeries
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<int> Values { get; init; } = [];
}

public sealed class DashboardEventItem
{
    public string When { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string? Href { get; init; }
}

public sealed class DashboardAlertItem
{
    public string Title { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Status { get; init; } = "";
    public string When { get; init; } = "";
    public string? Href { get; init; }
}

public sealed class DashboardRequestItem
{
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string When { get; init; } = "";
    public string? Href { get; init; }
}

public sealed class AdminDashboardDto
{
    public int TotalAccounts { get; init; }
    public int ActiveAccounts { get; init; }
    public int DisabledAccounts { get; init; }
    public int Administrators { get; init; }
    public int SecurityOfficers { get; init; }
    public int Employees { get; init; }
    public int PendingRecovery { get; init; }
    public int AlertsInRange { get; init; }
    public int OpenAlerts { get; init; }
    public int OngoingInvestigations { get; init; }
    public int AuditEventsInRange { get; init; }
    public int AccountsCreatedInRange { get; init; }
    public DashboardChartSeries ActivityTrend { get; init; } = new();
    public DashboardChartSeries AccountOverview { get; init; } = new();
    public DashboardChartSeries AlertSeverity { get; init; } = new();
    public DashboardChartSeries InvestigationStatus { get; init; } = new();
    public DashboardChartSeries AccountCreationTrend { get; init; } = new();
    public IReadOnlyList<DashboardEventItem> RecentAudit { get; init; } = [];
    public IReadOnlyList<DashboardAlertItem> RecentAlerts { get; init; } = [];
    public IReadOnlyList<DashboardRequestItem> PendingActions { get; init; } = [];
}

public sealed class OfficerDashboardDto
{
    public int AlertsInRange { get; init; }
    public int UnacknowledgedAlerts { get; init; }
    public int OpenAlerts { get; init; }
    public int HighPriorityOpen { get; init; }
    public int PendingInvestigations { get; init; }
    public int OngoingInvestigations { get; init; }
    public int ResolvedInvestigationsInRange { get; init; }
    public DashboardChartSeries AlertSeverity { get; init; } = new();
    public DashboardChartSeries InvestigationStatus { get; init; } = new();
    public DashboardChartSeries AlertTrend { get; init; } = new();
    public IReadOnlyList<DashboardAlertItem> RecentAlerts { get; init; } = [];
    public IReadOnlyList<DashboardEventItem> RecentSecurityActivity { get; init; } = [];
}

public sealed class EmployeeDashboardDto
{
    public string DisplayName { get; init; } = "";
    public string Email { get; init; } = "";
    public string RoleName { get; init; } = "";
    public string AccountStatus { get; init; } = "";
    public string LastSignIn { get; init; } = "";
    public string Authenticator { get; init; } = "";
    public int PersonalEventsInRange { get; init; }
    public int OpenPersonalAlerts { get; init; }
    public int PendingRequests { get; init; }
    public DashboardChartSeries PersonalActivityTrend { get; init; } = new();
    public IReadOnlyList<DashboardEventItem> RecentActivity { get; init; } = [];
    public IReadOnlyList<DashboardAlertItem> PersonalNotifications { get; init; } = [];
    public IReadOnlyList<DashboardRequestItem> Requests { get; init; } = [];
}

public sealed record DashboardPayload
{
    public string Kind { get; init; } = "";
    public string Preset { get; init; } = "";
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string RangeLabel { get; init; } = "";
    public string? Error { get; init; }
    public AdminDashboardDto? Admin { get; init; }
    public OfficerDashboardDto? Officer { get; init; }
    public EmployeeDashboardDto? Employee { get; init; }
}
