using System.Reflection;

namespace System.Linq.Expressions;

public static class ExpressionExtensions
{
    public static PropertyInfo GetPropertyInfo<T, U>(this Expression<Func<T, U>> propertyExpression)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);

        if (propertyExpression.Body is not MemberExpression body)
        {
            throw new ArgumentException("Invalid argument", nameof(propertyExpression));
        }

        if (body.Member is not PropertyInfo info)
        {
            throw new ArgumentException("Argument is not a property", nameof(propertyExpression));
        }

        return info;
    }
}
