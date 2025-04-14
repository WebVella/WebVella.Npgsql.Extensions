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
The library provides 4 public interfaces:  
**IWvDbService** - provides simple api for working with npgsql connections, transactions and advisory locks  
**IWvDbConnection** - its a npgsql connection wrapper with attached context  
**IWvDbTransactionScope** - transaction scope for npgsql connections, with support of nested transactions usage  
**IWvDbAdvisoryLockScope** - advisory lock scope for executing sql commands with advisory locks  
  
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


#### Connection and Command

Note: no connection open call is needed, the connection is opened automatically during its creation.  
Calling IWvDbConnection.CreateCommand() will create NpogsqlCommand and other npgsql classes can be used for getting or updating data.

```csharp
//connection is open on its creation and closed on leaving the scope
using var connection = dbService.CreateConnection();

//do something with database
var command = connection.CreateCommand("SELECT 1;");
await command.ExecuteNonQueryAsync();
```

#### TransactionScope  

Here is how simple transaction scope looks like. The connection is opened on its creation and closed on leaving the scope.  
 
```csharp
using( var scope = dbService.CreateTransactionScope() )
{
	var command = scope.Connection.CreateCommand("SELECT 1;");
	await command.ExecuteNonQueryAsync();

	//complete commits current transaction
	scope.Complete();
}
```

You can use nested TransactionScopes. I this case same connection will be used for all scopes, but new savepoint will be created for each nested transaction scope. If something fales in nested transaction scope or Complete() is not called, it will rollback to the savepoint and code can continue with upper transaction scope.
```csharp
using( var scope = dbService.CreateTransactionScope() )
{
	//do something with database
	...

	using( var scope = dbService.CreateTransactionScope() )
	{
		//do something with database
		...
		scope.Complete();
	}

	scope.Complete();
}
```

#### AdvisoryLockScope  

AdvisoryLockScopes have similar usage as TransactionScopes.
 
```csharp
const long lockKey = 100;
using (var scope = dbService.CreateAdvisoryLockScope(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();
}
```

YOu can use nested AdvisoryLockScopes also, but be advised using multiple keys with advisory locks is not recommended, because it can lead to deadlocks.  
```csharp
const long lockKey = 100;
using (var scope = dbService.CreateAdvisoryLockScope(lockKey))
{
	var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING");
	await command.ExecuteNonQueryAsync();

	const long nestedLockKey = 101;
	using (var scope = dbService.CreateAdvisoryLockScope(nestedLockKey))
	{
		var command = scope.Connection.CreateCommand("SQL TO UPDATE SOMETHING ELSE");
		await command.ExecuteNonQueryAsync();
	}
}
```
  
  


## License
[![Library license details](https://img.shields.io/badge/%F0%9F%93%9C%0A%20read-license%20details-blue?style=for-the-badge)](https://github.com/WebVella/WebVella.Npgsql.Extensions/blob/main/LICENSE/)
