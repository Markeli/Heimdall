using System.Text;
using System.Text.RegularExpressions;

namespace Heimdall.Core.Filtering.Rules;

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
					// Every non-wildcard character is escaped so regex metacharacters in IDs are matched literally.
					sb.Append(Regex.Escape(ch.ToString()));
					break;
			}
		}
		sb.Append('$');

		// 50 ms timeout guards against pathological inputs; IgnoreCase matches README semantics.
		return new Regex(
			sb.ToString(),
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
			TimeSpan.FromMilliseconds(50));
	}
}
