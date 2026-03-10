namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Represents a scope that manages a PostgreSQL advisory lock for a database connection.
/// </summary>
public interface IWvDbAdvisoryLockScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the database connection associated with this advisory lock scope.
    /// </summary>
	public IWvDbConnection Connection { get; }

    /// <summary>
    /// Releases the acquired advisory lock and marks the scope as completed.
    /// </summary>
	public void Complete();

    /// <summary>
    /// Asynchronously releases the acquired advisory lock and marks the scope as completed.
    /// </summary>
	public Task CompleteAsync();
}

/// <summary>
/// Provides an implementation of <see cref="IWvDbAdvisoryLockScope"/> for managing PostgreSQL advisory locks in a database connection.
/// </summary>
internal class WvDbAdvisoryLockScope : IWvDbAdvisoryLockScope
{
	private bool _isCompleted = false;
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private WvDbConnection _connection;

    /// <summary>
    /// Gets the database connection associated with this advisory lock scope.
    /// </summary>
	public IWvDbConnection Connection { get { return _connection; } }

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbAdvisoryLockScope"/> class for async factory usage.
    /// </summary>
	private WvDbAdvisoryLockScope() { }

    /// <summary>
    /// Asynchronously creates a new <see cref="WvDbAdvisoryLockScope"/> and acquires an advisory lock using the specified connection context and lock key.
    /// The caller must ensure that <see cref="WvDbConnectionContext"/> is created synchronously before calling this method, so that the <see cref="AsyncLocal{T}"/> context ID is set in the caller's execution context.
    /// </summary>
    /// <param name="connectionCtx">The connection context to use.</param>
    /// <param name="shouldDispose">Whether this scope should dispose the connection and context when disposed.</param>
    /// <param name="lockKey">The advisory lock key to acquire.</param>
    /// <returns>A configured <see cref="WvDbAdvisoryLockScope"/> instance.</returns>
	internal static async Task<WvDbAdvisoryLockScope> CreateAsync(WvDbConnectionContext connectionCtx, bool shouldDispose, long lockKey)
	{
		var scope = new WvDbAdvisoryLockScope();
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
				scope._connection = scope._connectionCtx.CreateConnection();
			}
		}
		else
		{
			scope._connection = scope._connectionCtx.CreateConnection();
		}

		await scope._connection.AcquireAdvisoryLockAsync(lockKey);

		return scope;
	}

    /// <summary>
    /// Initializes a new instance of the <see cref="WvDbAdvisoryLockScope"/> class and acquires an advisory lock using the specified connection string and lock key.
    /// </summary>
    /// <param name="connectionString">The connection string for the database.</param>
    /// <param name="lockKey">The advisory lock key to acquire.</param>
	internal WvDbAdvisoryLockScope(string connectionString, long lockKey)
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

		_connection.AcquireAdvisoryLock(lockKey);
	}

    /// <summary>
    /// Releases the acquired advisory lock and marks the scope as completed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the advisory lock scope is already completed.</exception>
	public void Complete()
	{
		if (_isCompleted)
		{
			throw new InvalidOperationException("AdvisoryLockScope is already completed.");
		}

		_connection.ReleaseAdvisoryLock();

		_isCompleted = true;
	}

    /// <summary>
    /// Asynchronously releases the acquired advisory lock and marks the scope as completed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the advisory lock scope is already completed.</exception>
	public async Task CompleteAsync()
	{
		if (_isCompleted)
		{
			throw new InvalidOperationException("AdvisoryLockScope is already completed.");
		}

		await _connection.ReleaseAdvisoryLockAsync();

		_isCompleted = true;
	}

    /// <summary>
    /// Releases all resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
    /// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

    /// <summary>
    /// Releases the resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (!_isCompleted)
			{
				_connection.ReleaseAdvisoryLock();
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
            /// Asynchronously releases all resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
            /// </summary>
			public async ValueTask DisposeAsync()
			{
				if (!_isCompleted)
				{
					await _connection.ReleaseAdvisoryLockAsync();
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
