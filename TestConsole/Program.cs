using MessageBus.Core;
using MessageBus.Core.Enums;
using MessageBus.Core.Extensions;
using MessageBus.Core.Interfaces;
using MessageBus.Core.Models;
using MessageBus.Core.Types;

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
            // var items = new Person[]
            // {
            //     new() { FirstName = "John", LastName = "Doe", Age = 31 },
            //     new() { FirstName = "Peter", LastName = "Jackson", Age = 25 },
            //     new() { FirstName = "Hell", LastName = "Cop", Age = 121 },
            //     new() { FirstName = "Table", LastName = "Cat", Age = 5 }
            // };
            // mb.PublishBatch(items);
            
            // Тест 5 - массовое потребление
            mb.SubscribeBatch<Person>(person => Console.WriteLine($"Two: {person.FullName}"));
            
            Console.ReadLine();
        }

        // Тест TimerBag
        // var bag = new TimerBag<Person>(TimeSpan.FromSeconds(5), 3);
        // bag.BatchProcessStarted += () => Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")} [{bag.TotalFires}] Batch process started");
        // bag.BatchProcessFailed += (e) => Console.WriteLine($"[{bag.TotalFires}] Batch process error: {e.Message}");
        // bag.BatchProcessSucceeded += () => Console.WriteLine($"[{bag.TotalFires}] Batch process succeeded");
        // bag.ItemProcessed += item => Console.WriteLine($"[{bag.TotalFires}] Processed: {item.FullName}");
        //
        // bag.Add(new() { FirstName = "John", LastName = "Doe", Age = 31 });
        // bag.Add(new() { FirstName = "Peter", LastName = "Jackson", Age = 25 });
        // bag.Add(new() { FirstName = "Hell", LastName = "Cop", Age = 121 });
        // bag.Add(new() { FirstName = "Table", LastName = "Cat", Age = 5 });
        var x = 2;
        Console.ReadLine();
    }
}