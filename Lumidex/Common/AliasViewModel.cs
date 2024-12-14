namespace Lumidex.Common;

public partial class AliasViewModel : ObservableObject, IEquatable<AliasViewModel?>
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string ObjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Alias { get; set; } = string.Empty;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as AliasViewModel);
    }

    public bool Equals(AliasViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(AliasViewModel? left, AliasViewModel? right)
    {
        return EqualityComparer<AliasViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(AliasViewModel? left, AliasViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}
