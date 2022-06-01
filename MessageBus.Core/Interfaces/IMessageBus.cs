using System.Runtime.CompilerServices;

namespace MessageBus.Core.Interfaces;

/// <summary>
///     Описание шины сообщений
/// </summary>
public interface IMessageBus : IDisposable
{
    /// <summary>
    ///     Отправить объект в очередь
    /// </summary>
    void Publish<T>(T item, string routingKey = "") where T : IMessage;

    /// <summary>
    ///     Отправка сообщений одной пачкой
    /// </summary>
    void BatchPublish<T>(IEnumerable<T> items, string routingKey = "") where T : IMessage;

    /// <summary>
    ///     Подписаться на exchange
    /// </summary>
    /// <param name="consumerName">Имя подписчика</param>
    /// <param name="handler">Действие для обработки</param>
    /// <param name="routingKey">Ключ для связки</param>
    /// <param name="prefetchCount">Кол-во сообщений при приемке за раз</param>
    void Subscribe<T>(Action<T> handler, [CallerMemberName]string consumerName = "", string routingKey = "", ushort prefetchCount = 0)
        where T : IMessage;
}
