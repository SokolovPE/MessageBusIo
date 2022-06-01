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
        RoutingType routingType = RoutingType.Fanout, int ttl = 0,
        SerializerType serializerType = SerializerType.Json, int batchDelay = 0, ushort batchSize = 0)
    {
        Name = name;
        RoutingType = routingType;
        ConnectionAlias = connectionAlias;
        Ttl = ttl <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(ttl);
        SerializerType = serializerType;
        BatchDelay = batchDelay <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(batchDelay);
        BatchSize = batchSize <= 0 ? (ushort)1 : batchSize;
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
    public int HandleAttempts { get; } = 1;
    
    /// <summary>
    ///     Задержка на время сборка пачки
    /// </summary>
    public TimeSpan BatchDelay { get; }
    
    /// <summary>
    ///     Максимальный размер пачки
    /// </summary>
    public ushort BatchSize { get; }
}