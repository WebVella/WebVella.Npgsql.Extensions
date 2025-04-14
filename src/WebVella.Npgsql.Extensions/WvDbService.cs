namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Interface for database service operations.
/// </summary>
public interface IWvDbService
{
	/// <summary>
	/// Creates a new database connection.
	/// </summary>
	/// <returns>An instance of <see cref="IWvDbConnection"/>.</returns>
	IWvDbConnection CreateConnection();

	/// <summary>
	/// Creates a new transaction scope.
	/// </summary>
	/// <param name="lockKey">Optional lock key for advisory locking.</param>
	/// <returns>An instance of <see cref="IWvDbTransactionScope"/>.</returns>
	IWvDbTransactionScope CreateTransactionScope(long? lockKey = null);

	/// <summary>
	/// Creates a new advisory lock scope.
	/// </summary>
	/// <param name="lockKey">The lock key for the advisory lock.</param>
	/// <returns>An instance of <see cref="IWvDbAdvisoryLockScope"/>.</returns>
	IWvDbAdvisoryLockScope CreateAdvisoryLockScope(long lockKey);
}

/// <summary>
/// Implementation of the database service.
/// </summary>
public class WvDbService : IWvDbService
{
	/// <summary>
	/// Gets the connection string for the database.
	/// </summary>
	public string ConnectionString { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="WvDbService"/> class using a configuration object.
	/// </summary>
	/// <param name="config">The configuration object containing the connection string.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
	public WvDbService(IWvDbServiceConfiguration config)
	{
		if (config == null)
			throw new ArgumentNullException("config");

		ConnectionString = config.ConnectionString;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="WvDbService"/> class using a connection string.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
	public WvDbService(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentNullException(connectionString);

		ConnectionString = connectionString;
	}

	/// <summary>
	/// Creates a new database connection.
	/// </summary>
	/// <returns>An instance of <see cref="IWvDbConnection"/>.</returns>
	public IWvDbConnection CreateConnection()
	{
		var currentCtx = WvDbConnectionContext.GetCurrentContext();

		if (currentCtx is null)
		{
			currentCtx = WvDbConnectionContext.CreateContext(ConnectionString);
		}

		return currentCtx.CreateConnection();
	}

	/// <summary>
	/// Creates a new transaction scope.
	/// </summary>
	/// <param name="lockKey">Optional lock key for advisory locking.</param>
	/// <returns>An instance of <see cref="IWvDbTransactionScope"/>.</returns>
	public IWvDbTransactionScope CreateTransactionScope(long? lockKey = null)
	{
		return new WvDbTransactionScope(ConnectionString, lockKey);
	}

	/// <summary>
	/// Creates a new advisory lock scope.
	/// </summary>
	/// <param name="lockKey">The lock key for the advisory lock.</param>
	/// <returns>An instance of <see cref="IWvDbAdvisoryLockScope"/>.</returns>
	public IWvDbAdvisoryLockScope CreateAdvisoryLockScope(long lockKey)
	{
		return new WvDbAdvisoryLockScope(ConnectionString, lockKey);
	}
}
