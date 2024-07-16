using Lumidex.Features.Aliases.Messages;

namespace Lumidex.Features.Aliases;

public partial class ObjectNameViewModel : ViewModelBase
{
    [ObservableProperty] string _objectName = string.Empty;
    [ObservableProperty] ObservableCollectionEx<AliasViewModel> _aliases = new();
    [ObservableProperty] string? _newAliasName;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as ObjectNameViewModel);
    }

    public bool Equals(ObjectNameViewModel? other)
    {
        return other is not null &&
               ObjectName.Equals(other.ObjectName, StringComparison.InvariantCultureIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ObjectName);
    }

    public static bool operator ==(ObjectNameViewModel? left, ObjectNameViewModel? right)
    {
        return EqualityComparer<ObjectNameViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(ObjectNameViewModel? left, ObjectNameViewModel? right)
    {
        return !(left == right);
    }

    #endregion

    [RelayCommand]
    private void CreateAlias()
    {
        if (NewAliasName is { Length: > 0 })
        {
            Messenger.Send(new CreateAlias
            {
                ObjectName = ObjectName,
                Alias = NewAliasName.Trim(),
            });

            NewAliasName = null;
        }
    }

    [RelayCommand]
    private void DeleteAlias(AliasViewModel alias)
    {
        if (alias.Id > 0)
        {
            Messenger.Send(new DeleteAlias
            {
                Id = alias.Id,
            });
        }
    }
}
