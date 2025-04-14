namespace WebVella.Npgsql.Extensions.UnitTests;

/// <summary>
/// Service for managing database operations on a sample repository.
/// </summary>
public class SampleRepositoryService
{
	/// <summary>
	/// The name of the database table used by this service.
	/// </summary>
	const string tableName = "test_table";

	/// <summary>
	/// The database service used for creating connections and managing transactions.
	/// </summary>
	private readonly IWvDbService _dbService;

	/// <summary>
	/// Initializes a new instance of the <see cref="SampleRepositoryService"/> class.
	/// </summary>
	/// <param name="dbService">The database service to use for database operations.</param>
	public SampleRepositoryService(IWvDbService dbService)
	{
		_dbService = dbService;
	}

	/// <summary>
	/// Creates the database table if it does not already exist.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public async Task CreateTableAsync()
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"
CREATE TABLE IF NOT EXISTS public.{tableName}
(
    id uuid NOT NULL,
    name text COLLATE pg_catalog.""default"" NOT NULL,
    CONSTRAINT test_table_pkey PRIMARY KEY (id)
)");
		await command.ExecuteNonQueryAsync();
	}

	/// <summary>
	/// Drops the database table if it exists.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public async Task DropTableAsync()
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand($"DROP TABLE IF EXISTS {tableName};");
		await command.ExecuteNonQueryAsync();
	}

	/// <summary>
	/// Retrieves all records from the database table.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="DataTable"/> with the retrieved records.</returns>
	public async Task<DataTable> GetRecordsAsync()
	{
		// Simulate async work
		await Task.Delay(1);
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"SELECT * FROM public.{tableName};");
		var commandAdapter = new NpgsqlDataAdapter((NpgsqlCommand)command);
		var dataTable = new DataTable();
		commandAdapter.Fill(dataTable);
		return dataTable;
	}

	/// <summary>
	/// Inserts a new record into the database table.
	/// </summary>
	/// <param name="id">The unique identifier of the record.</param>
	/// <param name="name">The name associated with the record.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
	public async Task<bool> InsertRecordAsync(Guid id, string name)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"INSERT INTO public.{tableName} (id, name) VALUES (@id, @name);");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		command.Parameters.Add(new NpgsqlParameter("name", name));
		return await command.ExecuteNonQueryAsync() > 0;
	}

	/// <summary>
	/// Updates an existing record in the database table.
	/// </summary>
	/// <param name="id">The unique identifier of the record to update.</param>
	/// <param name="name">The new name to associate with the record.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
	public async Task<bool> UpdateRecordAsync(Guid id, string name)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"UPDATE public.{tableName} SET name = @name WHERE id = @id;");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		command.Parameters.Add(new NpgsqlParameter("name", name));
		return await command.ExecuteNonQueryAsync() > 0;
	}

	/// <summary>
	/// Deletes a record from the database table.
	/// </summary>
	/// <param name="id">The unique identifier of the record to delete.</param>
	/// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
	public async Task<bool> DropRecordAsync(Guid id)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"DELETE FROM public.{tableName} WHERE id = @id;");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		return await command.ExecuteNonQueryAsync() > 0;
	}
}
