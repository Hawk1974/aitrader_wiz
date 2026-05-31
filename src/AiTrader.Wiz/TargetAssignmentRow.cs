using System.ComponentModel;
using System.Runtime.CompilerServices;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public sealed class TargetAssignmentRow : INotifyPropertyChanged
{
    private bool _hermesBackend;
    private bool _hermesDesktop;
    private bool _isPrimaryDesktop;
    private bool _isAuthoritativeBackend;
    private string _aiProviderKey = string.Empty;

    public required string TargetId { get; init; }
    public required string DisplayName { get; init; }
    public required RuntimeTargetKind Kind { get; init; }

    public bool HermesBackend
    {
        get => _hermesBackend;
        set => SetField(ref _hermesBackend, value);
    }

    public bool HermesDesktop
    {
        get => _hermesDesktop;
        set => SetField(ref _hermesDesktop, value);
    }

    public bool IsPrimaryDesktop
    {
        get => _isPrimaryDesktop;
        set => SetField(ref _isPrimaryDesktop, value);
    }

    public bool IsAuthoritativeBackend
    {
        get => _isAuthoritativeBackend;
        set => SetField(ref _isAuthoritativeBackend, value);
    }

    public string AiProviderKey
    {
        get => _aiProviderKey;
        set => SetField(ref _aiProviderKey, value);
    }

    public IReadOnlyList<HermesProviderOption> AvailableAiProviders { get; } =
        HermesProviderCatalog.All;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
