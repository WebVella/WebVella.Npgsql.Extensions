namespace WebVella.Npgsql.Extensions.UnitTests;

[Collection("Database")]
public class AsyncDatabaseTests
{
	public TestContext Context { get; }

	public AsyncDatabaseTests()
	{
		Context = new TestContext();

		//load configuration
		var config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.json".ToApplicationPath())
				.AddJsonFile($"appsettings.{Environment.MachineName}.json".ToApplicationPath(), true)
		   .Build();

		//load database service configuration
		var dbServiceConfig = new WvDbServiceConfiguration();
		config.Bind(dbServiceConfig);

		//register the database services
		Context.Services.AddWvDbService(dbServiceConfig);

		//register the test repository service
		Context.Services.AddSingleton<SampleRepositoryService>();

		//register the account repository service
		Context.Services.AddSingleton<AccountRepositoryService>();
	}

	[Fact]
	public async Task AsyncConnection_CRUDOperations_ShouldSucceed()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			Guid id = Guid.NewGuid();

			await using (var connection = await dbService.CreateConnectionAsync())
			{
				var command = connection.CreateCommand(
					"INSERT INTO public.test_table (id, name) VALUES (@id, @name);");
				command.Parameters.Add(new NpgsqlParameter("id", id));
				command.Parameters.Add(new NpgsqlParameter("name", "AsyncName"));
				(await command.ExecuteNonQueryAsync()).Should().Be(1);
			}

			var dataTable = await repoService.GetRecordsAsync();
			dataTable.Rows.Count.Should().Be(1);
			dataTable.Rows[0]["name"].ToString().Should().Be("AsyncName");

			await using (var connection = await dbService.CreateConnectionAsync())
			{
				var command = connection.CreateCommand(
					"UPDATE public.test_table SET name = @name WHERE id = @id;");
				command.Parameters.Add(new NpgsqlParameter("id", id));
				command.Parameters.Add(new NpgsqlParameter("name", "AsyncUpdated"));
				(await command.ExecuteNonQueryAsync()).Should().Be(1);
			}

			dataTable = await repoService.GetRecordsAsync();
			dataTable.Rows.Count.Should().Be(1);
			dataTable.Rows[0]["name"].ToString().Should().Be("AsyncUpdated");

			await using (var connection = await dbService.CreateConnectionAsync())
			{
				var command = connection.CreateCommand(
					"DELETE FROM public.test_table WHERE id = @id;");
				command.Parameters.Add(new NpgsqlParameter("id", id));
				(await command.ExecuteNonQueryAsync()).Should().Be(1);
			}

			dataTable = await repoService.GetRecordsAsync();
			dataTable.Rows.Count.Should().Be(0);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_WithCompleteAsync_ShouldCommitRecords()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var scope = await dbService.CreateTransactionScopeAsync())
			{
				bool result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				await scope.CompleteAsync();
			}

			var dataTable = await repoService.GetRecordsAsync();
			dataTable.Rows.Count.Should().Be(2);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_WithoutComplete_ShouldRollbackRecords()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var scope = await dbService.CreateTransactionScopeAsync())
			{
				var result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

				//here we skip calling CompleteAsync for transaction scope
				//it will rollback sql transaction when DisposeAsync is called
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(0);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_NestedWithMixedComplete_ShouldRollbackAndCommitCorrectly()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();
			bool result;

			await using (var scope = await dbService.CreateTransactionScopeAsync())
			{
				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

				//nested async transaction scope has access to records
				//inserted by parent scope via the shared connection/transaction
				await using (var nestedScope = await dbService.CreateTransactionScopeAsync())
				{
					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test3");
					result.Should().BeTrue();

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test4");
					result.Should().BeTrue();

					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

					await nestedScope.CompleteAsync();
				}

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

				//another nested async transaction scope, but will not complete it
				await using (var nestedScope = await dbService.CreateTransactionScopeAsync())
				{
					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test5");
					result.Should().BeTrue();

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test6");
					result.Should().BeTrue();

					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(6);
					//skip calling CompleteAsync - DisposeAsync will rollback
				}

				//records inserted in the second nested scope are rolled back
				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

				//skip calling CompleteAsync for the main scope
				//it will rollback the entire transaction via DisposeAsync
			}

			//no records should be present in the database
			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(0);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedAsyncTransactionScope_InnerCompleted_OuterNotCompleted_ShouldRollbackAll()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var outerScope = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				await using (var innerScope = await dbService.CreateTransactionScopeAsync())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					await innerScope.CompleteAsync();
				}

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

				//outer scope not completed - should rollback everything
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(0);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedAsyncTransactionScope_AllCompleted_ShouldCommitAll()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var outerScope = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				await using (var innerScope = await dbService.CreateTransactionScopeAsync())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					await innerScope.CompleteAsync();
				}

				await outerScope.CompleteAsync();
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedMixedTransactionScope_SyncOuterAsyncInner_BothCompleted()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			using (var outerScope = dbService.CreateTransactionScope())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				await using (var innerScope = await dbService.CreateTransactionScopeAsync())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					await innerScope.CompleteAsync();
				}

				outerScope.Complete();
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedMixedTransactionScope_AsyncOuterSyncInner_BothCompleted()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var outerScope = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				using (var innerScope = dbService.CreateTransactionScope())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					innerScope.Complete();
				}

				await outerScope.CompleteAsync();
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedMixedTransactionScope_SyncOuterAsyncInner_InnerRolledBack()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			using (var outerScope = dbService.CreateTransactionScope())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				await using (var innerScope = await dbService.CreateTransactionScopeAsync())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					//inner not completed - rolled back via DisposeAsync
				}

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);

				outerScope.Complete();
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task NestedMixedTransactionScope_AsyncOuterSyncInner_InnerRolledBack()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var outerScope = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Outer1");

				using (var innerScope = dbService.CreateTransactionScope())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Inner1");
					//inner not completed - rolled back via Dispose
				}

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);

				await outerScope.CompleteAsync();
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_DeeplyNestedWithMixedComplete_ShouldPartiallyCommit()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var level1 = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Level1");
				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);

				await using (var level2 = await dbService.CreateTransactionScopeAsync())
				{
					await repoService.InsertRecordAsync(Guid.NewGuid(), "Level2");
					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

					await using (var level3 = await dbService.CreateTransactionScopeAsync())
					{
						await repoService.InsertRecordAsync(Guid.NewGuid(), "Level3");
						(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(3);

						await level3.CompleteAsync();
					}

					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(3);

					//level2 not completed - rolls back level2 and level3 changes
				}

				//only level1 record remains
				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);

				await level1.CompleteAsync();
			}

			//level1 committed with 1 record
			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncAdvisoryLockScope_ConcurrentOperations_ShouldSerializeAccess()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		AccountRepositoryService repoService = Context.Services.GetService<AccountRepositoryService>();
		const long LOCK_KEY = 2025;
		Guid accountId = Guid.NewGuid();
		const int tasksCount = 10;

		try
		{
			await repoService.CreateTableAsync();
			bool result;

			result = await repoService.InsertRecordAsync(accountId, 0);
			result.Should().BeTrue();

			Task[] tasks = new Task[tasksCount];
			for (int i = 0; i < tasksCount; i++)
			{
				tasks[i] = Task.Run(async () =>
				{
					//here we create async advisory lock scope
					//so every task will wait to acquire this lock before continue
					await using (var scope = await dbService.CreateAdvisoryLockScopeAsync(LOCK_KEY))
					{
						DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);

						//simulate async work ,that delay will allow
						//other tasks to read accountRow with initial value=0
						//if there is no advisory lock
						await Task.Delay(100);

						//increment account with 1
						int newAmount = (int)accountRow["amount"] + 1;

						result = await repoService.UpdateRecordAsync(accountId, newAmount);

						result.Should().BeTrue();

						await scope.CompleteAsync();
					}
				});
			}

			await Task.WhenAll(tasks);

			DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);
			((int)accountRow["amount"]).Should().Be(tasksCount);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_ConcurrentWithAdvisoryKey_ShouldSerializeAccess()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		AccountRepositoryService repoService = Context.Services.GetService<AccountRepositoryService>();
		const long LOCK_KEY = 2025;
		Guid accountId = Guid.NewGuid();
		const int tasksCount = 10;

		try
		{
			await repoService.CreateTableAsync();
			bool result;

			result = await repoService.InsertRecordAsync(accountId, 0);
			result.Should().BeTrue();

			Task[] tasks = new Task[tasksCount];
			for (int i = 0; i < tasksCount; i++)
			{
				tasks[i] = Task.Run(async () =>
				{
					//here we create async transaction with advisory lock
					//so every task will wait to take this lock before continue
					await using (var scope = await dbService.CreateTransactionScopeAsync(LOCK_KEY))
					{
						DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);

						//simulate async work ,that delay will allow
						//other tasks to read accountRow with initial value=0
						//if there is no advisory lock
						await Task.Delay(100);

						//increment account with 1
						int newAmount = (int)accountRow["amount"] + 1;

						result = await repoService.UpdateRecordAsync(accountId, newAmount);

						result.Should().BeTrue();

						await scope.CompleteAsync();
					}
				});
			}

			await Task.WhenAll(tasks);

			DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);
			((int)accountRow["amount"]).Should().Be(tasksCount);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AsyncTransactionScope_ConcurrentWithoutAdvisoryKey_ShouldAllowRaceCondition()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		AccountRepositoryService repoService = Context.Services.GetService<AccountRepositoryService>();
		Guid accountId = Guid.NewGuid();
		const int tasksCount = 10;

		try
		{
			await repoService.CreateTableAsync();
			bool result;

			result = await repoService.InsertRecordAsync(accountId, 0);
			result.Should().BeTrue();

			Task[] tasks = new Task[tasksCount];
			for (int i = 0; i < tasksCount; i++)
			{
				tasks[i] = Task.Run(async () =>
				{
					await using (var scope = await dbService.CreateTransactionScopeAsync())
					{
						DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);

						//simulate async work ,that delay will allow
						//other tasks to read accountRow with initial value=0
						//if there is no advisory lock
						await Task.Delay(100);

						//all tasks will read amount 0 and increment it with 1 and update to 1
						int newAmount = (int)accountRow["amount"] + 1;
						Debug.WriteLine(newAmount);

						result = await repoService.UpdateRecordAsync(accountId, newAmount);

						result.Should().BeTrue();

						await scope.CompleteAsync();
					}
				});
			}

			await Task.WhenAll(tasks);

			DataRow accountRow = await repoService.GetRecordByIdAsync(accountId);
			((int)accountRow["amount"]).Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task TransactionScope_CompleteCalledTwice_ShouldThrowInvalidOperationException()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			using (var scope = dbService.CreateTransactionScope())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				scope.Complete();

				Action act = () => scope.Complete();
				act.Should().Throw<InvalidOperationException>()
					.WithMessage("*already completed*");
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task TransactionScope_CompleteAsyncCalledTwice_ShouldThrowInvalidOperationException()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			await using (var scope = await dbService.CreateTransactionScopeAsync())
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				await scope.CompleteAsync();

				Func<Task> act = () => scope.CompleteAsync();
				await act.Should().ThrowAsync<InvalidOperationException>()
					.WithMessage("*already completed*");
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(1);
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AdvisoryLockScope_CompleteCalledTwice_ShouldThrowInvalidOperationException()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();
			const long LOCK_KEY = 2025;

			using (var scope = dbService.CreateAdvisoryLockScope(LOCK_KEY))
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				scope.Complete();

				Action act = () => scope.Complete();
				act.Should().Throw<InvalidOperationException>()
					.WithMessage("*already completed*");
			}
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task AdvisoryLockScope_CompleteAsyncCalledTwice_ShouldThrowInvalidOperationException()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();
			const long LOCK_KEY = 2025;

			await using (var scope = await dbService.CreateAdvisoryLockScopeAsync(LOCK_KEY))
			{
				await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				await scope.CompleteAsync();

				Func<Task> act = () => scope.CompleteAsync();
				await act.Should().ThrowAsync<InvalidOperationException>()
					.WithMessage("*already completed*");
			}
		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}
}
