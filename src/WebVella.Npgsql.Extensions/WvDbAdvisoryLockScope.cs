namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Represents a scope for managing advisory locks in a database connection.
/// </summary>
public interface IWvDbAdvisoryLockScope : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Gets the database connection associated with the advisory lock scope.
	/// </summary>
	public IWvDbConnection Connection { get; }

	/// <summary>
	/// Completes the advisory lock scope by releasing the acquired lock.
	/// </summary>
	public void Complete();

	/// <summary>
	/// Asynchronously completes the advisory lock scope by releasing the acquired lock.
	/// </summary>
	public Task CompleteAsync();
}

/// <summary>
/// Implements a scope for managing advisory locks in a database connection.
/// </summary>
internal class WvDbAdvisoryLockScope : IWvDbAdvisoryLockScope
{
	private bool _isCompleted = false;
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private WvDbConnection _connection;

	/// <summary>
	/// Gets the database connection associated with the advisory lock scope.
	/// </summary>
	public IWvDbConnection Connection { get { return _connection; } }

	/// <summary>
	/// Private constructor for async factory usage.
	/// </summary>
	private WvDbAdvisoryLockScope() { }

	/// <summary>
	/// Asynchronously creates a new instance of the <see cref="WvDbAdvisoryLockScope"/> class.
	/// Acquires an advisory lock using the specified connection context and lock key.
	/// The caller must ensure that <see cref="WvDbConnectionContext"/> is created synchronously
	/// before calling this method, so that the <see cref="AsyncLocal{T}"/> context ID is set
	/// in the caller's execution context.
	/// </summary>
	/// <param name="connectionCtx">The pre-resolved or pre-created connection context.</param>
	/// <param name="shouldDispose">Whether this scope should dispose the connection and context.</param>
	/// <param name="lockKey">The key used to acquire the advisory lock.</param>
	/// <returns>A configured WvDbAdvisoryLockScope instance.</returns>
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
	/// Initializes a new instance of the <see cref="WvDbAdvisoryLockScope"/> class.
	/// Acquires an advisory lock using the specified connection string and lock key.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <param name="lockKey">The key used to acquire the advisory lock.</param>
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
	/// Completes the advisory lock scope by releasing the acquired lock.
	/// </summary>
	/// <exception cref="Exception">Thrown if the advisory lock scope is already completed.</exception>
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
	/// Asynchronously completes the advisory lock scope by releasing the acquired lock.
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
	/// Disposes the resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
	/// </summary>
	/// <param name="disposing">A value indicating whether to dispose managed resources.</param>
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
			/// Asynchronously disposes the resources used by the <see cref="WvDbAdvisoryLockScope"/> instance.
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
