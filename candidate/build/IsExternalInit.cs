// The C# 9 compiler emits a reference to System.Runtime.CompilerServices.IsExternalInit for
// every init-only property. That type ships with .NET 5 and later but not with the .NET
// Framework 4.8 BCL, so on this TFM it has to be declared by the application itself.
//
// It is internal, which means one copy per assembly rather than one for the solution: a
// public copy would leak a framework type out of our public surface and collide with any
// referenced assembly that ships its own. Directory.Build.props therefore links this single
// file into every project instead of each project redeclaring it.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
