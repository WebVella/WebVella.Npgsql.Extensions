namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Interface for database connection operations, including transaction management and advisory locks.
/// </summary>
public interface IWvDbConnection : IDisposable
{
	/// <summary>
	/// Begins a new transaction or creates a savepoint if a transaction already exists.
	/// </summary>
	internal void BeginTransaction();

	/// <summary>
	/// Commits the current transaction or releases the savepoint if nested.
	/// </summary>
	internal void CommitTransaction();

	/// <summary>
	/// Rolls back the current transaction or reverts to the previous savepoint if nested.
	/// </summary>
	internal void RollbackTransaction();

	/// <summary>
	/// Acquires a PostgreSQL advisory lock using the specified key.
	/// </summary>
	/// <param name="key">The key for the advisory lock.</param>
	internal void AcquireAdvisoryLock(long key);

	/// <summary>
	/// Releases the currently held PostgreSQL advisory lock.
	/// </summary>
	internal void ReleaseAdvisoryLock();

	/// <summary>
	/// Creates a new NpgsqlCommand with the specified SQL, command type, and parameters.
	/// </summary>
	/// <param name="sql">The SQL query or command text.</param>
	/// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
	/// <param name="parameters">Optional parameters for the command.</param>
	/// <returns>A configured NpgsqlCommand instance.</returns>
	public NpgsqlCommand CreateCommand(string sql, CommandType commandType = CommandType.Text, params NpgsqlParameter[] parameters);
}

/// <summary>
/// Implementation of IWvDbConnection for managing PostgreSQL database connections, transactions, and advisory locks.
/// </summary>
internal class WvDbConnection : IWvDbConnection
{
	private Stack<string> _transactionStack = new Stack<string>();
	private NpgsqlTransaction _transaction;
	private NpgsqlConnection _connection;
	private bool _initialTransactionHolder = false;
	private long? _lockKey = null;
	private WvDbConnectionContext CurrentContext;

	/// <summary>
	/// Initializes a new instance of WvDbConnection with an existing transaction.
	/// </summary>
	/// <param name="transaction">The existing NpgsqlTransaction.</param>
	/// <param name="connectionContext">The associated connection context.</param>
	internal WvDbConnection(NpgsqlTransaction transaction, WvDbConnectionContext connectionContext)
	{
		CurrentContext = connectionContext;
		this._transaction = transaction;
		_connection = transaction.Connection;
	}

	/// <summary>
	/// Initializes a new instance of WvDbConnection with a connection string.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <param name="connectionContext">The associated connection context.</param>
	internal WvDbConnection(string connectionString, WvDbConnectionContext connectionContext)
	{
		CurrentContext = connectionContext;
		_transaction = null;
		_connection = new NpgsqlConnection(connectionString);
		_connection.Open();
	}

	/// <inheritdoc/>
	public NpgsqlCommand CreateCommand(string sql, CommandType commandType = CommandType.Text, NpgsqlParameter[] parameters = null)
	{
		NpgsqlCommand command = _transaction != null
			? new NpgsqlCommand(sql, _connection, _transaction)
			: new NpgsqlCommand(sql, _connection);

		command.CommandType = commandType;
		if (parameters != null)
			command.Parameters.AddRange(parameters);

		return command;
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public void ReleaseAdvisoryLock()
	{
		if (_lockKey is null)
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

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public void CommitTransaction()
	{
		if (_transaction == null)
		{
			throw new Exception("Trying to commit non existent transaction.");
		}

		var savepointName = _transactionStack.Pop();

		if (_transactionStack.Count == 0)
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

	/// <inheritdoc/>
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
					"from another connection. The transaction is rolled back, " +
					"but this exception is thrown to notify.");
			}
		}
	}

	/// <summary>
	/// Closes the database connection and ensures all transactions are properly handled.
	/// </summary>
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

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the connection and releases resources.
	/// </summary>
	/// <param name="disposing">Indicates whether the method is called from Dispose or a finalizer.</param>
	public void Dispose(bool disposing)
	{
		if (disposing)
		{
			Close();
		}
	}
}
