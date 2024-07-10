using AutoMapper;
using AutoMapper.QueryableExtensions;
using Avalonia.Controls;
using Avalonia.Media;
using Lumidex.Core.Data;
using Lumidex.Features.Tags.Messages;
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

    private readonly IServiceProvider _serviceProvider;
    private readonly IMapper _mapper;
    private readonly LumidexDbContext _dbContext;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(CreateTagCommand))]
    private string? _name;

    [ObservableProperty] Color _color = DefaultColor;
    [ObservableProperty] AvaloniaList<TagViewModel> _tags = new();

    public TagManagerViewModel(
        IServiceProvider serviceProvider,
        IMapper mapper,
        LumidexDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _mapper = mapper;
        _dbContext = dbContext;

        Tags = new(
            _dbContext
                .Tags
                .AsNoTracking()
                .Include(tag => tag.TaggedImages)
                .OrderByDescending(tag => tag.Id)
                .ProjectTo<TagViewModel>(_mapper.ConfigurationProvider)
                .ToList()
        );
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        foreach (var tag in Tags)
        {
            Messenger.Send(new TagCreated { Tag = tag });
        }
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
        var tag = new Tag
        {
            Name = message.Name,
            Color = $"#{message.Color.ToUInt32():x8}",
        };

        _dbContext.Tags.Add(tag);
        if (_dbContext.SaveChanges() > 0)
        {
            // Update UI
            var tagVm = _mapper.Map<TagViewModel>(tag);
            Tags.Insert(0, tagVm);

            Messenger.Send(new TagCreated
            {
                Tag = tagVm,
            });
        }
    }

    public void Receive(DeleteTag message)
    {
        var tag = _dbContext.Tags.FirstOrDefault(tag => tag.Id == message.Tag.Id);
        if (tag is not null)
        {
            _dbContext.Tags.Remove(tag);
            if (_dbContext.SaveChanges() > 0)
            {
                // Update UI
                var tagVm = Tags.First(t => t.Id == message.Tag.Id);
                Tags.Remove(tagVm);

                Messenger.Send(new TagDeleted
                {
                    Tag = tagVm,
                });
            }
        }
    }

    public void Receive(EditTag message)
    {
        var tag = _dbContext.Tags.FirstOrDefault(tag => tag.Id == message.Id);
        if (tag is not null)
        {
            tag.Name = message.Name;
            tag.Color = $"#{message.Color.ToUInt32():x8}";

            if (_dbContext.SaveChanges() > 0)
            {
                // Update UI
                var tagVm = Tags.First(t => t.Id == message.Id);
                tagVm.Name = tag.Name;
                tagVm.Color = Color.Parse(tag.Color);

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
        if (_dbContext.AddTagsToImageFiles(tags.Select(t => t.Id), imageFiles.Select(f => f.Id)) > 0)
        {
            foreach (var tag in tags)
            {
                foreach (var imageFile in imageFiles)
                {
                    var newTags = new List<TagViewModel>(imageFile.Tags);
                    newTags.Add(tag);
                    imageFile.Tags = new(newTags.Distinct());
                }

                Messenger.Send(new TagAdded
                {
                    Tag = tag,
                    ImageFiles = imageFiles.ToList(),
                });
            }
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
        if (_dbContext.RemoveTagsFromImageFiles(tags.Select(t => t.Id), imageFiles.Select(f => f.Id)) > 0)
        {
            var tagsCopy = tags.ToList();
            var imageFilesCopy = imageFiles.ToList();

            foreach (var tag in tagsCopy)
            {
                foreach (var imageFile in imageFilesCopy)
                {
                    imageFile.Tags.Remove(tag);
                }

                Messenger.Send(new TagRemoved
                {
                    Tag = tag,
                    ImageFiles = imageFilesCopy,
                });
            }
        }
    }

    public void Receive(ClearTags message)
    {
        if (_dbContext.ClearTagsFromImageFiles(message.ImageFiles.Select(f => f.Id)) > 0)
        {
            // Remove all tags from the UI
            foreach (var imageFile in message.ImageFiles)
            {
                imageFile.Tags.Clear();
            }

            Messenger.Send(new TagsCleared
            {
                ImageFiles = message.ImageFiles.ToList(),
            });
        }
    }

    public void Receive(TagCreated message)
    {
        if (!Tags.Contains(message.Tag))
            Tags.Add(message.Tag);
    }

    public void Receive(TagDeleted message)
    {
        Tags.Remove(message.Tag);
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
    private void DeleteTag(int id)
    {
        var tag = Tags.FirstOrDefault(tag => tag.Id == id);
        if (tag is not null)
        {
            Messenger.Send(new DeleteTag
            {
                Tag = tag,
            });
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
    private void ChangeTagColor(TagViewModel? tag)
    {
        if (tag is null) return;

        Messenger.Send(new EditTag
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
        });
    }
}
