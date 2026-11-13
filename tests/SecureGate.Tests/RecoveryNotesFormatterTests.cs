using SecureGate1.Services;

namespace SecureGate.Tests;

public class RecoveryNotesFormatterTests
{
    [Fact]
    public void ParseChecklistItems_SplitsLegacySingleLineBullets()
    {
        const string notes =
            "Verification performed:• Verified the employee's identity• Confirmed the employee's name and account ID";

        var items = RecoveryNotesFormatter.ParseChecklistItems(notes);

        Assert.Equal(2, items.Count);
        Assert.Equal("Verified the employee's identity", items[0]);
        Assert.Equal("Confirmed the employee's name and account ID", items[1]);
    }

    [Fact]
    public void ParseChecklistItems_SplitsMultilineStorageFormat()
    {
        const string notes = """
            Verification performed:
            • Verified the employee's identity
            • Confirmed department or assigned office
            """;

        var items = RecoveryNotesFormatter.ParseChecklistItems(notes);

        Assert.Equal(2, items.Count);
        Assert.Equal("Verified the employee's identity", items[0]);
        Assert.Equal("Confirmed department or assigned office", items[1]);
    }

    [Fact]
    public void CleanMultiline_PreservesLineBreaks()
    {
        const string value = "Line one\n• item two";

        var cleaned = AuditService.CleanMultiline(value, 500);

        Assert.Contains('\n', cleaned);
        Assert.Equal(value, cleaned);
    }
}
