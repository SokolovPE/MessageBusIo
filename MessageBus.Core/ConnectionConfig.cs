using MessageBus.Core.Enums;

namespace MessageBus.Core;

/// <summary>
///     Конфигурация подключения
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    ///     Именование подключения
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    ///     Вид транспорта
    /// </summary>
    public TransportType TransportType { get; set; }
    
    /// <summary>
    ///     Строка подключения
    /// </summary>
    public string ConnectionString { get; init; }

    /// <summary>
    ///     Настройка тайм-аута
    /// </summary>
    public int Timeout { get; set; } = 10;

    /// <summary>
    ///     Используемый сериализатор
    /// </summary>
    public SerializerType SerializerType { get; set; } = SerializerType.Json;
}