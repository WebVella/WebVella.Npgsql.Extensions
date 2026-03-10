namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Defines the configuration required for <see cref="WvDbService"/>.
/// </summary>
public interface IWvDbServiceConfiguration
{
    /// <summary>
    /// Gets or sets the connection string to the database.
    /// </summary>
	string ConnectionString { get; set; }
}

/// <summary>
/// Provides a concrete implementation of <see cref="IWvDbServiceConfiguration"/> for configuring <see cref="WvDbService"/>.
/// </summary>
public class WvDbServiceConfiguration : IWvDbServiceConfiguration
{
    /// <summary>
    /// Gets or sets the connection string to the database.
    /// </summary>
	public string ConnectionString { get; set; }
}
