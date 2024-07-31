namespace Lumidex.Common;

public partial class AstrobinFilterViewModel : ViewModelBase, IEquatable<AstrobinFilterViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] int _astrobinId;
    [ObservableProperty] string _name = string.Empty;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as AstrobinFilterViewModel);
    }

    public bool Equals(AstrobinFilterViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(AstrobinFilterViewModel? left, AstrobinFilterViewModel? right)
    {
        return EqualityComparer<AstrobinFilterViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(AstrobinFilterViewModel? left, AstrobinFilterViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}
