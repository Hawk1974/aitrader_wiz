using System.ComponentModel;
using System.Runtime.CompilerServices;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz;

public sealed class ComputerServicePlacementRow : INotifyPropertyChanged
{
    private ServicePlacementMode _placementMode;

    public required ServiceKind ServiceKind { get; init; }
    public required string DisplayName { get; init; }

    public ServicePlacementMode PlacementMode
    {
        get => _placementMode;
        set => SetField(ref _placementMode, value);
    }

    public IReadOnlyList<EnumOption<ServicePlacementMode>> AvailablePlacementModes { get; } =
        DeploymentCatalog.ServicePlacementOptions;

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
