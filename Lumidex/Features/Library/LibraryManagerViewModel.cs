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
        var library = new Core.Data.Library
        {
            Name = message.Name,
            Path = message.Path,
        };
        _dbContext.Libraries.Add(library);

        if (_dbContext.SaveChanges() > 0)
        {
            var vm = _serviceProvider.GetRequiredService<LibraryViewModel>();
            vm.Id = library.Id;
            vm.Name = library.Name;
            Libraries.Add(vm);

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
        var library = _dbContext.Libraries.FirstOrDefault(l => l.Id == message.Library.Id);
        if (library is not null)
        {
            _dbContext.Libraries.Remove(library);
            if (_dbContext.SaveChanges() > 0)
            {
                var vm = Libraries.First(l => l.Id == library.Id);
                Libraries.Remove(vm);

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
