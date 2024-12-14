using CommunityToolkit.Mvvm.Messaging.Messages;
using Flurl.Http;
using Lumidex.Core.Data;
using Lumidex.Core.Exporters;
using Lumidex.Services;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.Settings;

public partial class AstrobinSettingsViewModel : ViewModelBase, ISettingsViewModel,
    IRecipient<CollectionRequestMessage<AstrobinFilterViewModel>>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    private readonly DialogService _dialogService;

    private readonly AstrobinAcquisitionExporter _exporter = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<AstrobinFilterViewModel> AstrobinFilters { get; set; } = new();

    [ObservableProperty]
    public partial bool IsQueryingAstrobin { get; set; }

    [ObservableProperty]
    public partial string? AstrobinUnavailableMessage { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddFilterCommand))]
    public partial object? SelectedFilter { get; set; }

    public string DisplayName => "Astrobin";
    public Func<string?, CancellationToken, Task<IEnumerable<object>>>? AsyncPopulator { get; private set; }

    public AstrobinSettingsViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        DialogService dialogService)
    {
        _dbContextFactory = dbContextFactory;
        _dialogService = dialogService;
        AsyncPopulator = QueryAstrobinAsync;

        using var dbContext = _dbContextFactory.CreateDbContext();
        AstrobinFilters = new(dbContext.AstrobinFilters
            .OrderBy(f => f.Name)
            .Select(f => new AstrobinFilterViewModel
            {
                Id = f.Id,
                AstrobinId = f.AstrobinId,
                Name = f.Name,
            })
        );
    }

    private async Task<IEnumerable<object>> QueryAstrobinAsync(string? query, CancellationToken token)
    {
        try
        {
            IsQueryingAstrobin = true;
            AstrobinUnavailableMessage = null;
            return await _exporter.QueryFilters(query ?? string.Empty, token);
        }
        catch (FlurlHttpException)
        {
            AstrobinUnavailableMessage = "Astrobin is unavailable, check your connection";
            return [];
        }
        finally
        {
            IsQueryingAstrobin = false;
        }
    }

    public void Receive(CollectionRequestMessage<AstrobinFilterViewModel> message)
    {
        foreach (var filter in AstrobinFilters)
        {
            message.Reply(filter);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddFilter))]
    private void AddFilter()
    {
        if (SelectedFilter is not Core.Exporters.AstrobinFilter astrobinFilter)
            return;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var alreadyExists = dbContext.AstrobinFilters.Any(f => f.AstrobinId == astrobinFilter.Id);
        if (alreadyExists)
        {
            SearchText = null;
            SelectedFilter = null;
            return;
        }

        var filter = new Core.Data.AstrobinFilter
        {
            AstrobinId = astrobinFilter.Id,
            Name = astrobinFilter.Name,
            AppSettings = dbContext.AppSettings.First(),
        };

        dbContext.AstrobinFilters.Add(filter);

        if (dbContext.SaveChanges() > 0)
        {
            SearchText = null;
            SelectedFilter = null;
            AstrobinFilters.Add(new AstrobinFilterViewModel
            {
                Id = filter.Id,
                AstrobinId = astrobinFilter.Id,
                Name = astrobinFilter.Name,
            });
        }
    }

    public bool CanAddFilter => SelectedFilter is Core.Exporters.AstrobinFilter;

    [RelayCommand]
    private async Task RemoveFilter(AstrobinFilterViewModel filter)
    {
        if (await _dialogService.ShowConfirmationDialog("Are you sure you want to remove this filter?"))
        {
            if (AstrobinFilters.Remove(filter))
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                dbContext.AstrobinFilters
                    .Where(f => f.Id == filter.Id)
                    .ExecuteDelete();
            }
        }
    }
}