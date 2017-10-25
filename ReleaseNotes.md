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