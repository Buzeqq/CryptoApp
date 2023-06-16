namespace CryptoApp.Communication.Interfaces;

public interface IManageableServer
{
    int Port { get; }
    void Start();
    void Stop();

    void Toggle()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }
    bool IsRunning { get; }
}