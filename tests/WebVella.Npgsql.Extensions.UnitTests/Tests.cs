namespace WebVella.Npgsql.Extensions.UnitTests;

public class Tests
{
	public TestContext Context { get; }

	public Tests()
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
	public async Task CRUDRepoOperationsWithoutTransactionScope()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			Guid id = Guid.NewGuid();

			bool result = await repoService.InsertRecordAsync(id, "Name");
			result.Should().BeTrue();

			var dataTable = await repoService.GetRecordsAsync();

			dataTable.Rows.Count.Should().Be(1);

			result = await repoService.UpdateRecordAsync(id, "NameUpdated");
			result.Should().BeTrue();

			dataTable = await repoService.GetRecordsAsync();

			dataTable.Rows.Count.Should().Be(1);
			dataTable.Rows[0]["name"].ToString().Should().Be("NameUpdated");

			result = await repoService.DropRecordAsync(id);
			result.Should().BeTrue();

			dataTable = await repoService.GetRecordsAsync();
			dataTable.Rows.Count.Should().Be(0);

		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task RepoOperationsWithinTransactionScope()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			using (var scope = dbService.CreateTransactionScope())
			{
				bool result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				scope.Complete();
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
	public async Task RepoOperationsWithinTransactionScopeWithoutComplete()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();

			using (var scope = dbService.CreateTransactionScope())
			{
				var result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

				//here we skip calling complete method for transaction scope
				//it will rollback sql transaction when exit scope range
			}

			(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(0);

		}
		finally
		{
			await repoService.DropTableAsync();
		}
	}

	[Fact]
	public async Task RepoOperationsWithNestedTransactionScope()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		SampleRepositoryService repoService = Context.Services.GetService<SampleRepositoryService>();

		try
		{
			await repoService.CreateTableAsync();
			bool result;

			using (var scope = dbService.CreateTransactionScope())
			{
				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test1");
				result.Should().BeTrue();

				result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test2");
				result.Should().BeTrue();

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

				//because the nested transaction scope use the same
				//connection/transaction as the main transaction scope
				//it has access to newly inserted records above
				using (var nestedScope = dbService.CreateTransactionScope())
				{
					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(2);

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test3");
					result.Should().BeTrue();

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test4");
					result.Should().BeTrue();

					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

					nestedScope.Complete();
				}

				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

				//another nested transaction scope, but will not complete it
				using (var nestedScope = dbService.CreateTransactionScope())
				{
					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test5");
					result.Should().BeTrue();

					result = await repoService.InsertRecordAsync(Guid.NewGuid(), "Test6");
					result.Should().BeTrue();

					(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(6);
					//here we skip calling complete method for transaction scope
					//it will rollback sql transaction when exit scope range
				}

				//because we did not call complete method for the second nested transaction scope
				//records inserted in that scope will be rolled back
				(await repoService.GetRecordsAsync()).Rows.Count.Should().Be(4);

				//here we skip calling complete method for main transaction scope
				//it will rollback sql transaction when exit scope range
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
	public async Task RepoOperationsWithAdvisoryLock()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		AccountRepositoryService repoService = Context.Services.GetService<AccountRepositoryService>();
		const long LOCK_KEY = 1975; //random number for advisory lock key
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
					//here we create advisory lock scope
					//so every task will wait to acquire this lock before continue
					//this code can be separated to a method and result will be the same
					using (var scope = dbService.CreateAdvisoryLockScope(LOCK_KEY))
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

						scope.Complete();
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
	public async Task RepoOperationsWithTransactionScopeAndAdvisoryKey()
	{
		IWvDbService dbService = Context.Services.GetService<IWvDbService>();
		AccountRepositoryService repoService = Context.Services.GetService<AccountRepositoryService>();
		const long LOCK_KEY = 1975; //random number for advisory lock key
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

					//here we create transaction with advisory lock
					//so every task will wait to take this lock before continue
					//this code can be separated to a method and result will be the same
					using (var scope = dbService.CreateTransactionScope(LOCK_KEY))
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

						scope.Complete();
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
	public async Task RepoOperationsWithTransactionScopeWithoutAdvisoryKey()
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
					using (var scope = dbService.CreateTransactionScope())
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

						scope.Complete();
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
}
