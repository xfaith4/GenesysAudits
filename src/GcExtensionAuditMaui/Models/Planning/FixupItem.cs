using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GcExtensionAuditMaui.Models.Planning;

public sealed class FixupItem : INotifyPropertyChanged
{
    public required string Category { get; init; } // DuplicateUser | Missing | Discrepancy | Reassert
    public required string UserId { get; init; }
    public required string? User { get; init; }

    public string? CurrentExtension { get; init; }

    private string? _recommendedExtension;
    public string? RecommendedExtension
    {
        get => _recommendedExtension;
        set => SetProperty(ref _recommendedExtension, value);
    }

    private FixupActionType _action;
    public FixupActionType Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public string? Notes { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) { return false; }
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
