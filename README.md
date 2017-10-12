# Buildalyzer

![buildalyzer logo](./docs/buildalyzer.png)

A little utility to perform design-time builds of .NET projects without having to think too hard about it:

```csharp
AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
IReadOnlyList<string> sourceFiles = analyzer.GetSourceFiles();
```

It should work with these project types and .NET runtimes:

|                                   | **Host Runtimes**  |                    |
|-----------------------------------|--------------------|--------------------|
| **Project Types**                 | .NET Framework     | .NET Core          |
| Legacy (`ToolsVersion` attribute) | :heavy_check_mark: | :heavy_check_mark: |
| SDK-style (`SDK` attribute)       | :heavy_check_mark: | :heavy_check_mark: |
| SDK-style (SDK `Import` element)    | :heavy_check_mark: | :heavy_check_mark: |

Combinations that aren't displayed here are untested, but might work anyway. Give it a try and report back.

## Installation

Buildalyzer is [available on NuGet](https://www.nuget.org/packages/Buildalyzer/) and can be installed via the commands below:

```
$ Install-Package Buildalyzer
```
or via the .NET Core CLI:

```
$ dotnet add package Buildalyzer
```

Buildalyzer.Workspaces is [available on NuGet](https://www.nuget.org/packages/Buildalyzer.Workspaces/) and can be installed via the commands below:

```
$ Install-Package Buildalyzer.Workspaces
```
or via the .NET Core CLI:

```
$ dotnet add package Buildalyzer.Workspaces
```

Both packages target .NET Standard 2.0.

## Usage

There are two main classes in Buildalyzer: `AnalyzerManager` and `ProjectAnalyzer`.

The `AnalyzerManager` class coordinates loading each individual project and ensures that a project is only analyzed once and following requests return the already-analyzed project.

The `ProjectAnalyzer` class figures out how to configure MSBuild and uses it to load and compile the project in *design-time* mode. Using a design-time build lets us get information about the project such as resolved references and source files without actually having to call the compiler.

To get a `ProjectAnalyzer` you first create an `AnalyzerManager` and then call `GetProject()`:

```csharp
AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
```

Calling `GetProject()` again for the same project path will return the existing `ProjectAnalyzer`. You can iterate all the existing project analyzers with the `IReadOnlyDictionary<string, ProjectAnalyzer>` property `AnalyzerManager.Projects`.

Once you have a `ProjectAnalyzer` for an MSBuild project, you can trigger loading that project, which parses the project and reads targets and properties, by calling `Load()`. This will return the MSBuild `Project` instance for the project.

To compile the project, which triggers evaluation of the specified MSBuild tasks and targets but stops short of invoking the compiler by default in Buildalyzer, call `Compile()`. This implicitly calls `Load()` to get the MSBuild `Project` instance and returns the compiled MSBuild `ProjectInstance`.

You can also access the MSBuild `Project` or `ProjectInstance` objects for the project using the `ProjectAnalyzer.Project` and `ProjectAnalyzer.CompiledProject` properties respectively. These properties will call the corresponding `Load()` and `Compile()` methods if the project has not already been loaded or compiled.

## Helper Methods

`ProjectAnalyzer` includes several helper methods to make parsing the output of MSBuild compilation easier:

**`ProjectAnalyzer.GetSourceFiles()`** - Returns an `IReadOnlyList<string>` with the full path of all resolved source files in the project.

**`ProjectAnalyzer.GetReferences()`** - Returns an `IReadOnlyList<string>` with the full path of all resolved references in the project.

**`ProjectAnalyzer.GetProjectReferences()`** - Returns an `IReadOnlyList<string>` with the full path of the priject file for all resolved project references in the project.

These methods trigger compilation if it hasn't already been performed and will return `null` if the compilation fails.

## Adjusting MSBuild Properties

Buildalyzer sets some MSBuild properties to make loading and compilation work the way it needs to (for example, to trigger a design-time build). You can view these properties with the `IReadOnlyDictionary<string, string>` property `ProjectAnalyzer.GlobalProperties`.

If you want to change the configured properties before loading or compiling the project, use `ProjectAnalyzer.SetGlobalProperty(string key, string value)` and `ProjectAnalyzer.RemoveGlobalProperty(string key)`. Be careful though, you may break the ability to load, compile, or interpret the project if you change the MSBuild properties.

## Logging

Buildalyzer uses the `Microsoft.Extensions.Logging` framework for logging MSBuild output. When you create an `AnayzerManager` you can specify an `ILoggerFactory` that Buildalyzer should use to create loggers. By default, the `ProjectAnalyzer` will log MSBuild output to the provided logger.

## Roslyn Workspaces

The extension library `Buildalyzer.Workspaces` adds extension methods to the Buildalyzer `ProjectAnalyzer` that make it easier to take Buildalyzer output and create a Roslyn `AdhocWorkspace` from it:

```csharp
using Buildalyzer.Workspaces;
// ...

AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
AdhocWorkspace workspace = projectAnalyzer.GetWorkspace();
```

You can also create your own workspace and add Buildalyzer projects to it:

```csharp
using Buildalyzer.Workspaces;
// ...

AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
AdhocWorkspace workspace = new AdhocWorkspace();
Project roslynProject = analyzer.AddToWorkspace(workspace);
```

In both cases, Buildalyzer will attempt to resolve project references within the Roslyn workspace so the Roslyn projects will correctly reference each other.