using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex.Features.AstrobinExport;

public partial class FilterMappingViewModel : ViewModelBase
{
    [ObservableProperty] string _imageFilterName = string.Empty;
    [ObservableProperty] AstrobinFilterViewModel? _selectedAstrobinFilter;

    partial void OnSelectedAstrobinFilterChanged(AstrobinFilterViewModel? oldValue, AstrobinFilterViewModel? newValue)
    {
        Messenger.Send(new PropertyChangedMessage<AstrobinFilterViewModel?>(this, nameof(SelectedAstrobinFilter), oldValue, newValue));
    }

    [RelayCommand]
    private void Clear() => SelectedAstrobinFilter = null;
}