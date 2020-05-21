# 3.0.0

- Added support for specifying additional MSBuild arguments via `EnvironmentOptions.Arguments`.
- Updated the MSBuild.StructuredLogger package to version 2.1.133
- Updated MSBuild packages to 16.5.0 (#140, thanks @colombod)
- Updated Microsoft.CodeAnalysis packages to 3.6.0
- The result of these package updates is that while Buildalyzer itself targets .NET Standard 2.0, any consuming application will need to target either .NET Core 2.1 or .NET Framework 4.7.2 (or higher)

# 2.6.0

- [Refactoring] Refactored key classes into interfaces for easier testing (#132, thanks @richardwerkman)

# 2.5.1

- [Fix] Removed the explicit encoding parameter when generating a `SourceText` in Buildalyzer.Workspaces (#128)
- [Fix] Fixes a race condition in `ProcessRunner` in `Process.WaitForExit()` calls (#125, thanks @duncanawoods)

# 2.5.0

- [Fix] Fix for finding the default SDK in some scenarios due to differences in output from dotnet (#120, thanks @patriksvensson)
- [Refactoring] Updates `Microsoft.CodeAnalysis` dependencies in `Buildalyzer.Workspaces` (#124, thanks @farnyser)
- [Feature] Support for F# projects (#123)

# 2.4.0

- [Fix] Removes `IProjectTransformer` since Buildalyzer no longer feeds project files to the MSBuild API (#114)
- [Feature] Adds back project filtering (#118)
- [Fix] Added "System.Reflection.TypeExtensions" as a direct dependency for .NET Framework consumers (#116)
- [Fix] Disable the COREHOST_TRACE environment variable (#115, thanks @sapsari)

# 2.3.0

- [Feature] Package references are now available directly in `ProjectFile` (#111, thanks @eNeRGy164)
- [Feature] Better future-proof MSBuild path discovery (#106, thanks @colombod)
- [Fix] Fixes for csc arguments parser (#100, thanks azyobuzin)

# 2.2.0

- [Fix] Several fixes for parsing the `csc` command line arguments from the build log (#89)
- [Feature] Passes all defined constants from the build to Roslyn (#86)
- [Feature] Now uses a single MSBuild submission and defers restore phase to MSBuild (#66)
- [Feature] Better processing of `dotnet` console output (#94, thanks @jonstelly)
- [Feature] Better support for two-phase builds like Razor projects (#92, #93, thanks @jonstelly)
- [Feature] Parallelize project builds when creating a Roslyn workspace (#91, thanks @jonstelly)
- [Refactoring] Scopes project ID cache when creating Roslyn workspaces to `AnalyzerManager` (#87. #88, thanks @wadinj)
- [Feature] Adds support for setting a custom `dotnet.exe` path (#84, thanks @itn3000)
- [Fix] Avoid exceptions when overwriting existing keys in `EnvironmentOptions` (#83, thanks @itn3000)
- [Feature] Adds `AnalyzerResult.PackageReferences` to easily access project package references (#82, thanks @mholo65)

# 2.1.0

- [Feature] Adds `AnalyzerManager.Analyze()` support for reading MSBuild binary log files
- [Fix] Fix for pipe communication problems on Linux

# 2.0.1

- [Fix] Fix for hang when the MSBuild process fails to start or logger doesn't connect (#78)

# 2.0.0

- **[Breaking Change]** [Refactoring] Entire API...again. Consider this the "if at first you don't succeed" release.
- [Refactoring] Now uses MSBuild directly by launching out-of-process MSBuild instances instead of the API - if you can build it, Buildalyzer should be able to
- [Refactoring] Reduced build methods to just `ProjectAnalyzer.Build()` and overloads - every build builds now builds every target framework unless otherwise specified and always returns an `AnalyzerResults`
- [Refactoring] `AnalyzerResult` build results are now limited to what we can pull out of MSBuild logs (which is surprisingly a lot) - file an issue if you're missing something you used to get from the old MSBuild API results

# 1.0.1

- [Fix] Fix for AssemblyInfo BOM marking (#74, thanks @bhugot)
- [Refactoring] Updated MSBuild assemblies
- [Refactoring] Updated logging assemblies (#69, thanks @ltcmelo)
- [Fix] Fixes for cross-platform path handling (#67, #68, thanks @ltcmelo)

# 1.0.0

- **[Breaking Change]** [Refactoring] Entire API. Most of the concepts are the same, but the API has changed significantly since the last release (too many changes to enumerate). Documentation is forthcoming, but I wanted to get this release out the door as soon as possible.
- [Refactoring] Introduces a `ProjectTransformer` base class for specifying project file adjustments instead of a delegate
- [Fix] Converts multi-targeted projects into a single target so Buildalyzer can build them (#29, #57)
- [Fix] Calling `ProjectAnalyzer.SetGlobalProperty` and `ProjectAnalyzer.RemoveGlobalProperty` no longer leaks to projects sharing the same `BuildManager`
- [Feature] Added ability to set global properties at the `AnalyzerManager` level (#52, thanks @dfederm)
- [Feature] Added ability to set environment variables for `AnalyzerManager` (all projects) and `ProjectAnalyzer` (specific project)

# 0.5.0

- [Fix] Updated MSBuild API references for latest Visual Studio and .NET SDKs
- [Fix] Added `DisableRarCache` MSBuild property and set to `false` (#56)
- [Fix] Added `NuGet.Common` and `NuGet.ProjectModel` since they're no longer shipped in the box with the .NET SDK (#49, #54)

# 0.4.0

- [Fix] Updated MSBuild API references for latest Visual Studio and .NET SDKs
- [Refactoring] Internally refactored the way temporary environment variables are set and unset
- [Feature] Sets environment variable MSBUILD_EXE_PATH while preserving existing value if there is one (#42)
- [Feature] Sets MSBuild project path so properties like MSBuildThisFileDirectory work (#45, thanks @jirikopecky)

# 0.3.0

- **[Breaking Change]** [Refactoring] Added `AnalyzerManagerOptions` to encapsulate lesser used `AnalyzerManager` constructor arguments (#44)
- [Fix] Updated MSBuild API references for latest Visual Studio and .NET SDKs
- [Feature] Added support for custom build environments (#41, thanks @dfederm)
- [Feature] Added toggle for whether to clean when compiling (#38, #40, thanks @dfederm)

# 0.2.3

- [Feature] Added ability to tweak project files prior to build (#36, thanks @Mpdreamz)
- [Feature] Added ability to filter out specific projects in a solution (#35, thanks @Mpdreamz)
- [Fix] Extended timeout to get `dotnet --info` (#34, thanks @Mpdreamz)

# 0.2.2

- [Feature] Workspace extensions now accept `Workspace` instead of more specific `AdhocWorkspace` (#31, thanks @Jjagg)
- [Fix] Updated MSBuild API references (#32)

# 0.2.1

- [Fix] A better strategy for .NET Framework SDK projects (#23, #25)

# 0.2.0

- **[Breaking Change]** [Refactoring] Changed the `StringBuilder` logging arguments to take a `TextWriter` instead (#24)
- **[Breaking Change]** [Refactoring] Renamed `ProjectAnalyzer.ProjectPath` to `ProjectAnalyzer.ProjectFilePath` (and related method arguments) to make it clear this should be a file path
- [Feature] Allows passing a `XDocument` as a virtual project file (#19)
- [Feature] Adds an option to add known project references to the Roslyn workspace (#22)
- [Fix] Uses the VS toolchain for SDK .NET Framework projects (#23)

# 0.1.6

- [Fix] Ensures only projects are added when loading a solution (#14, #15, thanks @JosephWoodward)

# 0.1.5

- [Feature] Support for loading an entire solution into `AnalyzerManager` (#13)
- [Feature] Chooses the correct SDK folder depending on architecture of the host application (#10)
- [Feature] More test fixes for non-Windows platforms (#9, thanks @JosephWoodward)
- [Feature] Roslyn workspace reflects the correct `OutputKind` of the project (#8, thanks @JosephWoodward)

# 0.1.4

- [Feature] Roslyn workspaces now correctly resolve project references

# 0.1.3

- [Feature] Support for SDK projects with `Import` elements (#1, #6, thanks @mholo65)

# 0.1.2

- Unreleased, no idea where this version went

# 0.1.1

- [Fix] Fixed tests when not running on Windows because .NET Framework is unavailable (thanks @JosephWoodward)
- [Feature] Initial support for creating Roslyn workspaces

# 0.1.0

- Initial release