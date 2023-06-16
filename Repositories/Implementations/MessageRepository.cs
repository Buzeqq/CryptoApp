using System.Collections.ObjectModel;
using CryptoApp.Models;
using CryptoApp.Repositories.Interfaces;

namespace CryptoApp.Repositories.Implementations;

public class MessageRepository : IMessageRepository
{
    public ObservableCollection<Message> Messages { get; set; } = new();
    
    public void Add(Message message)
    {
        Messages.Add(message);
    }
}