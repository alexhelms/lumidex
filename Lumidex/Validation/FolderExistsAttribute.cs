using System.ComponentModel.DataAnnotations;

namespace Lumidex.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class FolderExistsAttribute : ValidationAttribute
{
    public override string FormatErrorMessage(string name)
    {
        return $"{name} does not exist.";
    }

    public override bool IsValid(object? value)
    {
        if (value is string s)
        {
            return Directory.Exists(s);
        }

        return false;
    }
}
