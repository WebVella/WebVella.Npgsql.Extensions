namespace WebVella.Npgsql.Extensions.UnitTests;

public class AccountRepositoryService
{
	const string tableName = "account_table";
	private readonly IWvDbService _dbService;

	public AccountRepositoryService(IWvDbService dbService)
	{
		_dbService = dbService;
	}

	public async Task CreateTableAsync()
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"
CREATE TABLE IF NOT EXISTS public.{tableName}
(
    id uuid NOT NULL,
    amount integer NOT NULL DEFAULT 0,
    CONSTRAINT test_table_pkey PRIMARY KEY (id)
)");
		await command.ExecuteNonQueryAsync();
	}

	public async Task DropTableAsync()
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand($"DROP TABLE IF EXISTS {tableName};");
		await command.ExecuteNonQueryAsync();
	}

	public async Task<DataRow> GetRecordByIdAsync(Guid id)
	{
		//simulate async work
		await Task.Delay(1);
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"SELECT * FROM public.{tableName} WHERE id = @id;");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		var commandAdapter = new NpgsqlDataAdapter((NpgsqlCommand)command);
		var dataTable = new DataTable();
		commandAdapter.Fill(dataTable);

		if(dataTable.Rows.Count == 0)
			return null;
		else
			return dataTable.Rows[0];
	}

	public async Task<DataTable> GetRecordsAsync()
	{
		//simulate async work
		await Task.Delay(1);
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"SELECT * FROM public.{tableName};");
		var commandAdapter = new NpgsqlDataAdapter((NpgsqlCommand)command);
		var dataTable = new DataTable();
		commandAdapter.Fill(dataTable);
		return dataTable;
	}

	public async Task<bool> InsertRecordAsync(Guid id, int amount)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"INSERT INTO public.{tableName} (id, amount) VALUES (@id, @amount);");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		command.Parameters.Add(new NpgsqlParameter("amount", amount));
		return await command.ExecuteNonQueryAsync() > 0;
	}

	public async Task<bool> UpdateRecordAsync(Guid id, int amount)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"UPDATE public.{tableName} SET amount = @amount WHERE id = @id;");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		command.Parameters.Add(new NpgsqlParameter("amount", amount));
		return await command.ExecuteNonQueryAsync() > 0;
	}

	public async Task<bool> DropRecordAsync(Guid id)
	{
		using var connection = _dbService.CreateConnection();
		var command = connection.CreateCommand(@$"DELETE FROM public.{tableName} WHERE id = @id;");
		command.Parameters.Add(new NpgsqlParameter("id", id));
		return await command.ExecuteNonQueryAsync() > 0;
	}
}
