using System.Runtime.CompilerServices;
using MessageBus.Core;
using MessageBus.Core.Enums;
using MessageBus.Core.Exceptions;
using MessageBus.Core.Interfaces;
using MessageBus.MQ.Attributes;
using MessageBus.MQ.Enums;
using MessageBus.MQ.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace MessageBus.MQ.Implementations;

/// <summary>
///     Шина RabbitMQ
/// </summary>
public class RabbitMqMessageBus : IMessageBus
{
    /// <summary>
    ///     Логировщик
    /// </summary>
    private readonly ILogger<RabbitMqMessageBus> _logger;

    /// <summary>
    ///     Будет ли сообщения в очереди/exchange восстановлены после ребута кролика
    /// </summary>
    private const bool Durable = true;

    /// <summary>
    ///     Разрешено ли автоматически конфигурировать exchange
    /// </summary>
    private const bool ExchangeReconfigureEnabled = false;

    /// <summary>
    ///     Набор сериализаторов
    /// </summary>
    private readonly Dictionary<SerializerType, IMessageBusSerializer> _serializers;

    /// <summary>
    ///     Набор активных подписок
    /// </summary>
    /// <remarks>Ключ - номер канала</remarks>
    private Dictionary<int, IModel?> _activeSubscriptions = new();

    private readonly Dictionary<string, IConnection?> _connections = new();

    private Dictionary<Type, IConnection> _typeToConnectionReferences = new();
    
    private Dictionary<Type, TopicAttribute> _topicAttributes = new();

    /// <summary>
    ///     .ctor
    /// </summary>
    public RabbitMqMessageBus(ILogger<RabbitMqMessageBus> logger, MessageBusConfig config,
        IEnumerable<IMessageBusSerializer> serializers)
    {
        _logger = logger;
        Initialize(config);
        _serializers = serializers.ToDictionary(serializer => serializer.Type);
    }

    /// <summary>
    ///     Инициализатор шины
    /// </summary>
    private void Initialize(MessageBusConfig config)
    {
        // Отфильтруем 
        var rabbitMqConfigs = 
            config.Connections.Where(Validate).ToArray();
        
        // Для каждого валидного конфига создать подключение
        foreach (var conf in rabbitMqConfigs)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(conf.ConnectionString),
                SocketReadTimeout = TimeSpan.FromSeconds(conf.Timeout),
                SocketWriteTimeout = TimeSpan.FromSeconds(conf.Timeout),
                AutomaticRecoveryEnabled = true
            };
            
            var connection = factory.CreateConnection();
            
            try
            {
                // Наружу торчим только сам открытый коннект, воизбежание тонны коннектов открытых
                _connections.Add(conf.Alias, connection);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Impossible to add connection for {Alias}", conf.Alias);
            }
        }
    }

    /// <summary>
    ///     Валидация конфигураций
    /// </summary>
    private bool Validate(ConnectionConfig config) =>
        !string.IsNullOrWhiteSpace(config.Alias) && !string.IsNullOrWhiteSpace(config.ConnectionString) &&
        config.TransportType == TransportType.RabbitMq;

    /// <summary>
    ///     Очистим за собой коннекты
    /// </summary>
    public void Dispose()
    {
        // Закрываем каналы
        foreach (var (channelNum, value) in _activeSubscriptions)
        {
            if (value == null) continue;
            value.Close();
            value.Dispose();
            _logger.LogInformation("Subscription channel {ChNum} closed", channelNum);
        }
        
        // Очищаем соединения
        foreach (var (connectionName, value) in _connections)
        {
            value?.Dispose();
            _logger.LogInformation("Connection {Name} closed", connectionName);
        }
    }

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
    public void BatchPublish<T>(IEnumerable<T> items, string routingKey = "") where T : IMessage
    {
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
            ConsumerOnReceived(s, e, handler, topicAttribute);

            // Помечаем сообщение обработанным, какой бы результат ни был
            channel.BasicAck(e.DeliveryTag, false);
        };
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
    ///     Обработчик входящего сообщения
    /// </summary>
    private void ConsumerOnReceived<T>(object? sender, BasicDeliverEventArgs e, Action<T?> handler, TopicAttribute attribute)
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

    /// <summary>
    ///     Создать очередь и привязать к Exchange
    /// </summary>
    private string DeclareTopic(TopicAttribute attribute, string consumerName, IModel? channel, string routingKey = "")
    {
        var topicName = $"{attribute.Name}${consumerName}";
        
        // Регистрация очереди, идемпотентно - можно дергать сколько хочется
        channel.QueueDeclare(topicName, durable: Durable, exclusive: false, autoDelete: false);
        
        // Привязка к exchange
        channel.QueueBind(topicName, attribute.Name, routingKey);

        return topicName;
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
    ///     Получить сериализатор
    /// </summary>
    private IMessageBusSerializer GetSerializer(TopicAttribute attribute)
    {
        var serializerFound = _serializers.TryGetValue(attribute.SerializerType, out var serializer);
        if (!serializerFound || serializer == null)
            throw new MessageBusException($"MessageBus serializer {attribute.SerializerType} not supported yet");
        
        return serializer;
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

    /// <summary>
    ///     Создать exchange
    /// </summary>
    private string DeclareExchange(TopicAttribute attribute, IModel channel, bool firstAttempt = true)
    {
        var type = attribute.RoutingType switch
        {
            RoutingType.Fanout => ExchangeType.Fanout,
            RoutingType.BindingKey => ExchangeType.Direct,
            _ => throw new MessageBusException($"{attribute.RoutingType} is not supported yet")
        };
        
        // Операция идемпотентная, дергай сколько душе влезет
        try
        {
            channel.ExchangeDeclare(attribute.Name, type, durable: Durable);
        }
        catch (OperationInterruptedException e)
        {
            // Если конфигурация exchange изменилась - пересоздадим
            if (firstAttempt && e.ShutdownReason.ReplyCode == 406 && ExchangeReconfigureEnabled)
                ReconfigureExchange(channel, attribute);
            else
                throw;
        }

        return attribute.Name;
    }

    /// <summary>
    ///     Пересоздать Exchange
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="attribute"></param>
    private void ReconfigureExchange(IModel channel, TopicAttribute attribute)
    {
        // W.I.P
        channel.ExchangeDelete(attribute.Name);
        DeclareExchange(attribute, channel, false);
    }

    /// <summary>
    ///     Получить соединение для данной сущности
    /// </summary>
    private IConnection GetConnection<T>()
    {
        var type = typeof(T);
        
        // Проверим есть ли метадата в кэше
        var cached = _typeToConnectionReferences.TryGetValue(type, out var connection);
        
        // Если нет - найдем соответствующее соединение и запишем мету в кэш
        if (cached) return connection ?? throw new MessageBusException($"Connection is empty for {type.FullName}");
        
        // Ищем в конкретной сущности, наследование атрибута запрещено
        var topic = GetTopicAttribute<T>();
        
        var connectionFound = _connections.TryGetValue(topic.ConnectionAlias, out connection);
            
        if(!connectionFound)
            throw new MessageBusException($"Connection with alias {topic.ConnectionAlias} not found");

        if(connection == null)
            throw new MessageBusException($"Connection is empty for {type.FullName}");
        
        // Добавим в мету
        _typeToConnectionReferences.TryAdd(type, connection);

        return connection;
    }

    /// <summary>
    ///     Получить атрибут с информацией по топику из сущности
    /// </summary>
    private TopicAttribute GetTopicAttribute<T>()
    {
        var type = typeof(T);
        
        // Сначала поиск в кэше
        var cached = _topicAttributes.TryGetValue(type, out var attribute);
        if (cached)
            return attribute ?? throw new MessageBusException($"Topic attribute is empty somehow for {type.FullName}");
        
        // Если не нашлось - найдем и закэшируем
        var attributesFound = type.GetCustomAttributes(typeof(TopicAttribute), false);
        if (attributesFound.Length > 1)
            throw new MessageBusException($"Model {typeof(T).FullName} contains multiple Topic attributes somehow");
        var topic = (TopicAttribute) attributesFound[0];
        
        _topicAttributes.Add(type, topic);
        return topic;
    }
}
