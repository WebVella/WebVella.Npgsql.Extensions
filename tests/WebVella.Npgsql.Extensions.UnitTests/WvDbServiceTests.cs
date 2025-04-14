namespace WebVella.Npgsql.Extensions.UnitTests;

public class WvDbServiceTests
{
	private readonly Mock<IWvDbServiceConfiguration> _mockConfig;
	private readonly string _testConnectionString = "Host=localhost;Database=testdb;Username=testuser;Password=testpassword";

	public WvDbServiceTests()
	{
		_mockConfig = new Mock<IWvDbServiceConfiguration>();
		_mockConfig.Setup(c => c.ConnectionString).Returns(_testConnectionString);
	}

	[Fact]
	public void Constructor_WithValidConfig_ShouldSetConnectionString()
	{
		// Arrange & Act
		var service = new WvDbService(_mockConfig.Object);

		// Assert
		service.ConnectionString.Should().Be(_testConnectionString);
	}

	[Fact]
	public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
	{
		// Act
		Action act = () => new WvDbService((IWvDbServiceConfiguration)null);

		// Assert
		act.Should().Throw<ArgumentNullException>().WithMessage("*config*");
	}

	[Fact]
	public void Constructor_WithValidConnectionString_ShouldSetConnectionString()
	{
		// Arrange & Act
		var service = new WvDbService(_testConnectionString);

		// Assert
		service.ConnectionString.Should().Be(_testConnectionString);
	}

	[Fact]
	public void Constructor_WithNullOrEmptyConnectionString_ShouldThrowArgumentNullException()
	{
		// Act
		Action act = () => new WvDbService((string)null);

		// Assert
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void CreateConnection_ShouldReturnNewConnection()
	{
		// Arrange
		var service = new WvDbService(_mockConfig.Object);
		var mockContext = new Mock<WvDbConnectionContext>(_testConnectionString);
		Mock.Get(WvDbConnectionContext.GetCurrentContext())
			.Setup(c => c.CreateConnection())
			.Returns(new Mock<WvDbConnection>().Object);

		// Act
		var connection = service.CreateConnection();

		// Assert
		connection.Should().NotBeNull();
		connection.Should().BeAssignableTo<IWvDbConnection>();
	}

	[Fact]
	public void CreateTransactionScope_ShouldReturnNewTransactionScope()
	{
		// Arrange
		var service = new WvDbService(_mockConfig.Object);

		// Act
		var transactionScope = service.CreateTransactionScope();

		// Assert
		transactionScope.Should().NotBeNull();
		transactionScope.Should().BeAssignableTo<IWvDbTransactionScope>();
	}

	[Fact]
	public void CreateTransactionScope_WithLockKey_ShouldReturnNewTransactionScope()
	{
		// Arrange
		var service = new WvDbService(_mockConfig.Object);
		const long lockKey = 12345;

		// Act
		var transactionScope = service.CreateTransactionScope(lockKey);

		// Assert
		transactionScope.Should().NotBeNull();
		transactionScope.Should().BeAssignableTo<IWvDbTransactionScope>();
	}

	[Fact]
	public void CreateAdvisoryLockScope_ShouldReturnNewAdvisoryLockScope()
	{
		// Arrange
		var service = new WvDbService(_mockConfig.Object);
		const long lockKey = 12345;

		// Act
		var advisoryLockScope = service.CreateAdvisoryLockScope(lockKey);

		// Assert
		advisoryLockScope.Should().NotBeNull();
		advisoryLockScope.Should().BeAssignableTo<IWvDbAdvisoryLockScope>();
	}
}