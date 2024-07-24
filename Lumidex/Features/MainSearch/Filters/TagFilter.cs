using Lumidex.Core.Data;
using Lumidex.Features.Tags.Messages;

namespace Lumidex.Features.MainSearch.Filters;

public partial class TagFilter : FilterViewModelBase,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>
{
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _allTags = new();
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _selectedTags = new();

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
        if (!AllTags.Contains(message.Tag))
            AllTags.Add(message.Tag);
    }

    public void Receive(TagDeleted message)
    {
        AllTags.Remove(message.Tag);
    }

    public override string ToString() => $"{DisplayName} = ({string.Join(", ", SelectedTags.Select(t => t.Name))})";
}
