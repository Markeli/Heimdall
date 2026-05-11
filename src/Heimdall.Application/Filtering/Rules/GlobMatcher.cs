using System.Text;
using System.Text.RegularExpressions;

namespace Heimdall.Application.Filtering.Rules;

internal static class GlobMatcher
{
	public static Regex Compile(string pattern)
	{
		ArgumentException.ThrowIfNullOrEmpty(pattern);

		var sb = new StringBuilder("^");
		foreach (var ch in pattern)
		{
			switch (ch)
			{
				case '*':
					sb.Append(".*");
					break;
				case '?':
					sb.Append('.');
					break;
				default:
					sb.Append(Regex.Escape(ch.ToString()));
					break;
			}
		}
		sb.Append('$');

		return new Regex(
			sb.ToString(),
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
			TimeSpan.FromMilliseconds(50));
	}
}
