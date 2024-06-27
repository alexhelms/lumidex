using Microsoft.Extensions.DependencyInjection;

namespace Lumidex;

public static class DependencyInjection
{
    public static void AddLumidexUi(this IServiceCollection services)
    {
        RegisterAllDerivedTypes<ViewModelBase>(services);
        RegisterLazyForType<ViewModelBase>(services);

        RegisterAllDerivedTypes<Avalonia.Controls.Window>(services);
        RegisterAllDerivedTypes<Avalonia.Controls.UserControl>(services);
    }

    private static IEnumerable<Type> GetDerivedTypes<T>()
        => typeof(ViewModelBase).Assembly
            .GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t))
            .Where(t => t.IsClass)
            .Where(t => !t.IsAbstract)
            .ToArray();

    private static void RegisterAllDerivedTypes<T>(IServiceCollection services)
    {
        foreach (var type in GetDerivedTypes<T>())
        {
            services.AddTransient(type);
        }
    }

    private static void RegisterLazyForType<T>(IServiceCollection services)
    {
        foreach(var type in GetDerivedTypes<T>())
        {
            services.AddTransient(typeof(Lazy<>).MakeGenericType(type));
        }
    }
}
