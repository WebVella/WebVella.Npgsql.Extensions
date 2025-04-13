namespace WebVella.Npgsql.Extensions;

public interface IWvDbService
{
	/// <summary>
	/// create a new connection to the database
	/// </summary>
	/// <returns></returns>
	IWvDbConnection CreateConnection();

	/// <summary>
	/// create a new transaction scope
	/// </summary>
	/// <param name="lockKey"></param>
	/// <returns></returns>
	IWvDbTransactionScope CreateTransactionScope(long? lockKey = null);

	/// <summary>
	/// create a new advisory lock scope
	/// </summary>
	/// <param name="lockKey"></param>
	/// <returns></returns>
	IWvDbAdvisoryLockScope CreateAdvisoryLockScope(long lockKey);
}

public class WvDbService : IWvDbService
{
	public string ConnectionString { get; private set; }

	public WvDbService(IWvDbServiceConfiguration config)
	{
		if (config == null)
			throw new ArgumentNullException("config");

		ConnectionString = config.ConnectionString;
	}

	public WvDbService(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentNullException(connectionString);

		ConnectionString = connectionString;
	}

	public IWvDbConnection CreateConnection()
	{
		var currentCtx = WvDbConnectionContext.GetCurrentContext();

		if (currentCtx is null)
		{
			currentCtx = WvDbConnectionContext.CreateContext(ConnectionString);
		}

		return currentCtx.CreateConnection();
	}

	public IWvDbTransactionScope CreateTransactionScope(long? lockKey = null)
	{
		return new WvDbTransactionScope(ConnectionString, lockKey);
	}

	public IWvDbAdvisoryLockScope CreateAdvisoryLockScope(long lockKey)
	{
		return new WvDbAdvisoryLockScope(ConnectionString, lockKey);
	}
}
