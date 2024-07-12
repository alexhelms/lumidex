using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Lumidex.Features.Library.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex.Features.Library;

public partial class LibraryManagerViewModel : ViewModelBase,
    IRecipient<CreateLibrary>,
    IRecipient<DeleteLibrary>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty] LibraryViewModel? _selectedLibrary;

    public ObservableCollectionEx<LibraryViewModel> Libraries { get; } = new();

    public LibraryManagerViewModel(
        IServiceProvider serviceProvider,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var libraries = dbContext.Libraries
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

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        foreach (var library in Libraries)
        {
            Messenger.Send(new LibraryCreated
            {
                Library = new Common.LibraryViewModel
                {
                    Id = library.Id,
                    Name = library.Name,
                    Path = library.Path,
                    LastScan = library.LastScan,
                },
            });
        }
    }

    public void Receive(CreateLibrary message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = new Core.Data.Library
        {
            Name = message.Name,
            Path = message.Path,
        };
        dbContext.Libraries.Add(library);

        if (dbContext.SaveChanges() > 0)
        {
            var vm = _serviceProvider.GetRequiredService<LibraryViewModel>();
            vm.Id = library.Id;
            vm.Name = library.Name;
            Libraries.Add(vm);

            Log.Information("Library created ({Id})", library.Id);

            Messenger.Send(new LibraryCreated
            {
                Library = new Common.LibraryViewModel
                {
                    Id = library.Id,
                    Name = library.Name,
                    Path = library.Path,
                    LastScan = library.LastScan,
                },
            });
        }
    }

    public void Receive(DeleteLibrary message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = dbContext.Libraries.FirstOrDefault(l => l.Id == message.Library.Id);
        if (library is not null)
        {
            dbContext.Libraries.Remove(library);
            if (dbContext.SaveChanges() > 0)
            {
                var vm = Libraries.First(l => l.Id == library.Id);
                Libraries.Remove(vm);

                Log.Information("Library deleted ({Id}) {Name}", library.Id, library.Name);

                Messenger.Send(new LibraryDeleted
                {
                    Library = new Common.LibraryViewModel
                    {
                        Id = library.Id,
                        Name = library.Name,
                        Path = library.Path,
                        LastScan = library.LastScan,
                    },
                });
            }
        }
    }

    [RelayCommand]
    private void CreateLibrary()
    {
        Messenger.Send(new CreateLibrary
        {
            Name = "New Library",
            Path = LumidexPaths.DefaultLibrary,
        });

        SelectedLibrary = Libraries.Last();
    }
}
