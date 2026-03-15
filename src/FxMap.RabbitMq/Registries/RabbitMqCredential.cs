using RabbitMQ.Client;

namespace FxMap.RabbitMq.Registries;

public sealed class RabbitMqCredential
{
    internal string UserNameValue { get; private set; }
    internal string PasswordValue { get; private set; }
    internal SslOption SslOptionValue { get; private set; }

    public void UserName(string userName) => UserNameValue = userName;
    public void Password(string password) => PasswordValue = password;
    public void Ssl(Action<SslOption> sslOption)
    {
        var option = new SslOption();
        sslOption.Invoke(option);
        SslOptionValue = option;
    }
}
