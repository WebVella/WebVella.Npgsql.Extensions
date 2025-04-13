using Microsoft.Extensions.Configuration;
using WebVella.Npgsql.Extensions;


/////////////////////////////////////////////////////
//sample using library without depencency injection

/////////////////////////////////////////////////////
//create db service instance using connection string
{
	IWvDbService dbService = new WvDbService("Host=localhost;Username=username;Password=password;Database=testdb");
}

/////////////////////////////////////////////////////
//create db service instance using configuration file
{
	var config = new ConfigurationBuilder()
		.SetBasePath(Directory.GetCurrentDirectory())
		.AddJsonFile("appsettings.json")
		.Build();

	//load database service configuration
	var dbServiceConfig = new WvDbServiceConfiguration();
	config.Bind(dbServiceConfig);

	//simplest way to create dbService
	IWvDbService dbService = new WvDbService(dbServiceConfig);
}

{
	IWvDbService dbService = new WvDbService("Host=localhost;Username=username;Password=password;Database=testdb");
	using var connection = dbService.CreateConnection();

	//do something with database, no need to open/close connection
	//connection is open on its creation and closed on leaving the scope
	var command = connection.CreateCommand("SELECT 1;");
	await command.ExecuteNonQueryAsync();
}
