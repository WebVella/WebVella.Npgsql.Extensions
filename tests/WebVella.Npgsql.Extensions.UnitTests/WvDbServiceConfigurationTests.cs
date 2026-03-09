namespace WebVella.Npgsql.Extensions.UnitTests;

public class WvDbServiceConfigurationTests
{
	[Fact]
	public void ConnectionString_Default_ShouldBeNull()
	{
		// Arrange & Act
		var config = new WvDbServiceConfiguration();

		// Assert
		config.ConnectionString.Should().BeNull();
	}

	[Fact]
	public void ConnectionString_SetValue_ShouldReturnSetValue()
	{
		// Arrange
		var config = new WvDbServiceConfiguration();

		// Act
		config.ConnectionString = "Host=localhost;Database=testdb";

		// Assert
		config.ConnectionString.Should().Be("Host=localhost;Database=testdb");
	}
}
