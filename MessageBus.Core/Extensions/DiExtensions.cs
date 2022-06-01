using System.Reflection;
using MessageBus.Core.Implementations;
using MessageBus.Core.Interfaces;
using MessageBus.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Extensions;

/// <summary>
///     расширения внедрения зависимостей
/// </summary>
public static class DiExtensions
{
    /// <summary>
    ///     Добавить основу MessageBus
    /// </summary>
    public static IServiceCollection AddMessageBusCore(this IServiceCollection serviceCollection,
        MessageBusConfig configuration)
    {
        serviceCollection.AddSingleton(configuration);
        
        // Сканируем сборку на все подключенные MessageBus
        var ass = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => x.FullName != null && !x.FullName.StartsWith("System") && !x.FullName.StartsWith("Microsoft"))
            .ToArray();
        var referenced = ass.SelectMany(a => a.GetReferencedAssemblies()
                .Where(x => x.FullName.StartsWith("MessageBus"))
                .ToArray())
            .Select(Assembly.Load)
            .ToArray();
        var assemblies = ass.Concat(referenced);
        
        var buses = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => !x.FullName.StartsWith("System") && !x.FullName.StartsWith("Microsoft"))
            .Where(x => !x.IsInterface && typeof(IMessageBus).IsAssignableFrom(x))
            .ToArray();
        
        // Регистрация подключенных шин
        foreach (var bus in buses)
        {
            serviceCollection.AddSingleton(typeof(IMessageBus), bus);
        }

        // Регистрация сериализаторов
        serviceCollection.AddSingleton<IMessageBusSerializer, JsonMessageBusSerializer>();
        
        return serviceCollection;
    }
}