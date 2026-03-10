namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Defines methods for creating and managing database connections, transaction scopes, and advisory lock scopes.
/// </summary>
public interface IWvDbService
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <returns>A new <see cref="IWvDbConnection"/> instance.</returns>
	IWvDbConnection CreateConnection();

    /// <summary>
    /// Creates a new transaction scope for database operations.
    /// </summary>
    /// <param name="lockKey">An optional advisory lock key.</param>
    /// <returns>A new <see cref="IWvDbTransactionScope"/> instance.</returns>
	IWvDbTransactionScope CreateTransactionScope(long? lockKey = null);

    /// <summary>
    /// Creates a new advisory lock scope for database operations.
    /// </summary>
    /// <param name="lockKey">The advisory lock key.</param>
    /// <returns>A new <see cref="IWvDbAdvisoryLockScope"/> instance.</returns>
	IWvDbAdvisoryLockScope CreateAdvisoryLockScope(long lockKey);

    /// <summary>
    /// Asynchronously creates a new database connection.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a new <see cref="IWvDbConnection"/> instance.</returns>
	Task<IWvDbConnection> CreateConnectionAsync();

    /// <summary>
    /// Asynchronously creates a new transaction scope for database operations.
    /// </summary>
    /// <param name="lockKey">An optional advisory lock key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a new <see cref="IWvDbTransactionScope"/> instance.</returns>
	Task<IWvDbTransactionScope> CreateTransactionScopeAsync(long? lockKey = null);

    /// <summary>
    /// Asynchronously creates a new advisory lock scope for database operations.
    /// </summary>
    /// <param name="lockKey">The advisory lock key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a new <see cref="IWvDbAdvisoryLockScope"/> instance.</returns>
	Task<IWvDbAdvisoryLockScope> CreateAdvisoryLockScopeAsync(long lockKey);
}

/// <summary>
/// Provides an implementation of <see cref="IWvDbService"/> for managing PostgreSQL database connections, transactions, and advisory locks.
/// </summary>
public class WvDbService : IWvDbService
{
    /// <summary>
    /// Gets the connection string used by this service.
    /// </summary>
	public string ConnectionString { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbService"/> class using the specified configuration object.
    /// </summary>
    /// <param name="config">The configuration object containing the connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <c>null</c>.</exception>
	public WvDbService(IWvDbServiceConfiguration config)
	{
		if (config == null)
			throw new ArgumentNullException("config");

		ConnectionString = config.ConnectionString;
	}

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbService"/> class using the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string for the database.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionString"/> is <c>null</c> or empty.</exception>
	public WvDbService(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentNullException(nameof(connectionString));

		ConnectionString = connectionString;
	}

    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <returns>A new <see cref="IWvDbConnection"/> instance.</returns>
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

	/// <summary>
	/// Asynchronously creates a new database connection.
	/// </summary>
	/// <returns>An instance of <see cref="IWvDbConnection"/>.</returns>
	public Task<IWvDbConnection> CreateConnectionAsync()
	{
		var currentCtx = WvDbConnectionContext.GetCurrentContext();

		if (currentCtx is null)
		{
			currentCtx = WvDbConnectionContext.CreateContext(ConnectionString);
		}

		return CreateConnectionCoreAsync(currentCtx);
	}

	private static async Task<IWvDbConnection> CreateConnectionCoreAsync(WvDbConnectionContext connectionCtx)
	{
		return await connectionCtx.CreateConnectionAsync();
	}

	/// <summary>
	/// Asynchronously creates a new transaction scope.
	/// </summary>
	/// <param name="lockKey">Optional lock key for advisory locking.</param>
	/// <returns>An instance of <see cref="IWvDbTransactionScope"/>.</returns>
	public Task<IWvDbTransactionScope> CreateTransactionScopeAsync(long? lockKey = null)
	{
		// Resolve or create the connection context synchronously (not in an async method)
		// so that the AsyncLocal context ID is set in the caller's execution context
		// and is visible to subsequent calls within the scope.
		var currentCtx = WvDbConnectionContext.GetCurrentContext();
		bool shouldDispose;

		if (currentCtx != null)
		{
			shouldDispose = false;
		}
		else
		{
			currentCtx = WvDbConnectionContext.CreateContext(ConnectionString);
			shouldDispose = true;
		}

		return CreateTransactionScopeCoreAsync(currentCtx, shouldDispose, lockKey);
	}

	private static async Task<IWvDbTransactionScope> CreateTransactionScopeCoreAsync(
		WvDbConnectionContext connectionCtx, bool shouldDispose, long? lockKey)
	{
		return await WvDbTransactionScope.CreateAsync(connectionCtx, shouldDispose, lockKey);
	}

	/// <summary>
	/// Asynchronously creates a new advisory lock scope.
	/// </summary>
	/// <param name="lockKey">The lock key for the advisory lock.</param>
	/// <returns>An instance of <see cref="IWvDbAdvisoryLockScope"/>.</returns>
	public Task<IWvDbAdvisoryLockScope> CreateAdvisoryLockScopeAsync(long lockKey)
	{
		var currentCtx = WvDbConnectionContext.GetCurrentContext();
		bool shouldDispose;

		if (currentCtx != null)
		{
			shouldDispose = false;
		}
		else
		{
			currentCtx = WvDbConnectionContext.CreateContext(ConnectionString);
			shouldDispose = true;
		}

		return CreateAdvisoryLockScopeCoreAsync(currentCtx, shouldDispose, lockKey);
	}

	private static async Task<IWvDbAdvisoryLockScope> CreateAdvisoryLockScopeCoreAsync(
		WvDbConnectionContext connectionCtx, bool shouldDispose, long lockKey)
	{
		return await WvDbAdvisoryLockScope.CreateAsync(connectionCtx, shouldDispose, lockKey);
	}
}
