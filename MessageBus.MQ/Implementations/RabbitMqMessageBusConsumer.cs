using System.Runtime.CompilerServices;
using MessageBus.Core.Exceptions;
using MessageBus.Core.Interfaces;
using MessageBus.Core.Types;
using MessageBus.MQ.Attributes;
using MessageBus.MQ.Enums;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageBus.MQ.Implementations;

/// <summary>
///     Шина RabbitMQ
/// </summary>
public partial class RabbitMqMessageBus
{
    /// <summary>
    ///     Подписаться на exchange
    /// </summary>
    /// <remarks>Подписка не напрямую, а всегда через очередь</remarks>
    public void Subscribe<T>(Action<T> handler, [CallerMemberName] string consumerName = "", string routingKey = "",
        ushort prefetchCount = 0) where T : IMessage
    {
        var connection = GetConnection<T>();
        var channel = connection.CreateModel();

        // Если нет очереди - создадим и привяжем к exchange
        // Имя очереди = Exchange$ConsumerName
        var topicAttribute = GetTopicAttribute<T>();

        // Если вид топика не BindingKey - не важно что передали, ставим пусто
        var routeKey = topicAttribute.RoutingType == RoutingType.BindingKey ? routingKey : string.Empty;
        var topicName = DeclareTopic(topicAttribute, consumerName, channel, routeKey);

        // Задаем prefetch
        channel.BasicQos(0, prefetchCount, false);

        // Подписываемся
        var consumer = CreateConsumer(channel, consumerName);
        consumer.Received += (s, e) =>
        {
            OnReceived(s, e, handler, topicAttribute);

            // Помечаем сообщение обработанным, какой бы результат ни был
            channel.BasicAck(e.DeliveryTag, false);
        };
        channel.BasicConsume(topicName, false, consumer);
        _activeSubscriptions.Add(channel!.ChannelNumber, channel);
    }

    /// <summary>
    ///     Подписаться на обработку сообщений пачками
    /// </summary>
    public void SubscribeBatch<T>(Action<T> handler, [CallerMemberName] string consumerName = "",
        string routingKey = "") where T : IMessage
    {
        // Проверить разрешены ли batch операции впринципе
        var batchAttribute = GetBatchAttribute<T>();
        if (!batchAttribute.Allowed)
            throw new MessageBusException(
                $"Batch operations with {typeof(T).FullName} are not allowed or batch attribute is missing");
        
        var connection = GetConnection<T>();
        var channel = connection.CreateModel();

        // Если нет очереди - создадим и привяжем к exchange
        // Имя очереди = Exchange$ConsumerName
        var topicAttribute = GetTopicAttribute<T>();

        // Если вид топика не BindingKey - не важно что передали, ставим пусто
        var routeKey = topicAttribute.RoutingType == RoutingType.BindingKey ? routingKey : string.Empty;
        var topicName = DeclareTopic(topicAttribute, consumerName, channel, routeKey);

        // Задаем prefetch
        channel.BasicQos(0, batchAttribute.BatchSize, false);
        
        // Создаем список с обрарботкой по таймеру либо превышению лимита
        var bag = new TimerBag<T>(batchAttribute.BatchDelay, batchAttribute.BatchSize);
        bag.BatchProcessStarted += batchSize => _logger.LogInformation("{ExecTime} Batch process of {TypeName} started, batchSize={Size}",
            DateTime.Now.ToString("hh:mm:ss"), typeof(T).FullName, batchSize);
        bag.BatchProcessFailed += e =>
            _logger.LogError(e, "Batch process of {TypeName} error: {Message}",
                e.Message, typeof(T).FullName);
        // bag.BatchProcessSucceeded +=
        //     () => _logger.LogInformation("[{TotalFires}] Batch process succeeded", bag.TotalFires);
        bag.ItemProcess += item =>
        {
            try
            {
                handler.Invoke(item);
            }
            catch (Exception e)
            {
                Redeliver(item, topicAttribute);
                _logger.LogError(e, "Failed to handle {TypeName} at {Topic}", typeof(T).FullName, topicName);
            }
        };

        // Подписываемся
        var consumer = CreateConsumer(channel, consumerName);
        consumer.Received += (_, e) =>
        {
            OnReceivedBatch(e, bag, topicAttribute);

            // Помечаем сообщение обработанным, какой бы результат ни был
            channel.BasicAck(e.DeliveryTag, false);
        };
        consumer.Shutdown += (_, _) => bag.Dispose();
        
        channel.BasicConsume(topicName, false, consumer);
        _activeSubscriptions.Add(channel!.ChannelNumber, channel);
    }
    
    /// <summary>
    ///     Создание подписчика
    /// </summary>
    private EventingBasicConsumer CreateConsumer(IModel? channel, string consumerName)
    {
        var consumer = new EventingBasicConsumer(channel);
        var key = channel!.ChannelNumber;
        consumer.Registered += (_, _) => _logger.LogInformation("Subscription {ConsumerName} registered", consumerName);
        consumer.Unregistered += (_, _) => _logger.LogInformation("Subscription {ConsumerName} unregistered", consumerName);
        consumer.Shutdown += (_, e) =>
        {
            // Если висит открытый канал - закрываем его
            var activeCh = _activeSubscriptions.TryGetValue(key, out var ch);
            if (activeCh)
            {
                if (ch!.IsOpen)
                {
                    ch.Close();
                    ch.Dispose();
                }
                _activeSubscriptions.Remove(key);
            }
            _logger.LogInformation("Subscription {ConsumerName} shutdown because: {Reason}", consumerName, e.ReplyText);
        };
        return consumer;
    }
    
    /// <summary>
    ///     Событие появления нового сообщения в batch подписчике
    /// </summary>
    private void OnReceivedBatch<T>(BasicDeliverEventArgs e, TimerBag<T> bag, TopicAttribute attribute)
    {
        try
        {
            var serializer = GetSerializer(attribute);
            var content = serializer.Deserialize<T>(e.Body.ToArray());
            if(content != null)
                bag.Add(content);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "There's an error during consume {DeliveryTag} at {Exchange}", e.DeliveryTag,
                e.Exchange);
        }
    }
    
    /// <summary>
    ///     Обработчик входящего сообщения
    /// </summary>
    private void OnReceived<T>(object? sender, BasicDeliverEventArgs e, Action<T?> handler, TopicAttribute attribute)
    {
        try
        {
            _logger.LogInformation("New message {DeliveryTag} at {Exchange}", e.DeliveryTag, e.Exchange);
            var serializer = GetSerializer(attribute);
            var content = serializer.Deserialize<T>(e.Body.ToArray());
            try
            {
                handler.Invoke(content);
                _logger.LogInformation("Message {DeliveryTag} successfully handled at {Exchange}", e.DeliveryTag,
                    e.Exchange);
            }
            catch (Exception exception)
            {
                Redeliver(content, attribute);
                _logger.LogError(exception, "Failed to handle {DeliveryTag} at {Exchange}", e.DeliveryTag,
                    e.Exchange);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "There's an error during consume {DeliveryTag} at {Exchange}", e.DeliveryTag,
                e.Exchange);
        }
    }

    /// <summary>
    ///     Переотправить сообщение в очередь если не удалось обработать сразу
    /// </summary>
    private void Redeliver<T>(T? content, TopicAttribute attribute)
    {
        // Пока заглушка
        // В хэдеры запишем номер попытки
        // Если попыток больше чем константа - удаляем сообщение
    }
}