using MessageBus.MQ.Attributes;
using RabbitMQ.Client;

namespace MessageBus.MQ.Models;

/// <summary>
///     Сборник информации для публикации сообщения
/// </summary>
public class PublishInfo
{
    /// <summary>
    ///     Атрибут с описанием параметров
    /// </summary>
    public TopicAttribute TopicAttribute { get; set; }
    
    /// <summary>
    ///     Имя exchange
    /// </summary>
    public string ExchangeName { get; set; }
    
    /// <summary>
    ///     Свойства сообщения
    /// </summary>
    public IBasicProperties? BasicProperties { get; set; }
}