namespace GcExtensionAuditMaui.Views;

public sealed class MainTabbedPage : TabbedPage
{
    public MainTabbedPage(
        HomePage home,
        DryRunPage dryRun,
        MissingAssignmentsPage missing,
        DiscrepanciesPage discrepancies,
        DuplicateUsersPage dupUsers,
        DuplicateExtensionsPage dupExts,
        PatchMissingPage patch,
        LogPage log)
    {
        Children.Add(home);
        Children.Add(dryRun);
        Children.Add(missing);
        Children.Add(discrepancies);
        Children.Add(dupUsers);
        Children.Add(dupExts);
        Children.Add(patch);
        Children.Add(log);
    }
}

