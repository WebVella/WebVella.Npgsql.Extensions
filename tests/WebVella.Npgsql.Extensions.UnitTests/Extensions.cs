namespace WebVella.Npgsql.Extensions.UnitTests;

public static class StringExtensions
{
	public static string ToApplicationPath(this string fileName)
	{
		var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		Regex appPathMatcher = new Regex(@"(?<!fil)[A-Za-z]:\\+[\S\s]*?(?=\\+bin)");
		var appRoot = appPathMatcher.Match(exePath).Value;
		return Path.Combine(appRoot, fileName);
	}
}
