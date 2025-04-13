using Microsoft.Extensions.DependencyInjection;

namespace WebVella.Npgsql.Extensions;

public static class ServiceCollectionExt
{
	public static IServiceCollection AddWvDbService(this IServiceCollection services,
		IWvDbServiceConfiguration config)
	{
		services.AddSingleton<IWvDbServiceConfiguration>(config);
		services.AddSingleton<IWvDbService, WvDbService>();
		return services;
	}

	public static IServiceCollection AddWvDbService(this IServiceCollection services,
		string connectionString)
	{
		WvDbServiceConfiguration config = new WvDbServiceConfiguration()
		{
			ConnectionString = connectionString
		};
		services.AddSingleton<IWvDbServiceConfiguration>(config);
		services.AddSingleton<IWvDbService, WvDbService>();
		return services;
	}
}
