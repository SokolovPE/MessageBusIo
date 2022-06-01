using MessageBus.Core.Exceptions;
using MessageBus.Core.Interfaces;
using MessageBus.MQ.Attributes;
using MessageBus.MQ.Models;
using RabbitMQ.Client;

namespace MessageBus.MQ.Implementations;

/// <summary>
///     Шина RabbitMQ
/// </summary>
public partial class RabbitMqMessageBus
{
    /// <inheritdoc />
    public void Publish<T>(T item, string routingKey = "") where T : IMessage
    {
        var connection = GetConnection<T>();
        using var channel = connection.CreateModel();

        // Собрать информацию для публикации
        var info = BuildPublishInfo<T>(channel);
        var message = BuildMessage(item, info.TopicAttribute);
        
        // Отправка
        channel.BasicPublish(info.ExchangeName, routingKey, mandatory: false, basicProperties: info.BasicProperties, body: message);
    }

    /// <summary>
    ///     Отправка сообщений одной пачкой
    /// </summary>
    public void PublishBatch<T>(IEnumerable<T> items, string routingKey = "") where T : IMessage
    {
        // Проверить разрешены ли batch операции впринципе
        var batchAttribute = GetBatchAttribute<T>();
        if (!batchAttribute.Allowed)
            throw new MessageBusException(
                $"Batch operations with {typeof(T).FullName} are not allowed or batch attribute is missing");
        
        var connection = GetConnection<T>();
        using var channel = connection.CreateModel();
        
        // Собрать информацию для публикации
        var info = BuildPublishInfo<T>(channel);
        
        // Создать пачку сообщений
        var batch = channel.CreateBasicPublishBatch();
        foreach (var item in items)
        {
            var message = BuildMessage(item, info.TopicAttribute);
            var bytes = new ReadOnlyMemory<byte>(message);
            batch.Add(info.ExchangeName, routingKey, mandatory: false, properties: info.BasicProperties, bytes);
        }
        batch.Publish();
    }

    /// <summary>
    ///     Собрать информацию для публикации сообщения
    /// </summary>
    private PublishInfo BuildPublishInfo<T>(IModel channel)
    {
        // Если нет exchange - создадим
        var topicAttribute = GetTopicAttribute<T>();
        var exchangeName = DeclareExchange(topicAttribute, channel);
        
        // Собрать детали сообщения
        var args = BuildArguments(topicAttribute, channel);

        return new PublishInfo
        {
            TopicAttribute = topicAttribute,
            ExchangeName = exchangeName,
            BasicProperties = args
        };
    }
    
    /// <summary>
    ///     Построить сообщение
    /// </summary>
    private byte[] BuildMessage<T>(T item, TopicAttribute attribute)
    {
        var serializer = GetSerializer(attribute);
        var message = serializer.Serialize(item);
        return message;
    }

    /// <summary>
    ///     Сборка справочника аргументов из атрибута
    /// </summary>
    private IBasicProperties? BuildArguments(TopicAttribute attribute, IModel? channel)
    {
        var props = channel!.CreateBasicProperties();
        
        // Если есть Ttl - задаем
        if (attribute.Ttl != default && attribute.Ttl > TimeSpan.Zero)
        {
            props.Expiration = attribute.Ttl.TotalMilliseconds.ToString();
        }

        return props;
    }
}