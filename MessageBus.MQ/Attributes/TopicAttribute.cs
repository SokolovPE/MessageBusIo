using System.Runtime.CompilerServices;
using MessageBus.Core.Enums;
using MessageBus.MQ.Enums;

namespace MessageBus.MQ.Attributes;

/// <summary>
///     Описание топика
/// </summary>
[AttributeUsage(AttributeTargets.Class| AttributeTargets.Struct)]
public class TopicAttribute : Attribute
{
    /// <summary>
    ///     .ctor
    /// </summary>
    public TopicAttribute(string connectionAlias, string name,
        RoutingType routingType = RoutingType.Fanout, string ttl = "",
        SerializerType serializerType = SerializerType.Json)
    {
        Name = name;
        RoutingType = routingType;
        ConnectionAlias = connectionAlias;
        Ttl = string.IsNullOrWhiteSpace(ttl) || !int.TryParse(ttl, out var ttlMs)
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(ttlMs);
        SerializerType = serializerType;
    }

    /// <summary>
    ///     Наименование топика
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    ///     Вид маршрутизации
    /// </summary>
    public RoutingType RoutingType { get; }
    
    /// <summary>
    ///     Именование соединения, в которое шлем сообщение
    /// </summary>
    public string ConnectionAlias { get; }

    /// <summary>
    ///     Время через которое автоматически стухнет сообщение
    /// </summary>
    public TimeSpan Ttl { get; }
    
    /// <summary>
    ///     Указание какой сериализатор использовать
    /// </summary>
    public SerializerType SerializerType { get; }

    /// <summary>
    ///     Кол-во попыток на обработку
    /// </summary>
    public int HandleAttempts { get; set; } = 1;
}