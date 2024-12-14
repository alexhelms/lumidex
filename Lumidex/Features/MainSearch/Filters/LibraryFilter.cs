using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.Library.Messages;
using System.Xml.Linq;

namespace Lumidex.Features.MainSearch.Filters;

public partial class LibraryFilter : FilterViewModelBase,
    IRecipient<LibraryCreated>,
    IRecipient<LibraryDeleted>,
    IRecipient<LibraryEdited>
{
    private int? _restoredLibraryId;

    [ObservableProperty]
    public partial LibraryViewModel? Library { get; set; }

    [ObservableProperty]
    public partial ObservableCollectionEx<LibraryViewModel> Libraries { get; set; } = new();

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
            {
                Libraries.Add(message.Library);
                
                // Set the library to the restored ID.
                // When the restoration happens, Libraries is empty so
                // restoring had to be deferred until now.
                if (_restoredLibraryId.HasValue &&
                    message.Library.Id == _restoredLibraryId.Value)
                {
                    Library = message.Library;
                }
            }
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

    public override PersistedFilter? Persist() => Library is null
        ? null
        : new PersistedFilter
        {
            Name = "Library",
            Data = Library.Id.ToString(),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "Library" &&
            int.TryParse(persistedFilter.Data, out var libraryId))
        {
            _restoredLibraryId = libraryId;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Library?.Name}";
}