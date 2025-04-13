namespace WebVella.Npgsql.Extensions;

public interface IWvDbConnection: IDisposable
{
	internal void BeginTransaction();
	internal void CommitTransaction();
	internal void RollbackTransaction();
	internal void AcquireAdvisoryLock(long key);
	internal void ReleaseAdvisoryLock();

	public NpgsqlCommand CreateCommand(string sql,
		CommandType commandType = CommandType.Text,
		params NpgsqlParameter[] parameters);
}

internal class WvDbConnection : IWvDbConnection
{
	private Stack<string> _transactionStack = new Stack<string>();
	private NpgsqlTransaction _transaction;
	private NpgsqlConnection _connection;
	private bool _initialTransactionHolder = false;
	private long? _lockKey = null;

	private WvDbConnectionContext CurrentContext;

	internal WvDbConnection(NpgsqlTransaction transaction, WvDbConnectionContext connectionContext)
	{
		CurrentContext = connectionContext;
		this._transaction = transaction;
		_connection = transaction.Connection;
	}

	internal WvDbConnection(string connectionString, WvDbConnectionContext connectionContext)
	{
		CurrentContext = connectionContext;
		_transaction = null;
		_connection = new NpgsqlConnection(connectionString);
		_connection.Open();
	}

	public NpgsqlCommand CreateCommand(string sql, CommandType commandType = CommandType.Text,
		NpgsqlParameter[] parameters = null)
	{
		NpgsqlCommand command = null;
		if (_transaction != null)
		{
			command = new NpgsqlCommand(sql, _connection, _transaction);
		}
		else
		{
			command = new NpgsqlCommand(sql, _connection);
		}

		command.CommandType = commandType;
		if (parameters != null)
			command.Parameters.AddRange(parameters);

		return command;
	}

	public void AcquireAdvisoryLock(long key)
	{
		_lockKey = key;

		NpgsqlCommand command = CreateCommand("SELECT pg_advisory_lock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", key));
		using (var reader = command.ExecuteReader())
		{
			try { reader.Read(); } finally { reader.Close(); }
		}
	}

	public void ReleaseAdvisoryLock()
	{
		if(_lockKey is null)
		{
			throw new Exception("Trying to release advisory lock, but no lock key is set.");
		}

		NpgsqlCommand command = CreateCommand("SELECT pg_advisory_unlock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", _lockKey));
		using (var reader = command.ExecuteReader())
		{
			try { reader.Read(); } finally { reader.Close(); }
		}
	}

	public void BeginTransaction()
	{
		if (_transaction == null)
		{
			_initialTransactionHolder = true;
			_transaction = _connection.BeginTransaction();
			CurrentContext.EnterTransactionalState(_transaction);
		}

		string savePointName = "tr_" + (Guid.NewGuid().ToString().Replace("-", ""));
		_transaction.Save(savePointName);
		_transactionStack.Push(savePointName);
	}

	public void CommitTransaction()
	{
		if (_transaction == null)
		{
			throw new Exception("Trying to commit non existent transaction.");
		}

		var savepointName = _transactionStack.Pop();

		if (_transactionStack.Count() == 0)
		{
			CurrentContext.LeaveTransactionalState();

			if (!_initialTransactionHolder)
			{
				_transaction.Rollback();

				_transaction = null;

				throw new Exception("Trying to commit transaction started " +
					"from another connection. The transaction is rolled back.");
			}

			_transaction.Commit();
			_transaction = null;

			if (_lockKey.HasValue)
			{
				ReleaseAdvisoryLock();
			}


		}
	}

	public void RollbackTransaction()
	{
		if (_transaction == null)
		{
			throw new Exception("Trying to rollback non existent transaction.");
		}

		_transaction.Rollback(_transactionStack.Pop());

		if (_transactionStack.Count == 0)
		{
			_transaction.Rollback();

			CurrentContext.LeaveTransactionalState();

			_transaction = null;

			if (_lockKey.HasValue)
			{
				ReleaseAdvisoryLock();
			}

			if (!_initialTransactionHolder)
			{
				throw new Exception("Trying to rollback transaction started " +
					"from another connection.The transaction is rolled back, " +
					"but this exception is thrown to notify.");
			}
		}
	}

	internal void Close()
	{
		if (_transaction != null && _initialTransactionHolder)
		{
			_transaction.Rollback();

			throw new Exception("Trying to close connection with " +
				"pending transaction. The transaction is rolled back.");
		}

		if (_transactionStack.Count > 0)
		{
			throw new Exception("Trying to close connection with " +
				"pending transaction. The transaction is rolled back.");
		}

		CurrentContext.CloseConnection(this);

		if (_transaction == null)
		{
			_connection.Close();
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
			Close();
		}
	}
}
