# Buildalyzer

![buildalyzer logo](./docs/buildalyzer.png)

A little utility to perform design-time builds of .NET projects without having to think too hard about it:

```csharp
Analyzer analyzer = new Analyzer();
IReadOnlyList<string> sourceFiles = analyzer.GetProject(@"C:\MyCode\MyProject.csproj").GetSourceFiles();
```

Should work with any project type on any .NET runtime (file an issue if you hit a problem).


## Installation

Buildalyzer is [available on NuGet](https://www.nuget.org/packages/Buildalyzer/) and can be installed via the commands below:

```
$ Install-Package Buildalyzer
```
or via the .NET Core CLI:

```
$ dotnet add package Buildalyzer
```

More documentation coming soon.
