using Microsoft.Extensions.DependencyInjection;

namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Extension methods for registering WvDbService and its configuration in the dependency injection container.
/// </summary>
public static class ServiceCollectionExt
{
	/// <summary>
	/// Adds the WvDbService and its configuration to the service collection.
	/// </summary>
	/// <param name="services">The service collection to which the services will be added.</param>
	/// <param name="config">The configuration for the WvDbService.</param>
	/// <returns>The updated service collection.</returns>
	public static IServiceCollection AddWvDbService(this IServiceCollection services,
		IWvDbServiceConfiguration config)
	{
		services.AddSingleton<IWvDbServiceConfiguration>(config);
		services.AddSingleton<IWvDbService, WvDbService>();
		return services;
	}

	/// <summary>
	/// Adds the WvDbService and its configuration to the service collection using a connection string.
	/// </summary>
	/// <param name="services">The service collection to which the services will be added.</param>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <returns>The updated service collection.</returns>
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
