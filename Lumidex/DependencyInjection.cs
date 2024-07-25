using Lumidex.Features.MainSearch.Editing;
using Lumidex.Features.MainSearch.Filters;
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

        // Filters
        services.AddTransient<Func<ObjectNameFilter>>(p => () => p.GetRequiredService<ObjectNameFilter>());
        services.AddTransient<Func<LibraryFilter>>(p => () => p.GetRequiredService<LibraryFilter>());
        services.AddTransient<Func<ImageTypeFilter>>(p => () => p.GetRequiredService<ImageTypeFilter>());
        services.AddTransient<Func<ImageKindFilter>>(p => () => p.GetRequiredService<ImageKindFilter>());
        services.AddTransient<Func<ExposureFilter>>(p => () => p.GetRequiredService<ExposureFilter>());
        services.AddTransient<Func<FilterFilter>>(p => () => p.GetRequiredService<FilterFilter>());
        services.AddTransient<Func<ObservationBeginFilter>>(p => () => p.GetRequiredService<ObservationBeginFilter>());
        services.AddTransient<Func<ObservationEndFilter>>(p => () => p.GetRequiredService<ObservationEndFilter>());
        services.AddTransient<Func<TagFilter>>(p => () => p.GetRequiredService<TagFilter>());

        services.AddSingleton<DialogService>();
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
