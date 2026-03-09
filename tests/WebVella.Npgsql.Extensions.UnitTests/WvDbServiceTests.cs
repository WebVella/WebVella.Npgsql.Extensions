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
	public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentNullException()
	{
		// Act
		Action act = () => new WvDbService(string.Empty);

		// Assert
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Constructor_WithWhitespaceConnectionString_ShouldThrowArgumentNullException()
	{
		// Act
		Action act = () => new WvDbService("   ");

		// Assert
		act.Should().Throw<ArgumentNullException>();
	}
}