using System.Text.Json.Serialization;
using MessageBus.Core.Interfaces;
using MessageBus.MQ.Attributes;
using MessageBus.MQ.Enums;

namespace TestConsole;

/// <summary>
///     Демо-сущность
/// </summary>
[Topic(name: nameof(Person), connectionAlias: "DemoRabbitMq", batchDelay: 15, batchSize: 3)]
public record Person : IMessage
{
    /// <summary>
    ///     Имя
    /// </summary>
    public string FirstName { get; set; }
    
    /// <summary>
    ///     Фамилия
    /// </summary>
    public string LastName { get; set; }
    
    /// <summary>
    ///     Возраст
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    ///     Авто-вычисляемое свойство, которое должно игнорироваться
    /// </summary>
    [JsonIgnore]
    public string FullName => $"{FirstName} {LastName}";
}