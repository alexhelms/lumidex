using CommunityToolkit.Mvvm.Messaging.Messages;
using Lumidex.Core.Exporters;
using Lumidex.Services;

namespace Lumidex.Features.AstrobinExport;

public partial class AstrobinExportViewModel : ViewModelBase,
    IRecipient<PropertyChangedMessage<AstrobinFilterViewModel?>>
{
    private readonly StringComparer _comparer = StringComparer.InvariantCultureIgnoreCase;

    private readonly SystemService _systemService;
    
    private Dictionary<string, List<ImageFileViewModel>> _imageGrouping = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> SelectedItems { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<AstrobinFilterViewModel> AstrobinFilters { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<FilterMappingViewModel> FilterMappings { get; set; } = new();

    [ObservableProperty]
    public partial string? CsvContent { get; set; }
    public Action CloseDialog { get; set; } = () => { };

    public AstrobinExportViewModel(SystemService systemService)
    {
        _systemService = systemService;
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        Messenger.UnregisterAll(this);
    }

    private void RenderCsvContent()
    {
        try
        {
            var groups = new List<AstrobinImageGroup>();
            foreach (var images in _imageGrouping.Values)
            {
                // Each list of images in this group have the same duration and filter

                var duration = TimeSpan.FromSeconds(images.First().Exposure.GetValueOrDefault());
                var filter = images.First().FilterName ?? "None";
                var mapping = FilterMappings.FirstOrDefault(m => m.ImageFilterName.Equals(filter, StringComparison.InvariantCultureIgnoreCase));
                if (mapping is not null)
                {
                    groups.Add(new AstrobinImageGroup
                    {
                        Count = images.Count,
                        Duration = duration,
                        Filter = mapping.SelectedAstrobinFilter is not null
                            ? new AstrobinFilter
                            {
                                Id = mapping.SelectedAstrobinFilter.AstrobinId,
                                Name = mapping.SelectedAstrobinFilter.Name,
                            }
                            : null,
                    });
                }
            }

            var exporter = new AstrobinAcquisitionExporter();
            CsvContent = exporter.ExportCsv(groups);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error rendering Astrobin CSV");
            CsvContent = null;
        }
    }

    partial void OnSelectedItemsChanged(ObservableCollectionEx<ImageFileViewModel> value)
    {
        // Get the user's configured astrobin filters
        var response = Messenger.Send(new CollectionRequestMessage<AstrobinFilterViewModel>()) ?? [];
        AstrobinFilters = new(response);

        // Create the mappings
        FilterMappings = new(value
            .GroupBy(f => string.IsNullOrWhiteSpace(f.FilterName) ? "None" : f.FilterName, _comparer)
            .Select(grp => new FilterMappingViewModel
            {
                ImageFilterName = grp.Key,
            })
            .OrderBy(x => x.ImageFilterName, _comparer)
        );

        _imageGrouping = SelectedItems
            .Select(x => new
            {
                Key = (string.IsNullOrWhiteSpace(x.FilterName) ? "None" : x.FilterName) + "|" + x.Exposure.GetValueOrDefault().ToString(),
                Value = x,
            })
            .GroupBy(x => x.Key, _comparer)
            .ToDictionary(
                grp => grp.Key,
                grp => grp.Select(x => x.Value).ToList(),
                _comparer);

        RenderCsvContent();
    }

    public void Receive(PropertyChangedMessage<AstrobinFilterViewModel?> message)
    {
        RenderCsvContent();
    }

    [RelayCommand]
    private async Task CopyCsvContent()
    {
        await _systemService.SetClipboard(CsvContent);
    }

    [RelayCommand]
    private void Close()
    {
        CloseDialog();
    }
}
