using System;
using System.Collections.Generic;
using System.Text;

namespace TokenMiddleware
{
    public static class GeneralConst
    {
        public const string X_NEW_TOKEN = "X-New-Token";
        public const string X_LIMIT_RESET = "X-RateLimit-Reset";
        public const string X_LIMIT_REMAINING = "X-RateLimit-Remaining";
        public const string X_LIMIT_LIMIT = "X-RateLimit-Limit";
        
        public const string SESSION_TOKEN_MARKER = "session";


    }
}
