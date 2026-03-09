[![Project Homepage](https://img.shields.io/badge/Homepage-blue?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions)
[![Dotnet](https://img.shields.io/badge/platform-.NET-blue?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions)
[![GitHub Repo stars](https://img.shields.io/github/stars/WebVella/WebVella.Npgsql.Extensions?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions/stargazers)
[![Nuget version](https://img.shields.io/nuget/v/WebVella.Npgsql.Extensions?style=for-the-badge)](https://www.nuget.org/packages/WebVella.Npgsql.Extensions/)
[![Nuget download](https://img.shields.io/nuget/dt/WebVella.Npgsql.Extensions?style=for-the-badge)](https://www.nuget.org/packages/WebVella.Npgsql.Extensions/)
[![License](https://img.shields.io/badge/LICENSE%20details-Community%20MIT%20and%20professional-green?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions/blob/main/LICENSE/)

Checkout our other projects:  
[WebVella ERP](https://github.com/WebVella/WebVella-ERP)  
[Data collaboration - Tefter.bg](https://github.com/WebVella/WebVella.Tefter)  
[Document template generation](https://github.com/WebVella/WebVella.DocumentTemplates)  


## What is WebVella.Npgsql.Extensions?
Open source library which extends npgsql with seamless and easy use of nested transactions and advisory locks

## How to get it
You can either clone this repository or get the [Nuget package](https://www.nuget.org/packages/WebVella.Npgsql.Extensions/)

## Please help by giving a star
GitHub stars guide developers toward great tools. If you find this project valuable, please give it a star – it helps the community and takes just a second!⭐

## Getting started
The library provides 4 public interfaces with both sync and async APIs:  
**IWvDbService** - provides simple api for working with npgsql connections, transactions and advisory locks  
**IWvDbConnection** - npgsql connection wrapper with attached context (implements `IDisposable` and `IAsyncDisposable`)  
**IWvDbTransactionScope** - transaction scope for npgsql connections, with support of nested transactions (implements `IDisposable` and `IAsyncDisposable`)  
**IWvDbAdvisoryLockScope** - advisory lock scope for executing sql commands with advisory locks (implements `IDisposable` and `IAsyncDisposable`)  
  
### Without dependency injection

#### Create an instance of IWvDbService  

You can provide sql connection string as argument  
```csharp
using Microsoft.Extensions.Configuration;
using WebVella.Npgsql.Extensions;

var conString = "Host=localhost;Username=username;Password=password;Database=testdb";
IWvDbService dbService = new WvDbService(conString);
```
or load service configuration from configuration file  
```csharp
var config = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json")
	.Build();

var dbServiceConfig = new WvDbServiceConfiguration();
config.Bind(dbServiceConfig);

IWvDbService dbService = new WvDbService(dbServiceConfig);
```

### With dependency injection

Using configuration object:
```csharp
var config = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json")
	.Build();

var dbServiceConfig = new WvDbServiceConfiguration();
config.Bind(dbServiceConfig);

//it will inject necessary dependencies to use dbService
services.AddWvDbService(dbServiceConfig);
```

Or directly with a connection string:
```csharp
services.AddWvDbService("Host=localhost;Username=username;Password=password;Database=testdb");
```


#### Connection and Command

Note: no connection open call is needed, the connection is opened automatically during its creation.  
Calling IWvDbConnection.CreateCommand() will create NpgsqlCommand and other npgsql classes can be used for getting or updating data.

##### Sync
```csharp
//connection is open on its creation and closed on leaving the scope
using var connection = dbService.CreateConnection();

//do something with database
var command = connection.CreateCommand("SELECT 1;");
await command.ExecuteNonQueryAsync();
```

##### Async
```csharp
//connection is open on its creation and closed on leaving the scope
await using var connection = await dbService.CreateConnectionAsync();

//do something with database
var command = connection.CreateCommand("SELECT 1;");
await command.ExecuteNonQueryAsync();
```

#### TransactionScope  

Transaction scope wraps database operations in a transaction. The connection is opened on its creation and closed on leaving the scope.  
If `Complete()` / `CompleteAsync()` is not called before the scope is disposed, all changes are automatically rolled back.

##### Sync
```csharp
using( var scope = dbService.CreateTransactionScope() )
{
	var command = scope.Connection.CreateCommand("INSERT INTO ...");
	await command.ExecuteNonQueryAsync();

	//complete commits current transaction
	scope.Complete();
}
```

##### Async
```csharp
await using( var scope = await dbService.CreateTransactionScopeAsync() )
{
	var command = scope.Connection.CreateCommand("INSERT INTO ...");
	await command.ExecuteNonQueryAsync();

	//complete commits current transaction
	await scope.CompleteAsync();
}
```

##### Rollback on incomplete scope

If `Complete()` / `CompleteAsync()` is not called, the transaction is rolled back when the scope is disposed.

```csharp
//sync
using( var scope = dbService.CreateTransactionScope() )
{
	var command = scope.Connection.CreateCommand("INSERT INTO ...");
	await command.ExecuteNonQueryAsync();

	//not calling scope.Complete() - all changes will be rolled back
}

//async
await using( var scope = await dbService.CreateTransactionScopeAsync() )
{
	var command = scope.Connection.CreateCommand("INSERT INTO ...");
	await command.ExecuteNonQueryAsync();

	//not calling scope.CompleteAsync() - all changes will be rolled back
}
```

##### Nested TransactionScopes

You can use nested TransactionScopes. In this case the same connection will be used for all scopes, but a new savepoint will be created for each nested transaction scope. If something fails in a nested transaction scope or `Complete()` / `CompleteAsync()` is not called, it will rollback to the savepoint and execution can continue with the outer transaction scope.

Sync:
```csharp
using( var scope = dbService.CreateTransactionScope() )
{
	//do something with database
	...

	using( var nestedScope = dbService.CreateTransactionScope() )
	{
		//do something with database
		...
		nestedScope.Complete();
	}

	scope.Complete();
}
```

Async:
```csharp
await using( var scope = await dbService.CreateTransactionScopeAsync() )
{
	//do something with database
	...

	await using( var nestedScope = await dbService.CreateTransactionScopeAsync() )
	{
		//do something with database
		...
		await nestedScope.CompleteAsync();
	}

	await scope.CompleteAsync();
}
```

##### Nested scopes with partial rollback

When a nested scope is not completed, only the changes made within that scope are rolled back. The outer scope can continue and decide whether to commit or rollback its own changes.
```csharp
await using( var scope = await dbService.CreateTransactionScopeAsync() )
{
	//insert records - these are part of the outer scope
	...

	//nested scope that will be committed
	await using( var nestedScope1 = await dbService.CreateTransactionScopeAsync() )
	{
		//insert more records
		...
		await nestedScope1.CompleteAsync();
	}
	//nestedScope1 changes are preserved

	//nested scope that will be rolled back
	await using( var nestedScope2 = await dbService.CreateTransactionScopeAsync() )
	{
		//insert more records
		...
		//not calling CompleteAsync - these changes will be rolled back
	}
	//nestedScope2 changes are rolled back, but outer scope and nestedScope1 changes remain

	await scope.CompleteAsync();
}
```

##### TransactionScope with advisory lock

You can combine transaction scopes with advisory locks to serialize concurrent access to shared resources. The lock is acquired at the start and released when the transaction completes.
```csharp
//sync
const long lockKey = 100;
using (var scope = dbService.CreateTransactionScope(lockKey))
{
	//read-modify-write is now safe from concurrent access
	var command = scope.Connection.CreateCommand("SELECT ...");
	...
	scope.Complete();
}

//async
const long lockKey = 100;
await using (var scope = await dbService.CreateTransactionScopeAsync(lockKey))
{
	//read-modify-write is now safe from concurrent access
	var command = scope.Connection.CreateCommand("SELECT ...");
	...
	await scope.CompleteAsync();
}
```

#### AdvisoryLockScope  

AdvisoryLockScopes have similar usage as TransactionScopes. They acquire a PostgreSQL advisory lock, which serializes access across concurrent connections. Call `Complete()` / `CompleteAsync()` to release the lock.

##### Sync
```csharp
const long lockKey = 100;
using (var scope = dbService.CreateAdvisoryLockScope(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();

	scope.Complete();
}
```

##### Async
```csharp
const long lockKey = 100;
await using (var scope = await dbService.CreateAdvisoryLockScopeAsync(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();

	await scope.CompleteAsync();
}
```

##### Nested AdvisoryLockScopes

You can use nested AdvisoryLockScopes also, but be advised using multiple keys with advisory locks is not recommended, because it can lead to deadlocks.  

Sync:
```csharp
const long lockKey = 100;
using (var scope = dbService.CreateAdvisoryLockScope(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();

	const long nestedLockKey = 101;
	using (var nestedScope = dbService.CreateAdvisoryLockScope(nestedLockKey))
	{
		var command = nestedScope.Connection.CreateCommand("SQL TO UPDATE SOMETHING ELSE");
		await command.ExecuteNonQueryAsync();

		nestedScope.Complete();
	}

	scope.Complete();
}
```

Async:
```csharp
const long lockKey = 100;
await using (var scope = await dbService.CreateAdvisoryLockScopeAsync(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();

	const long nestedLockKey = 101;
	await using (var nestedScope = await dbService.CreateAdvisoryLockScopeAsync(nestedLockKey))
	{
		var command = nestedScope.Connection.CreateCommand("SQL TO UPDATE SOMETHING ELSE");
		await command.ExecuteNonQueryAsync();

		await nestedScope.CompleteAsync();
	}

	await scope.CompleteAsync();
}
```
  
  


## License
[![Library license details](https://img.shields.io/badge/%F0%9F%93%9C%0A%20read-license%20details-blue?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions/blob/main/LICENSE/)
