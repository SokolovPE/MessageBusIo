namespace MessageBus.Core.Models;

/// <summary>
///     Конфигурация шин сообщений
/// </summary>
public class MessageBusConfig
{
    /// <summary>
    ///     Список подключений
    /// </summary>
    public ConnectionConfig[] Connections { get; init; }
}