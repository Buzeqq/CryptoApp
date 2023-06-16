using System.Collections.ObjectModel;
using CryptoApp.Models;

namespace CryptoApp.Repositories.Interfaces;

public interface IMessageRepository
{
    ObservableCollection<Message> Messages { get; }
    void Add(Message message);
}