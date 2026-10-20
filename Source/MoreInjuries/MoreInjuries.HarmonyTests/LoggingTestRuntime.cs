global using System.Diagnostics.CodeAnalysis;
global using Logger = MoreInjuries.Logging.Logger;

namespace MoreInjuries.Roslyn.CompilerServices
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class CompileTimePlatformInfoAttribute : Attribute;
    internal sealed class CompileTimePlatformInfo(char separator)
    {
        internal char DirectorySeparatorChar => separator;
    }
}

namespace MoreInjuries.Logging
{
    internal static partial class Logger
    {
        private static partial Roslyn.CompilerServices.CompileTimePlatformInfo CompileTimePlatformInfo
            => new(System.IO.Path.DirectorySeparatorChar);
    }
}

#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DoesNotReturnAttribute : Attribute;
}
#endif
