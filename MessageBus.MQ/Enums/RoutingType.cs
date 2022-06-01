namespace MessageBus.MQ.Enums;

/// <summary>
///     Вид маршрутизации
/// </summary>
public enum RoutingType
{
    /// <summary>
    ///     Отправка всем подписчикам
    /// </summary>
    Fanout,
    
    /// <summary>
    ///     Отправка по ключу
    /// </summary>
    BindingKey
}
