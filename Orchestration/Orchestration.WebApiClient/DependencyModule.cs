using Microsoft.Extensions.DependencyInjection;
using Orchestration.WebApiClient.Core;

namespace Orchestration.WebApiClient;

public static class DependencyModule
{
    public static IHttpClientBuilder AddInstrumentsApiClient(this IServiceCollection services, string baseAddress)
    {
        return services.AddHttpClient<IInstrumentsApiClient, InstrumentsApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        });
    }
}
