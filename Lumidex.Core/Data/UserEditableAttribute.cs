namespace Lumidex.Core.Data;

/// <summary>
/// A property with this attribute is intended to be user editable in the UI.
/// E.g. properties on the <see cref="ImageFile"/> class, like <see cref="ImageFile.FilterName"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public sealed class UserEditableAttribute : Attribute
{
}
