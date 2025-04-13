namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Configuration interface for the WvDbService.
/// </summary>
public interface IWvDbServiceConfiguration
{
	/// <summary>
	/// The connection string to the database.
	/// </summary>
	string ConnectionString { get; set; }
}

/// <summary>
/// Configuration for the WvDbService.
/// </summary>
public class WvDbServiceConfiguration : IWvDbServiceConfiguration
{
	/// <summary>
	/// The connection string to the database.
	/// </summary>
	public string ConnectionString { get; set; }
}
