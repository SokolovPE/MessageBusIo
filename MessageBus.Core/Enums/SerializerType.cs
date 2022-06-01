namespace MessageBus.Core.Enums;

/// <summary>
///     Вид сериализатора
/// </summary>
public enum SerializerType
{
    /// <summary>
    ///     Json-сериализатор
    /// </summary>
    Json,
    
    /// <summary>
    ///     Сериализация через MessagePack
    /// </summary>
    MessagePack
}