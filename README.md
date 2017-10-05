# Buildalyzer

![buildalyzer logo](./docs/buildalyzer.png)

A little utility to perform design-time builds of .NET projects without having to think too hard about it:

```csharp
Analyzer analyzer = new Analyzer();
IReadOnlyList<string> sourceFiles = analyzer.GetProject(@"C:\MyCode\MyProject.csproj").GetSourceFiles();
```

Should work with any project type on any .NET runtime (file an issue if you hit a problem).

More documentation coming soon.