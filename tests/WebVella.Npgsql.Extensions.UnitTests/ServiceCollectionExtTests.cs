namespace WebVella.Npgsql.Extensions.UnitTests;

public class ServiceCollectionExtTests
{
	private readonly string _testConnectionString = "Host=localhost;Database=testdb;Username=testuser;Password=testpassword";

	[Fact]
	public void AddWvDbService_WithConfig_ShouldResolveIWvDbService()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new WvDbServiceConfiguration { ConnectionString = _testConnectionString };

		// Act
		services.AddWvDbService(config);
		var provider = services.BuildServiceProvider();

		// Assert
		var dbService = provider.GetService<IWvDbService>();
		dbService.Should().NotBeNull();
		dbService.Should().BeOfType<WvDbService>();
	}

	[Fact]
	public void AddWvDbService_WithConfig_ShouldResolveIWvDbServiceConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new WvDbServiceConfiguration { ConnectionString = _testConnectionString };

		// Act
		services.AddWvDbService(config);
		var provider = services.BuildServiceProvider();

		// Assert
		var resolvedConfig = provider.GetService<IWvDbServiceConfiguration>();
		resolvedConfig.Should().NotBeNull();
		resolvedConfig.ConnectionString.Should().Be(_testConnectionString);
	}

	[Fact]
	public void AddWvDbService_WithConnectionString_ShouldResolveIWvDbService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWvDbService(_testConnectionString);
		var provider = services.BuildServiceProvider();

		// Assert
		var dbService = provider.GetService<IWvDbService>();
		dbService.Should().NotBeNull();
		dbService.Should().BeOfType<WvDbService>();
	}

	[Fact]
	public void AddWvDbService_WithConnectionString_ShouldResolveIWvDbServiceConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWvDbService(_testConnectionString);
		var provider = services.BuildServiceProvider();

		// Assert
		var resolvedConfig = provider.GetService<IWvDbServiceConfiguration>();
		resolvedConfig.Should().NotBeNull();
		resolvedConfig.ConnectionString.Should().Be(_testConnectionString);
	}

	[Fact]
	public void AddWvDbService_WithConfig_ShouldReturnServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new WvDbServiceConfiguration { ConnectionString = _testConnectionString };

		// Act
		var result = services.AddWvDbService(config);

		// Assert
		result.Should().BeSameAs(services);
	}

	[Fact]
	public void AddWvDbService_WithConnectionString_ShouldReturnServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddWvDbService(_testConnectionString);

		// Assert
		result.Should().BeSameAs(services);
	}
}
