using CommunityToolkit.Mvvm.ComponentModel;
using GcExtensionAuditMaui.Models.Audit;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class ContextStore : ObservableObject
{
    private AuditContext? _context;
    public AuditContext? Context
    {
        get => _context;
        set => SetProperty(ref _context, value);
    }

    private AuditContext? _extensionContext;
    public AuditContext? ExtensionContext
    {
        get => _extensionContext;
        set => SetProperty(ref _extensionContext, value);
    }

    private AuditContext? _didContext;
    public AuditContext? DidContext
    {
        get => _didContext;
        set => SetProperty(ref _didContext, value);
    }

    private ContextSummary? _summary;
    public ContextSummary? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    private string? _lastOutputFolder;
    public string? LastOutputFolder
    {
        get => _lastOutputFolder;
        set => SetProperty(ref _lastOutputFolder, value);
    }

    public void Clear()
    {
        Context = null;
        ExtensionContext = null;
        DidContext = null;
        Summary = null;
        LastOutputFolder = null;
    }
}
