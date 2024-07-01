using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Lumidex.Features.Library.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex.Features.Library;

public partial class LibraryManagerViewModel : ViewModelBase,
    IRecipient<LibraryDeleted>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LumidexDbContext _dbContext;

    [ObservableProperty] LibraryViewModel? _selectedLibrary;

    public AvaloniaList<LibraryViewModel> Libraries { get; } = new();

    public LibraryManagerViewModel(
        IServiceProvider serviceProvider,
        LumidexDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;

        var libraries = _dbContext.Libraries
            .AsNoTracking()
            .ToList();

        Libraries.AddRange(libraries.Select(ToViewModel));
    }

    private LibraryViewModel ToViewModel(Core.Data.Library library)
    {
        var vm = _serviceProvider.GetRequiredService<LibraryViewModel>();
        vm.Id = library.Id;
        vm.Name = library.Name;
        return vm;
    }

    public void Receive(LibraryDeleted message)
    {
        var library = Libraries.FirstOrDefault(l => l.Id == message.Id);
        if (library is not null)
        {
            Libraries.Remove(library);
        }
    }

    [RelayCommand]
    private async Task AddLibrary()
    {
        var library = new Core.Data.Library
        {
            Name = "New Library",
            Path = LumidexPaths.DefaultLibrary,
            AppSettingsId = _dbContext.AppSettings.First().Id,
        };
        _dbContext.Libraries.Add(library);
        await _dbContext.SaveChangesAsync();

        Libraries.Add(ToViewModel(library));
        SelectedLibrary = Libraries.Last();
    }
}
