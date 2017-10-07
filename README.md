# Buildalyzer

![buildalyzer logo](./docs/buildalyzer.png)

A little utility to perform design-time builds of .NET projects without having to think too hard about it:

```csharp
Analyzer analyzer = new Analyzer();
IReadOnlyList<string> sourceFiles = analyzer.GetProject(@"C:\MyCode\MyProject.csproj").GetSourceFiles();
```

Should work with any project type on any .NET runtime (file an issue if you hit a problem).

## Buildalyzer.Workspaces

This library adds an extension method to the Buildalyzer `ProjectAnalyzer` that makes it easier to take Buildalyzer output and create a Roslyn `AdhocWorkspace` from it:

```csharp
using Buildalyzer.Workspaces;
// ...
Analyzer analyzer = new Analyzer();
ProjectAnalyzer projectAnalyzer = analyzer.GetProject(@"C:\MyCode\MyProject.csproj");
AdhocWorkspace workspace = projectAnalyzer.GetWorkspace();
```

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

More documentation coming soon.
