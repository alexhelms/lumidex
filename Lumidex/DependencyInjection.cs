using Lumidex.Features.MainSearch.Actions;
using Lumidex.Features.MainSearch.Editing;
using Lumidex.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex;

public static class DependencyInjection
{
    public static void AddLumidexUi(this IServiceCollection services)
    {
        RegisterAllDerivedTypes<IViewModelBase>(services);
        RegisterLazyForType<IViewModelBase>(services);

        RegisterAllDerivedTypes<Avalonia.Controls.Window>(services);
        RegisterAllDerivedTypes<Avalonia.Controls.UserControl>(services);

        services.AddTransient<Func<EditItemsViewModel>>(p => () => p.GetRequiredService<EditItemsViewModel>());
        services.AddTransient<Func<ImageFileInfoItem>>(p => () => p.GetRequiredService<ImageFileInfoItem>());

        services.AddSingleton<DialogService>();
        services.AddSingleton<SystemService>();
    }

    private static IEnumerable<Type> GetDerivedTypes<T>()
        => typeof(IViewModelBase).Assembly
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
