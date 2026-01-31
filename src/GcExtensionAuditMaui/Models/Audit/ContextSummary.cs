namespace GcExtensionAuditMaui.Models.Audit;

public sealed class ContextSummary
{
    public AuditNumberKind AuditKind { get; init; } = AuditNumberKind.Extension;
    public int UsersTotal { get; init; }
    public int UsersWithProfileExtension { get; init; }
    public int DistinctProfileExtensions { get; init; }
    public int ExtensionsLoaded { get; init; }
    public string ExtensionMode { get; init; } = "N/A";

    public override string ToString()
        => $"AuditKind={AuditKind}; UsersTotal={UsersTotal}; UsersWithProfileExtension={UsersWithProfileExtension}; DistinctProfileExtensions={DistinctProfileExtensions}; ExtensionsLoaded={ExtensionsLoaded}; ExtensionMode={ExtensionMode}";
}
