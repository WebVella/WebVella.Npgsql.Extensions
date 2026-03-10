namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Represents a transaction scope for managing database transactions, including support for nested transactions.
/// </summary>
public interface IWvDbTransactionScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the database connection associated with this transaction scope.
    /// </summary>
	public IWvDbConnection Connection { get; }

    /// <summary>
    /// Marks the transaction as successfully completed. After calling this method, the transaction will be committed when the scope is disposed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction scope is already completed.</exception>
	public void Complete();

    /// <summary>
    /// Asynchronously marks the transaction as successfully completed. After calling this method, the transaction will be committed when the scope is disposed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction scope is already completed.</exception>
	public Task CompleteAsync();
}

/// <summary>
/// Provides an implementation of <see cref="IWvDbTransactionScope"/> for managing database transactions and nested transaction scopes.
/// </summary>
internal class WvDbTransactionScope : IWvDbTransactionScope
{
	private bool _isCompleted = false;
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private IWvDbConnection _connection;

    /// <summary>
    /// Gets the database connection associated with this transaction scope.
    /// </summary>
	public IWvDbConnection Connection { get { return _connection; } }

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbTransactionScope"/> class for async factory usage.
    /// </summary>
	private WvDbTransactionScope() { }

    /// <summary>
    /// Asynchronously creates a new <see cref="WvDbTransactionScope"/> and begins a transaction using the specified connection context and optional advisory lock key.
    /// The caller must ensure that <see cref="WvDbConnectionContext"/> is created synchronously before calling this method, so that the <see cref="AsyncLocal{T}"/> context ID is set in the caller's execution context.
    /// </summary>
    /// <param name="connectionCtx">The connection context to use.</param>
    /// <param name="shouldDispose">Whether this scope should dispose the connection and context when disposed.</param>
    /// <param name="lockKey">An optional advisory lock key.</param>
    /// <returns>A configured <see cref="WvDbTransactionScope"/> instance.</returns>
	internal static async Task<WvDbTransactionScope> CreateAsync(WvDbConnectionContext connectionCtx, bool shouldDispose, long? lockKey = null)
	{
		var scope = new WvDbTransactionScope();
		scope._connectionCtx = connectionCtx;
		scope._shouldDispose = shouldDispose;

		if (!shouldDispose)
		{
			if (scope._connectionCtx._connectionStack.Count > 0)
			{
				scope._connection = scope._connectionCtx._connectionStack.Peek();
			}
			else
			{
				scope._connection = await scope._connectionCtx.CreateConnectionAsync();
			}
		}
		else
		{
			scope._connection = await scope._connectionCtx.CreateConnectionAsync();
		}

		await scope._connection.BeginTransactionAsync();

		if (lockKey.HasValue)
		{
			await scope._connection.AcquireAdvisoryLockAsync(lockKey.Value);
		}

		return scope;
	}

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbTransactionScope"/> class and begins a transaction using the specified connection string and optional advisory lock key.
    /// </summary>
    /// <param name="connectionString">The connection string for the database.</param>
    /// <param name="lockKey">An optional advisory lock key.</param>
	internal WvDbTransactionScope(string connectionString, long? lockKey = null)
	{
		_connectionCtx = WvDbConnectionContext.GetCurrentContext();

		if (_connectionCtx != null)
		{
			if (_connectionCtx._connectionStack.Count > 0)
			{
				_connection = _connectionCtx._connectionStack.Peek();
			}
			else
			{
				_connection = _connectionCtx.CreateConnection();
			}

			_shouldDispose = false;
		}
		else
		{
			_connectionCtx = WvDbConnectionContext.CreateContext(connectionString);
			_connection = _connectionCtx.CreateConnection();
		}

		_connection.BeginTransaction();

		if (lockKey.HasValue)
		{
			_connection.AcquireAdvisoryLock(lockKey.Value);
		}
	}

	/// <summary>
	/// Marks the transaction as successfully completed.
	/// </summary>
	/// <exception cref="Exception">Thrown if the transaction scope is already completed.</exception>
	public void Complete()
	{
		if (_isCompleted)
		{
			throw new InvalidOperationException("TransactionScope is already completed.");
		}

		_connection.CommitTransaction();

		_isCompleted = true;
	}

	/// <summary>
	/// Asynchronously marks the transaction as successfully completed.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the transaction scope is already completed.</exception>
	public async Task CompleteAsync()
	{
		if (_isCompleted)
		{
			throw new InvalidOperationException("TransactionScope is already completed.");
		}

		await _connection.CommitTransactionAsync();

		_isCompleted = true;
	}

	/// <summary>
	/// Releases the resources used by the transaction scope.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the resources used by the transaction scope.
	/// </summary>
	/// <param name="disposing">A value indicating whether to release managed resources.</param>
	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (!_isCompleted)
			{
				_connection.RollbackTransaction();
			}

			if (_shouldDispose)
			{
				_connection.Dispose();
				_connection = null;

				_connectionCtx.Dispose();
				_connectionCtx = null;
			}
		}
	}

	/// <summary>
	/// Asynchronously releases the resources used by the transaction scope.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (!_isCompleted)
		{
			await _connection.RollbackTransactionAsync();
		}

		if (_shouldDispose)
		{
			await _connection.DisposeAsync();
			_connection = null;

			await _connectionCtx.DisposeAsync();
			_connectionCtx = null;
		}

		GC.SuppressFinalize(this);
	}
}
