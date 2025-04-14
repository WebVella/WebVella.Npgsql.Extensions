namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Represents a transaction scope for database operations.
/// </summary>
public interface IWvDbTransactionScope : IDisposable
{
	/// <summary>
	/// Gets the database connection associated with the transaction scope.
	/// </summary>
	public IWvDbConnection Connection { get; }

	/// <summary>
	/// Marks the transaction as successfully completed.
	/// </summary>
	/// <exception cref="Exception">Thrown if the transaction scope is already completed.</exception>
	public void Complete();
}

/// <summary>
/// Provides an implementation of <see cref="IWvDbTransactionScope"/> for managing database transactions.
/// </summary>
internal class WvDbTransactionScope : IWvDbTransactionScope
{
	private bool _isCompleted = false;
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private IWvDbConnection _connection;

	/// <summary>
	/// Gets the database connection associated with the transaction scope.
	/// </summary>
	public IWvDbConnection Connection { get { return _connection; } }

	/// <summary>
	/// Initializes a new instance of the <see cref="WvDbTransactionScope"/> class.
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
			throw new Exception("TransactionScope is already completed.");
		}

		_connection.CommitTransaction();

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
	public void Dispose(bool disposing)
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
}
