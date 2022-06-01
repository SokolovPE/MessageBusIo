using MessageBus.Core.Enums;

namespace MessageBus.Core.Interfaces;

/// <summary>
///     Сериализатор, поддерживаемый MessageBus
/// </summary>
public interface IMessageBusSerializer
{
    /// <summary>
    ///     Вид сериализатора
    /// </summary>
    public SerializerType Type { get; }
    
    /// <summary>
    ///     Сериализовать сущность целиком
    /// </summary>
    public byte[] Serialize<T>(T value);
    
    /// <summary>
    ///     Сериализовать сущность целиком
    /// </summary>
    public Task<byte[]> SerializeAsync<T>(T value, CancellationToken token = default);
    
    /// <summary>
    ///     Сериализовать строку
    /// </summary>
    public byte[] Serialize(string value);
    
    /// <summary>
    ///     Сериализовать строку
    /// </summary>
    public Task<byte[]> SerializeAsync(string value, CancellationToken token = default);

    /// <summary>
    ///     Десериализовать сущность целиком
    /// </summary>
    public T? Deserialize<T>(byte[] input);

    /// <summary>
    ///     Десериализовать сущность целиком
    /// </summary>
    public Task<T?> DeserializeAsync<T>(byte[] input, CancellationToken token = default);
}