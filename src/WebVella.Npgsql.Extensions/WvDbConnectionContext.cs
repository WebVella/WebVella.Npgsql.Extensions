namespace WebVella.Npgsql.Extensions;
/// <summary>
/// Represents a database connection context for managing connections and transactions.
/// </summary>
internal class WvDbConnectionContext : IDisposable, IAsyncDisposable
{
	private static AsyncLocal<string> _currentCtxId = new AsyncLocal<string>();
	private static ConcurrentDictionary<string, WvDbConnectionContext> _contextDict =
		new ConcurrentDictionary<string, WvDbConnectionContext>();

	private bool _disposed = false;
	internal Stack<WvDbConnection> _connectionStack;
	internal NpgsqlTransaction _transaction;
	internal string _connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="WvDbConnectionContext"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	private WvDbConnectionContext(string connectionString)
	{
		_connectionString = connectionString;
		_connectionStack = new Stack<WvDbConnection>();
	}

	/// <summary>
	/// Creates a new database connection within the current context.
	/// </summary>
	/// <returns>A new instance of <see cref="WvDbConnection"/>.</returns>
	internal WvDbConnection CreateConnection()
	{
		WvDbConnection con = null;

		if (_transaction != null)
			con = new WvDbConnection(_transaction, this);
		else
			con = new WvDbConnection(_connectionString, this);

		_connectionStack.Push(con);

		return con;
	}

	/// <summary>
	/// Asynchronously creates a new database connection within the current context.
	/// </summary>
	/// <returns>A new instance of <see cref="WvDbConnection"/>.</returns>
	internal async Task<WvDbConnection> CreateConnectionAsync()
	{
		WvDbConnection con = null;

		if (_transaction != null)
			con = new WvDbConnection(_transaction, this);
		else
			con = await WvDbConnection.CreateAsync(_connectionString, this);

		_connectionStack.Push(con);

		return con;
	}

	/// <summary>
	/// Closes the specified database connection.
	/// </summary>
	/// <param name="connection">The connection to close.</param>
	/// <returns>True if all connections are closed; otherwise, false.</returns>
	/// <exception cref="Exception">Thrown if the connection is not the most recently opened connection.</exception>
	internal bool CloseConnection(WvDbConnection connection)
	{
		if (connection != _connectionStack.Peek())
		{
			throw new InvalidOperationException("Connection is closed or trying to" +
				" close connection, before closing inner connections.");
		}

		_connectionStack.Pop();

		return _connectionStack.Count == 0;
	}

	/// <summary>
	/// Enters a transactional state using the specified transaction.
	/// </summary>
	/// <param name="transaction">The transaction to use.</param>
	internal void EnterTransactionalState(NpgsqlTransaction transaction)
	{
		this._transaction = transaction;
	}

	/// <summary>
	/// Leaves the transactional state, clearing the current transaction.
	/// </summary>
	internal void LeaveTransactionalState()
	{
		this._transaction = null;
	}

	/// <summary>
	/// Creates a new connection context with the specified connection string.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <returns>A new instance of <see cref="WvDbConnectionContext"/>.</returns>
	/// <exception cref="Exception">Thrown if the context cannot be created or retrieved.</exception>
	internal static WvDbConnectionContext CreateContext(string connectionString)
	{
		_currentCtxId.Value = Guid.NewGuid().ToString();

		if (!_contextDict.TryAdd(
				_currentCtxId.Value,
				new WvDbConnectionContext(connectionString)))
		{
			throw new InvalidOperationException("Cannot create new connection context");
		}

		if (!_contextDict.TryGetValue(
				_currentCtxId.Value,
				out WvDbConnectionContext ctx))
		{
			throw new InvalidOperationException("Failed to get the connection context");
		}

		return ctx;
	}

	/// <summary>
	/// Gets the current connection context.
	/// </summary>
	/// <returns>The current <see cref="WvDbConnectionContext"/>, or null if no context exists.</returns>
	internal static WvDbConnectionContext GetCurrentContext()
	{
		if (_currentCtxId is null ||
			String.IsNullOrWhiteSpace(_currentCtxId.Value))
		{
			return null;
		}

		_contextDict.TryGetValue(
			_currentCtxId.Value,
			out WvDbConnectionContext ctx);

		return ctx;
	}

	/// <summary>
	/// Closes the current connection context, rolling back any active transactions.
	/// </summary>
	/// <exception cref="Exception">Thrown if there is an open transaction when attempting to close the context.</exception>
	internal static void CloseConnectionContext()
	{
		var currentCtx = GetCurrentContext();

		if (currentCtx != null && currentCtx._transaction != null)
		{
			currentCtx._transaction.Rollback();
			throw new InvalidOperationException("Trying to release connection context in " +
				"transactional state. There is open transaction in created connections.");
		}

		string idValue = null;

		if (_currentCtxId != null &&
			!string.IsNullOrWhiteSpace(_currentCtxId.Value))
		{
			idValue = _currentCtxId.Value;
		}

		if (!string.IsNullOrWhiteSpace(idValue))
		{
			_contextDict.TryRemove(idValue, out WvDbConnectionContext ctx);

			if (ctx != null)
			{
				ctx.Dispose();
			}

			_currentCtxId.Value = null;
		}
	}

	/// <summary>
	/// Asynchronously closes the current connection context, rolling back any active transactions.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if there is an open transaction when attempting to close the context.</exception>
	internal static async Task CloseConnectionContextAsync()
	{
		var currentCtx = GetCurrentContext();

		if (currentCtx != null && currentCtx._transaction != null)
		{
			await currentCtx._transaction.RollbackAsync();
			throw new InvalidOperationException("Trying to release connection context in " +
				"transactional state. There is open transaction in created connections.");
		}

		string idValue = null;

		if (_currentCtxId != null &&
			!string.IsNullOrWhiteSpace(_currentCtxId.Value))
		{
			idValue = _currentCtxId.Value;
		}

		if (!string.IsNullOrWhiteSpace(idValue))
		{
			_contextDict.TryRemove(idValue, out WvDbConnectionContext ctx);

			if (ctx != null)
			{
				await ctx.DisposeAsync();
			}

			_currentCtxId.Value = null;
		}
	}

	/// <summary>
	/// Releases all resources used by the <see cref="WvDbConnectionContext"/>.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged and optionally managed resources used by the <see cref="WvDbConnectionContext"/>.
	/// </summary>
	/// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	private void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		_disposed = true;

		if (disposing)
		{
			CloseConnectionContext();
		}
	}

	/// <summary>
	/// Asynchronously releases all resources used by the <see cref="WvDbConnectionContext"/>.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;

		await CloseConnectionContextAsync();
		GC.SuppressFinalize(this);
	}
}
