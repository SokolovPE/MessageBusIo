using System.Text;
using System.Text.Json;
using MessageBus.Core.Enums;
using MessageBus.Core.Interfaces;

namespace MessageBus.Core.Implementations;

/// <summary>
///     Json сериализатор
/// </summary>
public class JsonMessageBusSerializer : IMessageBusSerializer
{
    /// <summary>
    ///     Дефолт кодировка
    /// </summary>
    private Encoding _encoding = Encoding.UTF8;
    
    /// <summary>
    ///     Вид сериализатора
    /// </summary>
    public SerializerType Type => SerializerType.Json;

    /// <summary>
    ///     Сериализуем в Json объект целиком
    /// </summary>
    public byte[] Serialize<T>(T item) => _encoding.GetBytes(JsonSerializer.Serialize(item));

    /// <summary>
    ///     Сериализуем в Json объект целиком
    /// </summary>
    public async Task<byte[]> SerializeAsync<T>(T item, CancellationToken token = default)
    {
        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, item, cancellationToken: token);
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        var result = await reader.ReadToEndAsync();
        return _encoding.GetBytes(result);
    }

    /// <summary>
    ///     Сериализуем строковое значение
    /// </summary>
    /// <remarks>В сериализации не нуждается</remarks>
    public byte[] Serialize(string value) => _encoding.GetBytes(value);

    /// <summary>
    ///     Сериализуем строковое значение
    /// </summary>
    /// <remarks>В сериализации не нуждается</remarks>
    public async Task<byte[]> SerializeAsync(string value, CancellationToken token = default) =>
        _encoding.GetBytes(value);

    /// <summary>
    ///     Десериализуем Json в исходный тип
    /// </summary>
    public T? Deserialize<T>(byte[] input)
    {
        var content = _encoding.GetString(input);
        return JsonSerializer.Deserialize<T>(content);
    }

    /// <summary>
    ///     Десериализуем Json в исходный тип
    /// </summary>
    public async Task<T?> DeserializeAsync<T>(byte[] input, CancellationToken token = default)
    {
        using var ms = new MemoryStream();
        await ms.WriteAsync(input, 0, input.Length, token);
        ms.Position = 0;
        var result = await JsonSerializer.DeserializeAsync<T>(ms, cancellationToken: token);
        return result;
    }
}
