using MessageBus.Core.Enums;
using MessageBus.Core.Exceptions;
using MessageBus.Core.Interfaces;
using MessageBus.Core.Models;
using MessageBus.MQ.Attributes;
using MessageBus.MQ.Enums;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MessageBus.MQ.Implementations;

/// <summary>
///     Шина RabbitMQ
/// </summary>
public partial class RabbitMqMessageBus : IMessageBus
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

    /// <summary>
    ///     Кэш соединений
    /// </summary>
    /// <remarks>Ключ - Alias из конфигураци</remarks>
    private readonly Dictionary<string, IConnection?> _connections = new();

    /// <summary>
    ///     Кэш-связка типа сообщения и соответствующего соединения
    /// </summary>
    /// <remarks>Ключ - тип сообщения</remarks>
    private readonly Dictionary<Type, IConnection> _typeToConnectionReferences = new();
    
    /// <summary>
    ///     Кэш значений атрибутов на типе сообщения
    /// </summary>
    /// <remarks>Ключ - тип сообщения</remarks>
    private readonly Dictionary<Type, TopicAttribute> _topicAttributes = new();

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
}
