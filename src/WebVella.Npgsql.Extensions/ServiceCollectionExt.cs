using Microsoft.Extensions.DependencyInjection;

namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Provides extension methods for registering <see cref="WvDbService"/> and its configuration in the dependency injection container.
/// </summary>
public static class ServiceCollectionExt
{
    /// <summary>
    /// Adds <see cref="IWvDbService"/> and its configuration to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="config">The configuration for <see cref="WvDbService"/>.</param>
    /// <returns>The updated service collection.</returns>
	public static IServiceCollection AddWvDbService(this IServiceCollection services,
		IWvDbServiceConfiguration config)
	{
		services.AddSingleton<IWvDbServiceConfiguration>(config);
		services.AddSingleton<IWvDbService, WvDbService>();
		return services;
	}

    /// <summary>
    /// Adds <see cref="IWvDbService"/> and its configuration to the specified service collection using a connection string.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
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
