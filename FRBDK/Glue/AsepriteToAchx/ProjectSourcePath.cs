using System.Runtime.CompilerServices;

namespace AsepriteToAchx;

internal static class ProjectSourcePath
{
    private const  string  myRelativePath = nameof(ProjectSourcePath) + ".cs";
    private static string? lazyValue;
    public  static string  Value => lazyValue ??= CalculatePath();

    private static string CalculatePath()
    {
        string pathName = GetSourceFilePathName();
        return pathName.Substring( 0, pathName.Length - myRelativePath.Length );
    }
    
    public static string GetSourceFilePathName( [CallerFilePath] string? callerFilePath = null ) //
        => callerFilePath ?? "";
}
