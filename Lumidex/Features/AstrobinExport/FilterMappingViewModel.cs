using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex.Features.AstrobinExport;

public partial class FilterMappingViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string ImageFilterName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial AstrobinFilterViewModel? SelectedAstrobinFilter { get; set; }

    partial void OnSelectedAstrobinFilterChanged(AstrobinFilterViewModel? oldValue, AstrobinFilterViewModel? newValue)
    {
        Messenger.Send(new PropertyChangedMessage<AstrobinFilterViewModel?>(this, nameof(SelectedAstrobinFilter), oldValue, newValue));
    }

    [RelayCommand]
    private void Clear() => SelectedAstrobinFilter = null;
}