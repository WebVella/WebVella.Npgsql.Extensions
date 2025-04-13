namespace WebVella.Npgsql.Extensions;

public interface IWvDbTransactionScope : IDisposable
{
	public IWvDbConnection Connection { get; }
	public void Complete();
}

internal class WvDbTransactionScope : IWvDbTransactionScope
{
	private bool _isCompleted = false;
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private IWvDbConnection _connection;

	public IWvDbConnection Connection { get { return _connection; } }

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

	public void Complete()
	{
		if (_isCompleted)
		{
			throw new Exception("TransactionScope is already completed.");
		}

		_connection.CommitTransaction();

		_isCompleted = true;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

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
