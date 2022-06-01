namespace MessageBus.Core.Exceptions;

/// <summary>
///     Сообщение об ошибке в MessageBus
/// </summary>
public class MessageBusException : Exception
{
    /// <summary>
    ///     .ctor
    /// </summary>
    public MessageBusException()
    {
        
    }
    
    /// <summary>
    ///     .ctor
    /// </summary>
    public MessageBusException(string? message) : base(message)
    {
        
    }
    
    /// <summary>
    ///     .ctor
    /// </summary>
    public MessageBusException(string? message, Exception? innerException) : base(message, innerException)
    {
        
    }
}