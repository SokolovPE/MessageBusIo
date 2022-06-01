using MessageBus.Core;
using MessageBus.Core.Enums;
using MessageBus.Core.Extensions;
using MessageBus.Core.Interfaces;

namespace TestConsole;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var mbConfig = new MessageBusConfig
        {
            Connections = new[]
            {
                new ConnectionConfig
                {
                    Alias = "DemoRabbitMq",
                    ConnectionString = "amqp://guest:guest@localhost:5672/develop",
                    TransportType = TransportType.RabbitMq
                }
            }
        };
        // var config = builder.Configuration.GetSection(nameof(MessageBusConfig)).Get<MessageBusConfig>();
        builder.Services.AddMessageBusCore(mbConfig);

        var app = builder.Build();
        
        // Тест 1 - подключение к кролику и отправка [Done]
        using (var mb = app.Services.GetRequiredService<IMessageBus>())
        {
            // var item = new Person {FirstName = "John", LastName = "Doe", Age = 31};
            // mb.Publish(item);

            // // Тест 2 - подписка
            // mb.Subscribe<Person>(person => Console.WriteLine($"One: {person.FullName}"), routingKey: "key.first");
            //
            // // Тест 3 - подписка второго подписчика
            // using var mbTwo = app.Services.GetRequiredService<IMessageBus>();
            // mbTwo.Subscribe<Person>(person => Console.WriteLine($"Two: {person.FullName}"), consumerName: "Slave", routingKey: "key.second");
            
            // Тест 4 - массовая отправка
            var items = new Person[]
            {
                new() { FirstName = "John", LastName = "Doe", Age = 31 },
                new() { FirstName = "Peter", LastName = "Jackson", Age = 25 },
                new() { FirstName = "Hell", LastName = "Cop", Age = 121 },
                new() { FirstName = "Table", LastName = "Cat", Age = 5 }
            };
            mb.BatchPublish(items);
            
            Console.ReadLine();
        }

        var x = 2;
        Console.ReadLine();
    }
}