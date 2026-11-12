using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Pages.Monitoring.Investigations;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<InvestigationRecord> Records { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Records = await _db.InvestigationRecords
            .Include(i => i.Alert)
            .OrderByDescending(i => i.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }
}
