namespace CryptoApp.Communication.Interfaces;

public interface IManagableServer
{
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