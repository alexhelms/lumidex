using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class LibraryViewModel : ObservableObject, IEquatable<LibraryViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] string _name = string.Empty;
    [ObservableProperty] string _path = string.Empty;
    [ObservableProperty] DateTime? _lastScan;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as LibraryViewModel);
    }

    public bool Equals(LibraryViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(LibraryViewModel? left, LibraryViewModel? right)
    {
        return EqualityComparer<LibraryViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(LibraryViewModel? left, LibraryViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}

public static class LibraryMapper
{
    public static LibraryViewModel ToViewModel(Library library)
    {
        var libraryViewModel = new LibraryViewModel
        {
            Id = library.Id,
            Name = library.Name,
            Path = library.Path,
            LastScan = library.LastScan,
        };

        return libraryViewModel;
    }
}