using System.Text.RegularExpressions;

namespace TokenMiddleware.RateLimiter
{
    public static class LimitingPolicyResolver
    {
        /// <summary>
        /// Resolves the correct limiting option for a request.
        /// </summary>
        public static ILimitingOption Resolve(LimitingOptions options, bool isAuthorized, string path)
        {
            var policy = isAuthorized ? options.AuthorizedOptions : options.UnauthorizedOptions;

            if (policy == null)
                throw new InvalidOperationException("No limiting policy configured.");

            // Try endpoint-specific overrides
            foreach (var endpointOption in policy.EndpointOptions)
            {
                if (IsMatch(endpointOption.Endpoint, path))
                {
                    return endpointOption;
                }
            }

            // Fallback to general option
            return policy.GeneralOption!;
        }

        /// <summary>
        /// Matches endpoint patterns against the request path.
        /// Supports wildcards (*) and route templates ({param}).
        /// </summary>
        private static bool IsMatch(string pattern, string path)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            // Convert route template or wildcard to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")                // wildcard
                .Replace("\\{", "{").Replace("\\}", "}") // keep braces
                .Replace("{.*?}", "[^/]+")           // route param
                + "$";

            return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}