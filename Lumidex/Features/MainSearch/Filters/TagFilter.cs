using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.Tags.Messages;

namespace Lumidex.Features.MainSearch.Filters;

public partial class TagFilter : FilterViewModelBase,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>
{
    private HashSet<int> _restoredTagIds = [];

    [ObservableProperty]
    public partial ObservableCollectionEx<TagViewModel> AllTags { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<TagViewModel> SelectedTags { get; set; } = new();

    public override string DisplayName => "Tags";

    protected override void OnClear() => SelectedTags.Clear();

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (SelectedTags.Count > 0)
        {
            var tagIds = SelectedTags.Select(tag => tag.Id).ToHashSet();
            query = query.Where(f => f.Tags.Any(tag => tagIds.Contains(tag.Id)));
        }

        return query;
    }

    public void Receive(TagCreated message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!AllTags.Contains(message.Tag))
            {
                AllTags.Add(message.Tag);

                // Restore the current tag to the selected tags.
                // When the restoration happens, AllTags is empty so
                // restoring had to be deferred until now.
                if (_restoredTagIds.Contains(message.Tag.Id))
                {
                    SelectedTags.Add(message.Tag);
                }
            }
        });
    }

    public void Receive(TagDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            AllTags.Remove(message.Tag);
        });
    }

    public override PersistedFilter? Persist() => SelectedTags.Count == 0
        ? null
        : new PersistedFilter
        {
            Name = "Tags",
            Data = string.Join('|', SelectedTags.Select(tag => tag.Id)),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "Tags")
        {
            var tagIds = persistedFilter.Data?.Split('|') ?? [];
            foreach (var item in tagIds)
            {
                if (int.TryParse(item, out var tagId))
                {
                    _restoredTagIds.Add(tagId);
                    restored = true;
                }
            }
        }

        return restored;
    }

    public override string ToString() => $"{DisplayName} = ({string.Join(", ", SelectedTags.Select(t => t.Name))})";
}
