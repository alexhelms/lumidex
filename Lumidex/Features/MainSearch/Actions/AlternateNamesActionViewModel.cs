using Lumidex.Core.Data;
using Lumidex.Features.Library.Messages;
using Lumidex.Features.MainSearch.Actions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace Lumidex.Features.MainSearch.Actions;

public partial class AlternateNamesActionViewModel : ActionViewModelBase,
    IRecipient<LibraryScanned>,
    IRecipient<AddAlternateName>,
    IRecipient<RemoveAlternateName>,
    IRecipient<RemoveAllAlternateNames>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    private HashSet<string> _alternateNameLookup = new();

    [ObservableProperty] string? _newAlternateName;
    [ObservableProperty] AvaloniaList<AlternateNameViewModel> _alternateNames = new();
    [ObservableProperty] AvaloniaList<AlternateNameViewModel> _alternateNamesOfSelectedItems = new();

    public AlternateNamesActionViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        DisplayName = "Alternate Names";
        RefreshAlternateNames();
    }

    private void RefreshAlternateNames()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        AlternateNames = new(
            dbContext.AlternateNames
                .AsNoTracking()
                .OrderBy(name => name.Name)
                .Select(AlternateNameMapper.ToViewModel)
                .ToList()
        );

        _alternateNameLookup = AlternateNames
            .Select(name => name.Name)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
    }

    protected override void OnSelectedItemsChanged()
    {
        AlternateNamesOfSelectedItems = new(
            SelectedItems
                .SelectMany(f => f.AlternateNames)
                .OrderBy(name => name.Name)
                .Distinct()
        );
    }

    public void Receive(LibraryScanned message)
    {
        // A library scan adds new alternate names
        RefreshAlternateNames();
    }

    public void Receive(AddAlternateName message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var alternateName = dbContext.AlternateNames
            .Where(alt => alt.Name == message.Name)
            .FirstOrDefault();

        if (alternateName is null)
        {
            alternateName = new AlternateName
            {
                Name = message.Name,
            };

            dbContext.AlternateNames.Add(alternateName);
        }

        var imageFiles = dbContext.ImageFiles
            .Where(f => SelectedIds.Contains(f.Id))
            .ToList();

        foreach (var imageFile in imageFiles)
        {
            imageFile.AlternateNames.Add(alternateName);
        }

        if (dbContext.SaveChanges() > 0)
        {
            if (!_alternateNameLookup.Contains(message.Name))
            {
                RefreshAlternateNames();
            }

            var alternateNameVm = AlternateNameMapper.ToViewModel(alternateName);

            foreach (var imageFile in message.ImageFiles)
            {
                imageFile.AlternateNames.Add(alternateNameVm);
            }

            AlternateNamesOfSelectedItems.Add(alternateNameVm);

            Messenger.Send(new AlternateNameAdded
            {
                AlternateName = alternateNameVm,
                ImageFiles = message.ImageFiles,
            });
        }
    }

    public void Receive(RemoveAlternateName message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var alternateName = dbContext.AlternateNames
            .Where(alt => alt.Id == message.AlternateName.Id)
            .FirstOrDefault();

        if (alternateName is not null)
        {
            dbContext.AlternateNames.Remove(alternateName);

            if (dbContext.SaveChanges() > 0)
            {
                RefreshAlternateNames();

                foreach (var imageFile in message.ImageFiles)
                {
                    imageFile.AlternateNames.Remove(message.AlternateName);
                }

                AlternateNamesOfSelectedItems.Remove(message.AlternateName);

                Messenger.Send(new AlternateNameAdded
                {
                    AlternateName = message.AlternateName,
                    ImageFiles = message.ImageFiles,
                });
            }
        }
    }

    public void Receive(RemoveAllAlternateNames message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var imageFileIds = message.ImageFiles.Select(f => f.Id).ToHashSet();
        var imageFiles = dbContext.ImageFiles
            .Include(f => f.AlternateNames)
            .Where(f => imageFileIds.Contains(f.Id))
            .ToList();

        foreach (var imageFile in imageFiles)
        {
            imageFile.AlternateNames.Clear();
        }

        if (dbContext.SaveChanges() > 0)
        {
            var alternativeNames = new HashSet<AlternateNameViewModel>();

            foreach (var imageFile in message.ImageFiles)
            {
                foreach(var alternateName in imageFile.AlternateNames)
                    alternativeNames.Add(alternateName);

                imageFile.AlternateNames.Clear();
            }

            Messenger.Send(new AllAlternateNamesRemoved
            {
                AlternateNames = alternativeNames,
                ImageFiles = message.ImageFiles,
            });
        }
    }

    [RelayCommand]
    private void AddAlternateName()
    {
        if (SelectedItems.Count == 0) return;

        if (NewAlternateName is { Length: >0 })
        {
            Messenger.Send(new AddAlternateName
            {
                Name = NewAlternateName,
                ImageFiles = SelectedItems,
            });

            NewAlternateName = null;
        }
    }

    [RelayCommand]
    private void RemoveAllAlternateNames()
    {
        if (SelectedItems.Count == 0) return;

        Messenger.Send(new RemoveAllAlternateNames
        {
            ImageFiles = SelectedItems,
        });
    }

    [RelayCommand]
    private void RemoveAlternateName(AlternateNameViewModel alternateName)
    {
        if (SelectedItems.Count == 0) return;

        Messenger.Send(new RemoveAlternateName
        {
            AlternateName = alternateName,
            ImageFiles = SelectedItems,
        });
    }
}
