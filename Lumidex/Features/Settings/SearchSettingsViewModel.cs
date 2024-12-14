using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.Settings;

public partial class SearchSettingsViewModel : ViewModelBase, ISettingsViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty] bool _persistFiltersOnExit;

    public string DisplayName => "Search";

    public SearchSettingsViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            PersistFiltersOnExit = settings.PersistFiltersOnExit;
        }
    }

    partial void OnPersistFiltersOnExitChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var settings = dbContext.AppSettings.FirstOrDefault();
            if (settings is not null)
            {
                settings.PersistFiltersOnExit = newValue;

                // Clear any persisted filters if the user is turning this feature off.
                if (newValue == false)
                {
                    settings.PersistedFilters.Clear();
                }

                dbContext.SaveChanges();
            }
        }
    }
}
