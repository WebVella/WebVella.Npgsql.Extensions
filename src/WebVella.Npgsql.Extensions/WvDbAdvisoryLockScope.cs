namespace WebVella.Npgsql.Extensions;

public interface IWvDbAdvisoryLockScope : IDisposable
{
	public IWvDbConnection Connection { get; }
	public void Complete();
}

internal class WvDbAdvisoryLockScope : IWvDbAdvisoryLockScope
{
	private bool _shouldDispose = true;
	private WvDbConnectionContext _connectionCtx;
	private WvDbConnection _connection;

	public IWvDbConnection Connection { get { return _connection; } }

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

	public void Complete()
	{
		_connection.ReleaseAdvisoryLock();
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
