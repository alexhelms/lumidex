namespace Lumidex.Common;

public partial class LibraryViewModel : ObservableObject, IEquatable<LibraryViewModel?>
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? LastScan { get; set; }

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
