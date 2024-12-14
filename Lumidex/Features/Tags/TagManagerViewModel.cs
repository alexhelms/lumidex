using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.Tags.Messages;
using Lumidex.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Features.Tags;

public partial class TagManagerViewModel : ValidatableViewModelBase,
    IRecipient<GetTag>,
    IRecipient<GetTags>,
    IRecipient<CreateTag>,
    IRecipient<DeleteTag>,
    IRecipient<EditTag>,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>,
    IRecipient<AddTag>,
    IRecipient<AddTags>,
    IRecipient<RemoveTag>,
    IRecipient<RemoveTags>,
    IRecipient<ClearTags>
{
    private static readonly Color DefaultColor = Color.Parse(Tag.DefaultColor);

    private readonly DialogService _dialogService;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(CreateTagCommand))]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; } = DefaultColor;

    [ObservableProperty]
    public partial ObservableCollectionEx<TagViewModel> Tags { get; set; } = new();

    [ObservableProperty]
    public partial TagViewModel? SelectedTag { get; set; }

    public TagManagerViewModel(
        DialogService dialogService,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dialogService = dialogService;
        _dbContextFactory = dbContextFactory;

        using var dbContext = _dbContextFactory.CreateDbContext();
        Tags = new(
            dbContext
                .Tags
                .AsNoTracking()
                .OrderByDescending(tag => tag.Id)
                .Select(tag => new TagViewModel
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Color = tag.Color,
                })
                .ToList()
        );

        var usageLookup = dbContext
            .ImageFiles
            .AsNoTracking()
            .SelectMany(f => f.Tags)
            .GroupBy(tag => tag, (k, g) => new
            {
                TagId = k.Id,
                Count = g.Count(),
            })
            .ToDictionary(x => x.TagId, x => x.Count);

        // Get the tag usage count
        foreach (var tag in Tags)
        {
            usageLookup.TryGetValue(tag.Id, out var count);
            tag.TaggedImageCount = count;
        }
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        foreach (var tag in Tags)
        {
            Messenger.Send(new TagCreated { Tag = tag });
        }
    }

    private int AddTagsToImageFiles(LumidexDbContext dbContext, IEnumerable<int> tagIds, IEnumerable<int> imageFileIds)
    {
        int count = 0;
        var tagIdLookup = tagIds.ToHashSet();
        var idLookup = imageFileIds.ToHashSet();

        var tags = dbContext.Tags.Where(tag => tagIdLookup.Contains(tag.Id)).ToList();
        foreach (var tag in tags)
        {
            var imageFiles = dbContext.ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Add(tag);
            }

            count += dbContext.SaveChanges();
        }

        return count;
    }

    private int RemoveTagsFromImageFiles(LumidexDbContext dbContext, IEnumerable<int> tagIds, IEnumerable<int> imageFileIds)
    {
        int count = 0;
        var tagIdLookup = tagIds.ToHashSet();
        var idLookup = imageFileIds.ToHashSet();

        var tags = dbContext.Tags.Where(tag => tagIdLookup.Contains(tag.Id)).ToList();
        foreach (var tag in tags)
        {
            var imageFiles = dbContext.ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Remove(tag);
            }

            count += dbContext.SaveChanges();
        }

        return count;
    }

    private int ClearTagsFromImageFiles(LumidexDbContext dbContext, IEnumerable<int> imageFileIds)
    {
        var idLookup = imageFileIds.ToHashSet();
        var imageFiles = dbContext.ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

        foreach (var imageFile in imageFiles)
        {
            imageFile.Tags.Clear();
        }

        return dbContext.SaveChanges();
    }

    public void Receive(GetTag message)
    {
        var tag = Tags.First(tag => tag.Id == message.Id);
        message.Reply(tag);
    }

    public void Receive(GetTags message)
    {
        message.Reply(Tags);
    }

    public void Receive(CreateTag message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var tag = new Tag
        {
            Name = message.Name,
            Color = $"#{message.Color.ToUInt32():x8}",
        };

        dbContext.Tags.Add(tag);
        if (dbContext.SaveChanges() > 0)
        {
            // Update UI
            var tagVm = new TagViewModel
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = tag.Color,
            };
            Tags.Insert(0, tagVm);

            Log.Information("Tag created ({Id}) {Name} {Color}", tag.Id, tag.Name, tag.Color);

            Messenger.Send(new TagCreated
            {
                Tag = tagVm,
            });
        }
    }

    public void Receive(DeleteTag message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var tag = dbContext.Tags.FirstOrDefault(tag => tag.Id == message.Tag.Id);
        if (tag is not null)
        {
            dbContext.Tags.Remove(tag);
            if (dbContext.SaveChanges() > 0)
            {
                // Update UI
                var tagVm = Tags.First(t => t.Id == message.Tag.Id);
                Tags.Remove(tagVm);

                Log.Information("Tag deleted ({Id})", tag.Id);

                Messenger.Send(new TagDeleted
                {
                    Tag = tagVm,
                });
            }
        }
    }

    public void Receive(EditTag message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var tag = dbContext.Tags.FirstOrDefault(tag => tag.Id == message.Id);
        if (tag is not null)
        {
            tag.Name = message.Name;
            tag.Color = message.Color;
            dbContext.Tags.Update(tag);

            if (dbContext.SaveChanges() > 0)
            {
                // Update UI
                var tagVm = Tags.First(t => t.Id == message.Id);
                tagVm.Name = tag.Name;
                tagVm.Color = tag.Color;

                Log.Information("Tag edited ({Id}) {Name} {Color}", tag.Id, tag.Name, tag.Color);

                Messenger.Send(new TagEdited
                {
                    Tag = tagVm,
                });
            }
        }
    }

    public void Receive(AddTag message)
    {
        AddTags([message.Tag], message.ImageFiles);
    }

    public void Receive(AddTags message)
    {
        AddTags(message.Tags, message.ImageFiles);
    }

    private void AddTags(IEnumerable<TagViewModel> tags, IEnumerable<ImageFileViewModel> imageFiles)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        if (AddTagsToImageFiles(dbContext, tags.Select(t => t.Id), imageFiles.Select(f => f.Id)) > 0)
        {
            var numTags = tags.Count();
            var numImageFiles = imageFiles.Count();
            var messages = new List<TagAdded>(numTags * numImageFiles);

            foreach (var tag in tags)
            {
                foreach (var imageFile in imageFiles)
                {
                    var newTags = new List<TagViewModel>(imageFile.Tags);
                    newTags.Add(tag);
                    imageFile.Tags = new(newTags.Distinct());
                }

                tag.TaggedImageCount = dbContext
                    .ImageFiles
                    .SelectMany(f => f.Tags)
                    .Where(t => t.Id == tag.Id)
                    .Count();

                Log.Information("Added ({Id}) {Name} tag to {Count} images",
                    tag.Id, tag.Name, numImageFiles);

                messages.Add(new TagAdded
                {
                    Tag = tag,
                    ImageFiles = imageFiles.ToList(),
                });
            }

            foreach (var message in messages)
                Messenger.Send(message);
        }
    }

    public void Receive(RemoveTag message)
    {
        RemoveTags([message.Tag], message.ImageFiles);
    }

    public void Receive(RemoveTags message)
    {
        RemoveTags(message.Tags, message.ImageFiles);
    }

    private void RemoveTags(IEnumerable<TagViewModel> tags, IEnumerable<ImageFileViewModel> imageFiles)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        if (RemoveTagsFromImageFiles(dbContext, tags.Select(t => t.Id), imageFiles.Select(f => f.Id)) > 0)
        {
            var numTags = tags.Count();
            var numImageFiles = imageFiles.Count();
            var tagsCopy = tags.ToList();
            var imageFilesCopy = imageFiles.ToList();
            var messages = new List<TagRemoved>(numTags * numImageFiles);

            foreach (var tag in tagsCopy)
            {
                foreach (var imageFile in imageFilesCopy)
                {
                    imageFile.Tags.Remove(tag);
                }

                tag.TaggedImageCount = dbContext
                    .ImageFiles
                    .SelectMany(f => f.Tags)
                    .Where(t => t.Id == tag.Id)
                    .Count();

                Log.Information("Removed ({Id}) {Name} tag from {Count} images",
                    tag.Id, tag.Name, numImageFiles);

                messages.Add(new TagRemoved
                {
                    Tag = tag,
                    ImageFiles = imageFilesCopy,
                });
            }

            foreach (var message in messages)
                Messenger.Send(message);
        }
    }

    public void Receive(ClearTags message)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        if (ClearTagsFromImageFiles(dbContext, message.ImageFiles.Select(f => f.Id)) > 0)
        {
            var numImageFiles = message.ImageFiles.Count();

            // Remove all tags from the UI
            foreach (var imageFile in message.ImageFiles)
            {
                imageFile.Tags.Clear();
            }

            Log.Information("Cleared tags on {Count} images", numImageFiles);

            Messenger.Send(new TagsCleared
            {
                ImageFiles = message.ImageFiles.ToList(),
            });
        }
    }

    public void Receive(TagCreated message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Tags.Contains(message.Tag))
                Tags.Add(message.Tag);
        });
    }

    public void Receive(TagDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Tags.Remove(message.Tag);
        });
    }

    [RelayCommand]
    private void CreateTag()
    {
        if (Name is null or { Length: 0 }) return;

        Messenger.Send(new CreateTag
        {
            Name = Name,
            Color = Color,
        });

        Name = null;
        Color = DefaultColor;
        ClearErrors();

        if (View is Control control)
        {
            if (control.Find<TextBox>("TagNameTextBox") is { } tb)
            {
                tb.Focus();
            }
        }
    }

    [RelayCommand]
    private async Task DeleteTag(int id)
    {
        if (await _dialogService.ShowConfirmationDialog("Are you sure you want to delete this tag?"))
        {
            if (Tags.FirstOrDefault(tag => tag.Id == id) is { } tag)
            {
                Messenger.Send(new DeleteTag
                {
                    Tag = tag,
                });
            }
        }
    }

    [RelayCommand]
    private void EditTagComplete(DataGridCellEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit &&
            e.Column.GetCellContent(e.Row)?.DataContext is TagViewModel tag)
        {
            Messenger.Send(new EditTag
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = tag.Color,
            });
        }
    }

    [RelayCommand]
    private void ClearColor()
    {
        Color = DefaultColor;
    }

    [RelayCommand]
    private void ChangeTagColor(ColorChangedEventArgs e)
    {
        if (SelectedTag is not null)
        {
            Messenger.Send(new EditTag
            {
                Id = SelectedTag.Id,
                Name = SelectedTag.Name,
                Color = $"#{e.NewColor.ToUInt32():x8}",
            });
        }
    }
}
