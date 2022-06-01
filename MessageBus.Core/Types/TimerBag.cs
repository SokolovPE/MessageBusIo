using System.Collections.Concurrent;
using MessageBus.Core.Models;

namespace MessageBus.Core.Types;

/// <summary>
///     Коллекция, по заданному промежутку времени очищающая себя
///     и выполняющая заданное действие
/// </summary>
public sealed class TimerBag<T> : ConcurrentBag<BatchedItem<T>>, IDisposable
{
    /// <summary>
    ///     Отсчет времени
    /// </summary>
    private readonly System.Timers.Timer _timer;

    /// <summary>
    ///     Максимальный размер
    /// </summary>
    private readonly int _maxSize;

    /// <summary>
    ///     Интервал таймера
    /// </summary>
    private readonly TimeSpan _delay;

    /// <summary>
    ///     Последнее срабатывание
    /// </summary>
    public DateTime LastFire { get; private set; }

    /// <summary>
    ///     Событие начала обработки элементов
    /// </summary>
    public delegate void BatchStartHandler(int batchItems);

    public event BatchStartHandler? BatchProcessStarted;

    /// <summary>
    ///     Событие обработки элемента
    /// </summary>
    public delegate void ItemHandler(T item);

    public event ItemHandler? ItemProcess;

    /// <summary>
    ///     Событие успешной обработки пачки
    /// </summary>
    public delegate void BatchSucceedHandler();

    public event BatchSucceedHandler? BatchProcessSucceeded;

    /// <summary>
    ///     Событие провала обработки пачки
    /// </summary>
    public delegate void BatchFailedHandler(Exception e);

    public event BatchFailedHandler? BatchProcessFailed;

    /// <summary>
    ///     .ctor
    /// </summary>
    /// <param name="delay">Интервал срабатывания обработчика</param>
    /// <param name="maxSize">Максимальный размер</param>
    public TimerBag(TimeSpan delay, int maxSize)
    {
        _delay = delay;
        _maxSize = maxSize;
        _timer = new System.Timers.Timer(_delay.TotalMilliseconds);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => Process();
    }

    /// <summary>
    ///     Добавить в коллекцию
    /// </summary>
    public void Add(T item)
    {
        // Если пусто - пересоздаем таймер
        if (this.Count == 0)
        {
            _timer.Stop();
            _timer.Start();
        }
        
        // Если пачка набралась - сбрасываем
        if (this.Count >= _maxSize)
            Process();

        // Добавляем
        base.Add(new BatchedItem<T>(item));
    }

    /// <summary>
    ///     Обработка накопленных объектов
    /// </summary>
    private void Process()
    {
        if(this.Count <= 0)
            return;
        
        // Копируем элементы в отдельную область
        var itemArray = this.OrderBy(item => item.CreationTime).ToArray();
        Clear();
        
        new Thread(() =>
        {
            try
            {
                BatchProcessStarted?.Invoke(itemArray.Length);
                foreach (var item in itemArray)
                {
                    ItemProcess?.Invoke(item.Item);
                }

                BatchProcessSucceeded?.Invoke();
            }
            catch (Exception e)
            {
                BatchProcessFailed?.Invoke(e);
            }
            finally
            {
                itemArray = null;
            }

            Thread.CurrentThread.IsBackground = true;
        }).Start();
        
        _timer.Stop();
        _timer.Start();
        
        // Обновить время последнего запуска
        LastFire = DateTime.Now;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}