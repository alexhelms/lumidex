using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.Settings;

public partial class GeneralSettingsViewModel : ViewModelBase, ISettingsViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    
    public string DisplayName => "General";
    
    [ObservableProperty]
    public partial bool FullScreen { get; set; }

    public GeneralSettingsViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            FullScreen = settings.FullScreen;
        }
    }

    partial void OnFullScreenChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var settings = dbContext.AppSettings.FirstOrDefault();
            if (settings is not null)
            {
                settings.FullScreen = newValue;
                dbContext.SaveChanges();
            }
        }
    }
}