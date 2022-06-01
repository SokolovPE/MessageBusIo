namespace MessageBus.MQ.Attributes;

/// <summary>
///     Описание batch операции
/// </summary>
[AttributeUsage(AttributeTargets.Class| AttributeTargets.Struct)]
public class BatchAttribute : Attribute
{
    /// <summary>
    ///     .ctor
    /// </summary>
    /// <param name="allowed">Разрешены ли batch-операции</param>
    /// <param name="batchDelay">Задержка между обработкой пачек</param>
    /// <param name="batchSize">Максимально допустимый размер пачки</param>
    public BatchAttribute(bool allowed = false, int batchDelay = 0, ushort batchSize = 0)
    {
        Allowed = allowed;
        BatchDelay = batchDelay <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(batchDelay);
        BatchSize = batchSize <= 0 ? (ushort)1 : batchSize;
    }

    /// <summary>
    ///     Разрешены ли batch операции с сущностью
    /// </summary>
    public bool Allowed { get; }
    
    /// <summary>
    ///     Задержка на время сборка пачки
    /// </summary>
    public TimeSpan BatchDelay { get; }
    
    /// <summary>
    ///     Максимальный размер пачки
    /// </summary>
    public ushort BatchSize { get; }
}