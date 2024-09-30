using Tgs.Sms.Abosend;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAbosendSms(this IServiceCollection services, Action<IHttpClientBuilder> configureHttpClient)
	{
		configureHttpClient?.Invoke(services.AddHttpClient<AbosendApi>());

		return services;
	}
}
