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
    /// <param name="connectionAlias">Alias используемого соединения</param>
    /// <param name="name">Наименование топика</param>
    /// <param name="routingType">Вид маршрутизации</param>
    /// <param name="ttl">Время через которое автоматически стухнет сообщение</param>
    /// <param name="serializerType">Указание какой сериализатор использовать</param>
    public TopicAttribute(string connectionAlias, string name,
        RoutingType routingType = RoutingType.Fanout, int ttl = 0,
        SerializerType serializerType = SerializerType.Json)
    {
        Name = name;
        RoutingType = routingType;
        ConnectionAlias = connectionAlias;
        Ttl = ttl <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(ttl);
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
    public int HandleAttempts { get; } = 1;
}