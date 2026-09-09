// The C# 9 compiler emits a reference to System.Runtime.CompilerServices.IsExternalInit for
// every init-only property. That type ships with .NET 5+ but not with .NET Framework 4.8, so
// on this TFM it has to be declared by the application itself. Declaring it as internal keeps
// it out of the public surface and avoids clashing with any consumer that does the same.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
