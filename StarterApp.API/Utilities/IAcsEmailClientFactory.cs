using Azure.Communication.Email;
using Azure.Core;

namespace StarterApp.API.Utilities;

public interface IAcsEmailClientFactory
{
    EmailClient CreateClient(TokenCredential? tokenCredential = null);
}