namespace WebVella.Npgsql.Extensions;
internal class WvDbConnectionContext : IDisposable
{
	private static AsyncLocal<string> _currentCtxId = new AsyncLocal<string>();
	private static ConcurrentDictionary<string, WvDbConnectionContext> _contextDict =
		new ConcurrentDictionary<string, WvDbConnectionContext>();

	internal Stack<WvDbConnection> _connectionStack;
	internal NpgsqlTransaction _transaction;
	internal string _connectionString;

	private WvDbConnectionContext(string connectionString)
	{
		_connectionString = connectionString;
		_connectionStack = new Stack<WvDbConnection>();
	}

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

	internal bool CloseConnection(WvDbConnection connection)
	{
		if (connection != _connectionStack.Peek())
		{
			throw new Exception("Connection is closed or trying to" +
				" close connection, before closing inner connections.");
		}

		_connectionStack.Pop();

		return _connectionStack.Count == 0;
	}

	internal void EnterTransactionalState(NpgsqlTransaction transaction)
	{
		this._transaction = transaction;
	}

	internal void LeaveTransactionalState()
	{
		this._transaction = null;
	}

	internal static WvDbConnectionContext CreateContext(string connectionString)
	{
		_currentCtxId.Value = Guid.NewGuid().ToString();

		if (!_contextDict.TryAdd(
				_currentCtxId.Value,
				new WvDbConnectionContext(connectionString)))
		{
			throw new Exception("Cannot create new connection context");
		}

		if (!_contextDict.TryGetValue(
				_currentCtxId.Value,
				out WvDbConnectionContext ctx))
		{
			throw new Exception("Failed to get the connection context");
		}

		return ctx;
	}

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

	internal static void CloseConnectionContext()
	{
		var currentCtx = GetCurrentContext();

		if (currentCtx != null && currentCtx._transaction != null)
		{
			currentCtx._transaction.Rollback();
			throw new Exception("Trying to release connection context in " +
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

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public void Dispose(bool disposing)
	{
		if (disposing)
		{
			CloseConnectionContext();
		}
	}
}
