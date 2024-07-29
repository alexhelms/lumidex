using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.Library.Messages;

namespace Lumidex.Features.MainSearch.Filters;

public partial class LibraryFilter : FilterViewModelBase,
    IRecipient<LibraryCreated>,
    IRecipient<LibraryDeleted>,
    IRecipient<LibraryEdited>
{
    [ObservableProperty] LibraryViewModel? _library;
    [ObservableProperty] ObservableCollectionEx<LibraryViewModel> _libraries = new();
    
    public override string DisplayName => "Library";

    protected override void OnClear() => Library = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Library is { Id: > 0})
        {
            int id = Library.Id;
            query = query.Where(f => f.LibraryId == id);
        }

        return query;
    }

    public void Receive(LibraryCreated message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Libraries.Contains(message.Library))
                Libraries.Add(message.Library);
        });
    }

    public void Receive(LibraryDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Libraries.Remove(message.Library);
        });
    }

    public void Receive(LibraryEdited message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {

            if (Libraries.FirstOrDefault(library => library == message.Library) is { } existingLibrary)
            {
                // Retain a reference to the selected library, if one exists. Removing it from the list deselects!
                LibraryViewModel? selectedLibrary = Library;

                var index = Libraries.IndexOf(existingLibrary);
                Libraries.RemoveAt(index);
                Libraries.Insert(index, message.Library);

                // Retain user selection
                if (selectedLibrary is not null && selectedLibrary == message.Library)
                    Library = message.Library;
            }
        });
    }

    public override string ToString() => $"{DisplayName} = {Library?.Name}";
}