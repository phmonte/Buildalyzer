# Buildalyzer

[![NuGet Version](https://img.shields.io/nuget/v/Buildalyzer.svg)](https://www.nuget.org/packages/Buildalyzer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Buildalyzer.svg)](https://www.nuget.org/packages/Buildalyzer)

![buildalyzer logo](./docs/input/assets/img/buildalyzer.png)

**NOTE:** Major changes in the recent 1.0 release. Most of the concepts are the same, but the API has changed significantly since the last release (too many changes to enumerate). Revised documentation is forthcoming, but I wanted to get this release out the door as soon as possible. The information below is probably out of date right now and won't work as-written. You've been warned.

---

A little utility to perform design-time builds of .NET projects without having to think too hard about it:

```csharp
AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
IReadOnlyList<string> sourceFiles = analyzer.GetSourceFiles();
```

It should work with these project types and .NET runtimes:

|                                   | **Host Runtimes**    |                    |
|-----------------------------------|----------------------|--------------------|
| **Project Types**                 | .NET Framework       | .NET Core <sub>1</sub> |
| Legacy (`ToolsVersion` attribute) | :heavy_check_mark:   | :heavy_check_mark: |
| SDK-style (`SDK` attribute)       | :heavy_check_mark: <sub>2</sub> | :heavy_check_mark: |
| SDK-style (SDK `Import` element)  | :heavy_check_mark: <sub>2</sub> | :heavy_check_mark: |

1. You must have the [.NET Core SDK](https://www.microsoft.com/net/download/core) installed and in your path environment variable to build and analyze SDK-style projects.
2. You must have a .NET Core SDK installed that matches the architecture of the .NET Framework host application. For example, if the host application is x86 then the .NET Core x86 SDK must be installed.

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

You can add all projects in a solution to the `AnalyzerManager` by passing the solution path as the first argument of the `AnalyzerManager` constructor. This will parse the solution file and execute `GetProject()` for each of the projects that it finds.

Calling `GetProject()` again for the same project path will return the existing `ProjectAnalyzer`. You can iterate all the existing project analyzers with the `IReadOnlyDictionary<string, ProjectAnalyzer>` property `AnalyzerManager.Projects`.

Once you have a `ProjectAnalyzer` for an MSBuild project, you can trigger loading that project, which parses the project and reads targets and properties, by calling `Load()`. This will return the MSBuild `Project` instance for the project.

To compile the project, which triggers evaluation of the specified MSBuild tasks and targets but stops short of invoking the compiler by default in Buildalyzer, call `Compile()`. This implicitly calls `Load()` to get the MSBuild `Project` instance and returns the compiled MSBuild `ProjectInstance`.

You can also access the MSBuild `Project` or `ProjectInstance` objects for the project using the `ProjectAnalyzer.Project` and `ProjectAnalyzer.CompiledProject` properties respectively. These properties will call the corresponding `Load()` and `Compile()` methods if the project has not already been loaded or compiled.

## Helper Methods

`ProjectAnalyzer` includes several helper methods to make parsing the output of MSBuild compilation easier:

**`ProjectAnalyzer.GetSourceFiles()`** - Returns an `IReadOnlyList<string>` with the full path of all resolved source files in the project.

**`ProjectAnalyzer.GetReferences()`** - Returns an `IReadOnlyList<string>` with the full path of all resolved references in the project.

**`ProjectAnalyzer.GetProjectReferences()`** - Returns an `IReadOnlyList<string>` with the full path of the project file for all resolved project references in the project.

These methods trigger compilation if it hasn't already been performed and will return `null` if the compilation fails.

## Adjusting MSBuild Properties

Buildalyzer sets some MSBuild properties to make loading and compilation work the way it needs to (for example, to trigger a design-time build). You can view these properties with the `IReadOnlyDictionary<string, string>` property `ProjectAnalyzer.GlobalProperties`.

If you want to change the configured properties before loading or compiling the project, there are two options:

* `AnalyzerManager.SetGlobalProperty(string key, string value)` and `AnalyzerManager.RemoveGlobalProperty(string key)`. This will set the global properties for all projects loaded by this `AnalyzerManager`.
* `ProjectAnalyzer.SetGlobalProperty(string key, string value)` and `ProjectAnalyzer.RemoveGlobalProperty(string key)`. This will set the global properties for just this project.

Be careful though, you may break the ability to load, compile, or interpret the project if you change the MSBuild properties.

## Logging

Buildalyzer uses the `Microsoft.Extensions.Logging` framework for logging MSBuild output. When you create an `AnayzerManager` you can specify an `ILoggerFactory` that Buildalyzer should use to create loggers. By default, the `ProjectAnalyzer` will log MSBuild output to the provided logger.

## Roslyn Workspaces

The extension library `Buildalyzer.Workspaces` adds extension methods to the Buildalyzer `ProjectAnalyzer` that make it easier to take Buildalyzer output and create a Roslyn `AdhocWorkspace` from it:

```csharp
using Buildalyzer.Workspaces;
// ...

AnalyzerManager manager = new AnalyzerManager();
ProjectAnalyzer analyzer = manager.GetProject(@"C:\MyCode\MyProject.csproj");
AdhocWorkspace workspace = analyzer.GetWorkspace();
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

## Troubleshooting

### Check The Build Log

If something isn't working, try passing a `TextWriter` or `ILoggerFactory` into the `AnalyzerManager`. The log output after you call `ProjectAnalyzer.Load()` or `ProjectAnalyzer.Compile()` is often very helpful in tracking down problems.

### Microsoft.Build.Tasks.CodeAnalysis and Microsoft.Build.Framework Mismatch

If you see an error like this in the MSBuild log when using Buildalyzer from a .NET Framework project:

```
Target CoreCompile:
C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\Roslyn\Microsoft.CSharp.Core.targets(84,5): error MSB4127: The "Csc" task could not be instantiated from the assembly "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\Roslyn\Microsoft.Build.Tasks.CodeAnalysis.dll". Please verify the task assembly has been built using the same version of the Microsoft.Build.Framework assembly as the one installed on your computer and that your host application is not missing a binding redirect for Microsoft.Build.Framework. Unable to cast object of type 'Microsoft.CodeAnalysis.BuildTasks.Csc' to type 'Microsoft.Build.Framework.ITask'.
```

You might need to add the following binding redirect to your `app.config` file:

```xml
<dependentAssembly>
    <assemblyIdentity name="Microsoft.Build.Framework" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-99.9.9.9" newVersion="15.1.0.0" />
</dependentAssembly>
```

### MSB6003 due to System.IO.FileSystem

If you see an error like this in the MSBuild log when using Buildalyzer from a .NET Framework project:

```
C:\Program Files\dotnet\sdk\2.0.2\Roslyn\Microsoft.CSharp.Core.targets(84,5): error MSB6003: The specified task executable "csc.exe" could not be run. Could not load file or assembly 'System.IO.FileSystem, Version=4.0.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' or one of its dependencies. The system cannot find the file specified.
```

You might need to add the following binding redirect to your `app.config` file:

```xml
<dependentAssembly>
    <assemblyIdentity name="System.IO.FileSystem" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-4.0.3.0" newVersion="4.0.3.0" />
</dependentAssembly>
```

---
This project is maintained by Dave Glick ([@daveaglick](https://github.com/daveaglick)), Joseph Woodward ([@JosephWoodward](https://github.com/JosephWoodward)), and [other awesome contributors](https://github.com/daveaglick/Buildalyzer/graphs/contributors).
