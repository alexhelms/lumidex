using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.Settings;

public partial class PlotSettingsViewModel : ViewModelBase, ISettingsViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public string DisplayName => "Plot";

    [ObservableProperty]
    public partial bool UseCalibratedFrames { get; set; }

    public PlotSettingsViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            UseCalibratedFrames = settings.UseCalibratedFrames;
        }
    }

    partial void OnUseCalibratedFramesChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var settings = dbContext.AppSettings.FirstOrDefault();
            if (settings is not null)
            {
                settings.UseCalibratedFrames = newValue;
                dbContext.SaveChanges();
            }
        }
    }
}
