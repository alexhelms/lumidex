using Avalonia.Threading;
using Lumidex.Features.Tags.Messages;

namespace Lumidex.Features.MainSearch.Actions;

public partial class TagsActionViewModel : ActionViewModelBase,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>,
    IRecipient<TagAdded>,
    IRecipient<TagEdited>,
    IRecipient<TagRemoved>,
    IRecipient<TagsCleared>
{
    [ObservableProperty] AvaloniaList<TagViewModel> _allTags = new();
    [ObservableProperty] AvaloniaList<TagViewModel> _selectedTags = new();
    [ObservableProperty] AvaloniaList<TagViewModel> _tagsOfSelectedItems = new();

    public TagsActionViewModel()
    {
        DisplayName = "Tags";
    }

    protected override void OnSelectedItemsChanged()
    {
        TagsOfSelectedItems = new(
            SelectedItems
                .SelectMany(f => f.Tags)
                .Distinct()
        );
    }

    public void Receive(TagCreated message)
    {
        if (!AllTags.Contains(message.Tag))
            AllTags.Add(message.Tag);
    }

    public void Receive(TagDeleted message)
    {
        AllTags.Remove(message.Tag);
    }

    public void Receive(TagAdded message)
    {
        if (message.ImageFiles.Any(f => SelectedIds.Contains(f.Id)))
        {
            Dispatcher.UIThread.Invoke(OnSelectedItemsChanged);
        }
    }

    public void Receive(TagEdited message)
    {
        UpdateTag(AllTags, message);
        UpdateTag(TagsOfSelectedItems, message);

        static void UpdateTag(IEnumerable<TagViewModel> tags, TagEdited message)
        {
            if (tags.FirstOrDefault(t => t.Id == message.Tag.Id) is { } tag)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    tag.Name = message.Tag.Name;
                    tag.Color = message.Tag.Color;
                });
            }
        }
    }

    public void Receive(TagRemoved message)
    {
        if (message.ImageFiles.Any(f => SelectedIds.Contains(f.Id)))
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                TagsOfSelectedItems.Remove(message.Tag);
            });
        }
    }

    public void Receive(TagsCleared message)
    {
        if (message.ImageFiles.Any(f => SelectedIds.Contains(f.Id)))
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                TagsOfSelectedItems.Clear();
            });
        }
    }

    [RelayCommand]
    private void AddTags()
    {
        Messenger.Send(new AddTags
        {
            Tags = SelectedTags,
            ImageFiles = SelectedItems,
        });
    }

    [RelayCommand]
    private void RemoveAllTags()
    {
        Messenger.Send(new RemoveTags
        {
            Tags = TagsOfSelectedItems,
            ImageFiles = SelectedItems,
        });
    }

    [RelayCommand]
    private void RemoveTag(TagViewModel tag)
    {
        Messenger.Send(new RemoveTag
        {
            Tag = tag,
            ImageFiles = SelectedItems,
        });
    }
}
