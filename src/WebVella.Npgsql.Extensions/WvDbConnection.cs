namespace WebVella.Npgsql.Extensions;

/// <summary>
/// Defines methods for managing PostgreSQL database connections, transactions, savepoints, and advisory locks.
/// </summary>
public interface IWvDbConnection : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Begins a new transaction, or creates a savepoint if a transaction already exists.
    /// </summary>
	internal void BeginTransaction();

    /// <summary>
    /// Asynchronously begins a new transaction, or creates a savepoint if a transaction already exists.
    /// </summary>
	internal Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction, or releases the savepoint if this is a nested transaction.
    /// </summary>
	internal void CommitTransaction();

    /// <summary>
    /// Asynchronously commits the current transaction, or releases the savepoint if this is a nested transaction.
    /// </summary>
	internal Task CommitTransactionAsync();

    /// <summary>
    /// Rolls back the current transaction, or reverts to the previous savepoint if this is a nested transaction.
    /// </summary>
	internal void RollbackTransaction();

    /// <summary>
    /// Asynchronously rolls back the current transaction, or reverts to the previous savepoint if this is a nested transaction.
    /// </summary>
	internal Task RollbackTransactionAsync();

    /// <summary>
    /// Acquires a PostgreSQL advisory lock using the specified key.
    /// </summary>
    /// <param name="key">The advisory lock key.</param>
	internal void AcquireAdvisoryLock(long key);

    /// <summary>
    /// Asynchronously acquires a PostgreSQL advisory lock using the specified key.
    /// </summary>
    /// <param name="key">The advisory lock key.</param>
	internal Task AcquireAdvisoryLockAsync(long key);

	/// <summary>
	/// Releases the currently held PostgreSQL advisory lock.
	/// </summary>
	internal void ReleaseAdvisoryLock();

	/// <summary>
	/// Asynchronously releases the currently held PostgreSQL advisory lock.
	/// </summary>
	internal Task ReleaseAdvisoryLockAsync();

    /// <summary>
    /// Creates a new <see cref="NpgsqlCommand"/> with the specified SQL, command type, and parameters.
    /// </summary>
    /// <param name="sql">The SQL query or command text.</param>
    /// <param name="commandType">The type of the command (e.g., <see cref="CommandType.Text"/>, <see cref="CommandType.StoredProcedure"/>).</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <returns>A configured <see cref="NpgsqlCommand"/> instance.</returns>
	public NpgsqlCommand CreateCommand(string sql, CommandType commandType = CommandType.Text, params NpgsqlParameter[] parameters);
}

/// <summary>
/// Provides an implementation of <see cref="IWvDbConnection"/> for managing PostgreSQL database connections, transactions, savepoints, and advisory locks.
/// </summary>
internal class WvDbConnection : IWvDbConnection
{
	private Stack<string> _transactionStack = new Stack<string>();
	private NpgsqlTransaction _transaction;
	private NpgsqlConnection _connection;
	private bool _initialTransactionHolder = false;
	private bool _disposed = false;
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

	/// <summary>
	/// Private constructor for async factory usage.
	/// </summary>
	/// <param name="connectionContext">The associated connection context.</param>
	private WvDbConnection(WvDbConnectionContext connectionContext)
	{
		CurrentContext = connectionContext;
	}

	/// <summary>
	/// Asynchronously creates a new instance of WvDbConnection with a connection string.
	/// </summary>
	/// <param name="connectionString">The connection string for the database.</param>
	/// <param name="connectionContext">The associated connection context.</param>
	/// <returns>A configured WvDbConnection instance.</returns>
	internal static async Task<WvDbConnection> CreateAsync(string connectionString, WvDbConnectionContext connectionContext)
	{
		var conn = new WvDbConnection(connectionContext);
		conn._connection = new NpgsqlConnection(connectionString);
		await conn._connection.OpenAsync();
		return conn;
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

		using NpgsqlCommand command = CreateCommand("SELECT pg_advisory_lock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", key));
		using (var reader = command.ExecuteReader())
		{
			try { reader.Read(); } finally { reader.Close(); }
		}
	}

	/// <inheritdoc/>
	public async Task AcquireAdvisoryLockAsync(long key)
	{
		_lockKey = key;

		using NpgsqlCommand command = CreateCommand("SELECT pg_advisory_lock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", key));
		await using (var reader = await command.ExecuteReaderAsync())
		{
			await reader.ReadAsync();
		}
	}

	/// <inheritdoc/>
	public void ReleaseAdvisoryLock()
	{
		if (_lockKey is null)
		{
			throw new InvalidOperationException("Trying to release advisory lock, but no lock key is set.");
		}

		using NpgsqlCommand command = CreateCommand("SELECT pg_advisory_unlock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", _lockKey));
		using (var reader = command.ExecuteReader())
		{
			try { reader.Read(); } finally { reader.Close(); }
		}

		_lockKey = null;
	}

	/// <inheritdoc/>
	public async Task ReleaseAdvisoryLockAsync()
	{
		if (_lockKey is null)
		{
			throw new InvalidOperationException("Trying to release advisory lock, but no lock key is set.");
		}

		using NpgsqlCommand command = CreateCommand("SELECT pg_advisory_unlock(@key);");
		command.Parameters.Add(new NpgsqlParameter("@key", _lockKey));
		await using (var reader = await command.ExecuteReaderAsync())
		{
			await reader.ReadAsync();
		}

		_lockKey = null;
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
	public async Task BeginTransactionAsync()
	{
		if (_transaction == null)
		{
			_initialTransactionHolder = true;
			_transaction = await _connection.BeginTransactionAsync();
			CurrentContext.EnterTransactionalState(_transaction);
		}

		string savePointName = "tr_" + (Guid.NewGuid().ToString().Replace("-", ""));
		using (var cmd = new NpgsqlCommand($"SAVEPOINT \"{savePointName}\"", _connection, _transaction))
			await cmd.ExecuteNonQueryAsync();
		_transactionStack.Push(savePointName);
	}

	/// <inheritdoc/>
	public void CommitTransaction()
	{
		if (_transaction == null)
		{
			throw new InvalidOperationException("Trying to commit non existent transaction.");
		}

		var savepointName = _transactionStack.Pop();

		if (_transactionStack.Count == 0)
		{
			CurrentContext.LeaveTransactionalState();

			if (!_initialTransactionHolder)
			{
				_transaction.Rollback();
				_transaction = null;

				throw new InvalidOperationException("Trying to commit transaction started " +
					"from another connection. The transaction is rolled back.");
			}

			_transaction.Commit();
			_transaction = null;

			if (_lockKey.HasValue)
			{
				ReleaseAdvisoryLock();
			}
		}
		else
		{
			_transaction.Release(savepointName);
		}
	}

	/// <inheritdoc/>
	public async Task CommitTransactionAsync()
	{
		if (_transaction == null)
		{
			throw new InvalidOperationException("Trying to commit non existent transaction.");
		}

		var savepointName = _transactionStack.Pop();

		if (_transactionStack.Count == 0)
		{
			CurrentContext.LeaveTransactionalState();

			if (!_initialTransactionHolder)
			{
				await _transaction.RollbackAsync();
				_transaction = null;

				throw new InvalidOperationException("Trying to commit transaction started " +
					"from another connection. The transaction is rolled back.");
			}

			await _transaction.CommitAsync();
			_transaction = null;

			if (_lockKey.HasValue)
			{
				await ReleaseAdvisoryLockAsync();
			}
		}
		else
		{
			using (var cmd = new NpgsqlCommand($"RELEASE SAVEPOINT \"{savepointName}\"", _connection, _transaction))
				await cmd.ExecuteNonQueryAsync();
		}
	}

	/// <inheritdoc/>
	public void RollbackTransaction()
	{
		if (_transaction == null)
		{
			throw new InvalidOperationException("Trying to rollback non existent transaction.");
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
				throw new InvalidOperationException("Trying to rollback transaction started " +
					"from another connection. The transaction is rolled back, " +
					"but this exception is thrown to notify.");
			}
		}
	}

	/// <inheritdoc/>
	public async Task RollbackTransactionAsync()
	{
		if (_transaction == null)
		{
			throw new InvalidOperationException("Trying to rollback non existent transaction.");
		}

		var savepointName = _transactionStack.Pop();

		using (var cmd = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT \"{savepointName}\"", _connection, _transaction))
			await cmd.ExecuteNonQueryAsync();

		if (_transactionStack.Count == 0)
		{
			await _transaction.RollbackAsync();
			CurrentContext.LeaveTransactionalState();
			_transaction = null;

			if (_lockKey.HasValue)
			{
				await ReleaseAdvisoryLockAsync();
			}

			if (!_initialTransactionHolder)
			{
				throw new InvalidOperationException("Trying to rollback transaction started " +
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
		try
		{
			if (_transaction != null && _initialTransactionHolder)
			{
				_transaction.Rollback();

				throw new InvalidOperationException("Trying to close connection with " +
					"pending transaction. The transaction is rolled back.");
			}

			if (_transactionStack.Count > 0)
			{
				throw new InvalidOperationException("Trying to close connection with " +
					"pending transaction. The transaction is rolled back.");
			}
		}
		finally
		{
			CurrentContext.CloseConnection(this);

			if (_transaction == null)
			{
				_connection.Close();
			}
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
	private void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		_disposed = true;

		if (disposing)
		{
			Close();
		}
	}

	/// <summary>
	/// Asynchronously closes the database connection and ensures all transactions are properly handled.
	/// </summary>
	internal async Task CloseAsync()
	{
		try
		{
			if (_transaction != null && _initialTransactionHolder)
			{
				await _transaction.RollbackAsync();

				throw new InvalidOperationException("Trying to close connection with " +
					"pending transaction. The transaction is rolled back.");
			}

			if (_transactionStack.Count > 0)
			{
				throw new InvalidOperationException("Trying to close connection with " +
					"pending transaction. The transaction is rolled back.");
			}
		}
		finally
		{
			CurrentContext.CloseConnection(this);

			if (_transaction == null)
			{
				await _connection.CloseAsync();
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;

		await CloseAsync();
		GC.SuppressFinalize(this);
	}
}
