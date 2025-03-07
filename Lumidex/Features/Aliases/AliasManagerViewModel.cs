﻿using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.Aliases.Messages;
using Lumidex.Features.Library.Messages;
using Lumidex.Features.Main.Messages;
using Lumidex.Features.MainSearch.Editing.Messages;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.SideNavBar;
using Lumidex.Services;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.Aliases;

public partial class AliasManagerViewModel : ValidatableViewModelBase,
    IRecipient<LibraryScanned>,
    IRecipient<CreateAlias>,
    IRecipient<DeleteAlias>,
    IRecipient<AliasCreated>,
    IRecipient<AliasDeleted>,
    IRecipient<ImageFilesEdited>
{
    private readonly StringComparer _comparer = StringComparer.InvariantCultureIgnoreCase;

    private readonly DialogService _dialogService;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty]
    public partial ObjectNameViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial ObservableCollectionEx<ObjectNameViewModel> ObjectNames { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<AliasViewModel> Aliases { get; set; } = new();
    public DataGridCollectionView ObjectNameView { get; private set; } = new(Array.Empty<ObjectNameViewModel>());
    public DataGridCollectionView AliasView { get; private set; } = new(Array.Empty<AliasViewModel>());

    public AliasManagerViewModel(
        DialogService dialogService,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dialogService = dialogService;
        _dbContextFactory = dbContextFactory;

        RefreshObjectNamesAndAliases();
        SelectedItem = ObjectNames.FirstOrDefault();
    }

    private void RefreshObjectNamesAndAliases()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        ObjectNames = new(dbContext
            .ImageFiles
            .Where(f => f.ObjectName != null)
            .Select(f => f.ObjectName)
            .Distinct()
            .OrderBy(name => name)
            .Select(name => new ObjectNameViewModel
            {
                ObjectName = name!,
            })
        );

        Aliases = new(dbContext
            .ObjectAliases
            .Select(alias => new AliasViewModel
            {
                Id = alias.Id,
                ObjectName = alias.ObjectName,
                Alias = alias.Alias,
            })
            .OrderBy(alias => alias.Alias)
        );

        // Group the aliases by object name and add them to the object name view models
        var aliasGroups = Aliases
            .GroupBy(alias => alias.ObjectName)
            .ToDictionary(alias => alias.Key, alias => alias, StringComparer.InvariantCultureIgnoreCase);
        foreach (var objectName in ObjectNames)
        {
            using (var aliases = objectName.Aliases.DelayNotifications())
            {
                if (aliasGroups.TryGetValue(objectName.ObjectName, out var grouping))
                {
                    aliases.AddRange(grouping);
                }
            }
        }

        AliasView = new(Aliases);
        AliasView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(AliasViewModel.Alias)));
    }

    public void Receive(LibraryScanned message)
    {
        Dispatcher.UIThread.Invoke(RefreshObjectNamesAndAliases);
    }

    public void Receive(CreateAlias message)
    {
        var alias = new ObjectAlias
        {
            ObjectName = message.ObjectName,
            Alias = message.Alias,
        };

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.ObjectAliases.Add(alias);
        if (dbContext.SaveChanges() > 0)
        {
            var vm = new AliasViewModel
            {
                Id = alias.Id,
                ObjectName = alias.ObjectName,
                Alias = alias.Alias,
            };

            Messenger.Send(new AliasCreated
            {
                Alias = vm,
            });
        }
    }

    public void Receive(DeleteAlias message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        if (dbContext.ObjectAliases.FirstOrDefault(alias => alias.Id == message.Id) is { } alias)
        {
            dbContext.ObjectAliases.Remove(alias);
            if (dbContext.SaveChanges() > 0)
            {
                var vm = Aliases.First(alias => alias.Id == message.Id);
                Messenger.Send(new AliasDeleted
                {
                    Alias = vm,
                });
            }
        }
    }

    public void Receive(AliasCreated message)
    {
        if (!Aliases.Contains(message.Alias))
        {
            // Insert in the list while maintaining alphabetical order
            var index = Aliases
                .Select(a => a.Alias)
                .ToList()
                .BinarySearch(message.Alias.Alias, StringComparer.InvariantCultureIgnoreCase);
            if (index < 0)
            {
                Aliases.Insert(~index, message.Alias);
            }
            else
            {
                Aliases.Insert(index, message.Alias);
            }

            // Insert in the list while maintaining alphabetical order
            var objectName = ObjectNames.First(x => x.ObjectName.Equals(message.Alias.ObjectName, StringComparison.InvariantCultureIgnoreCase));
            index = objectName.Aliases
                .Select(a => a.Alias)
                .ToList()
                .BinarySearch(message.Alias.Alias, StringComparer.InvariantCultureIgnoreCase);
            if (index < 0)
            {
                objectName.Aliases.Insert(~index, message.Alias);
            }
            else
            {
                objectName.Aliases.Insert(index, message.Alias);
            }
        }
    }

    public void Receive(AliasDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (Aliases.Contains(message.Alias))
            {
                Aliases.Remove(message.Alias);

                var objectNames = ObjectNames.Where(o => o.ObjectName.Equals(message.Alias.ObjectName, StringComparison.InvariantCultureIgnoreCase));
                foreach (var objectName in objectNames)
                {
                    if (objectName.Aliases.FirstOrDefault(a => a.Id == message.Alias.Id) is { } alias)
                    {
                        objectName.Aliases.Remove(alias);
                    }
                }
            }
        });
    }

    public void Receive(ImageFilesEdited message)
    {
        Dispatcher.UIThread.Invoke(RefreshObjectNamesAndAliases);
    }

    [RelayCommand]
    private async Task DeleteAliasGroup(string? aliasName)
    {
        if (aliasName is null) return;

        if (await _dialogService.ShowConfirmationDialog("Are you sure you want to delete these aliases?"))
        {
            var aliasIds = Aliases
                .Where(alias => alias.Alias.Equals(aliasName, StringComparison.InvariantCultureIgnoreCase))
                .Select(alias => alias.Id)
                .ToArray();

            foreach (var id in aliasIds)
            {
                Messenger.Send(new DeleteAlias
                {
                    Id = id,
                });
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAlias(AliasViewModel alias)
    {
        if (await _dialogService.ShowConfirmationDialog("Are you sure you want to delete this alias?"))
        {
            Messenger.Send(new DeleteAlias
            {
                Id = alias.Id,
            });
        }
    }

    [RelayCommand]
    private void SearchObjectName()
    {
        if (SelectedItem?.ObjectName is { } objectName)
        {
            Messenger.Send(new ObjectNameSearchFill($"\"{objectName}\""));
            Messenger.Send(new ChangeSideTab(SideNavBarViewModel.SearchTabName));
        }
    }
}
