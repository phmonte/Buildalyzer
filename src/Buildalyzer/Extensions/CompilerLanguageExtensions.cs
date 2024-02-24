namespace Buildalyzer;

internal static class CompilerLanguageExtensions
{
    /// <summary>Represents the <see cref="CompilerLanguage"/> as (DEBUG) display string.</summary>
    [Pure]
    public static string Display(this CompilerLanguage language) => language switch
    {
        CompilerLanguage.CSharp => "C#",
        CompilerLanguage.FSharp => "F#",
        CompilerLanguage.VisualBasic => "VB.NET",
        _ => language.ToString(),
    };
}
