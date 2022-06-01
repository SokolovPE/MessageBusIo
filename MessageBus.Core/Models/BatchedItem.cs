namespace MessageBus.Core.Models;

/// <summary>
///     Сообщение, отложенное в пачку
/// </summary>
public class BatchedItem<T>
{
    /// <summary>
    ///     Дата и время помещения в пачки
    /// </summary>
    /// <remarks>Используется для сортировки</remarks>
    public DateTime CreationTime { get; } = DateTime.Now;

    /// <summary>
    ///     Отложенный объект обработки
    /// </summary>
    public T Item { get; }

    /// <summary>
    ///     .ctor
    /// </summary>
    public BatchedItem(T item)
    {
        Item = item;
    }
}