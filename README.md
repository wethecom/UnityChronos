# Unity Codebase Manager

A Unity Editor tool for managing, tracking, and restoring your project's codebase. This tool helps developers maintain version control of their Unity project's code files, create restore points, and compare changes between different versions.

### Features

* Scan and backup your codebase
* Create restore points
* Compare changes between different versions
* Restore to previous versions
* Track modifications across .cs, .uss, and .uxml files
* Generate detailed diff reports

### Installation

1. Copy the following files into your Unity project's Assets folder:
```
Assets/$UnityChronos/
├── CodebaseScanner.cs
├── CodebaseRestorer.cs
└── Editor/
    └── CodebaseManagerWindow.cs
```

2. The tool will appear in Unity under `Tools > Codebase Manager`

### Usage

#### Scanning Your Codebase

1. Open the Codebase Manager window (Tools > Codebase Manager)
2. In the "Scan" tab:
   - Verify or modify the Repository Name
   - Set the Root Directory (defaults to Assets folder)
   - Set the Backup Directory (defaults to "codebase_backups" in project root)
3. Click "Scan Codebase" to create a backup

#### Restoring Previous Versions

1. Switch to the "Restore" tab
2. Click "Refresh Restore Points" to see available backups
3. Select a restore point from the list
4. Click "Restore to Selected Point"
   - A backup of the current state will be created before restoration
   - Unity may need to be restarted to see all changes

#### Comparing Versions

1. In the "Restore" tab, locate the indices of the versions you want to compare
2. Enter the indices in the "From Point" and "To Point" fields
3. Click "Compare Points" to see:
   - Number of new files
   - Number of modified files
   - Number of deleted files
   - Detailed list of changes

### Technical Details

The tool consists of three main components:

1. **CodebaseScanner**: Handles scanning and creating backups
   - Generates detailed JSON structure of codebase
   - Creates diff reports between versions
   - Compresses backups into ZIP archives

2. **CodebaseRestorer**: Manages restoration and comparison
   - Lists available restore points
   - Handles file restoration
   - Generates change reports between versions

3. **CodebaseManagerWindow**: Unity Editor interface
   - Provides user-friendly access to features
   - Handles file/directory selection
   - Displays progress and results

### File Format Support

Currently tracks changes in:
- C# Scripts (.cs)
- USS Style Sheets (.uss)
- UXML UI Documents (.uxml)

### Backup Structure

Each backup is stored as a ZIP archive containing:
- Codebase structure (JSON)
- Diff report from previous version
- Detailed textual diff
- Metadata about the backup

### Safety Features

- Automatic backup creation before restoration
- Detailed logging of all operations
- Confirmation dialogs for destructive operations
- Error handling and recovery options

### Requirements

- Unity 2019.4 or higher
- Newtonsoft.Json package

### Best Practices

1. Create regular restore points during development
2. Use meaningful repository names
3. Keep backup directory outside of version control
4. Review changes before restoration
5. Maintain regular external version control (git) alongside this tool

### Error Handling

The tool includes comprehensive error handling:
- Validates all operations before execution
- Creates safety backups before restoration
- Provides detailed error messages
- Maintains recovery options

### Limitations

- Only tracks specified file types (.cs, .uss, .uxml)
- Does not track binary files
- Not a replacement for full version control systems

### Contributing

Feel free to contribute to this project by:
1. Reporting issues
2. Suggesting features
3. Submitting pull requests
4. Improving documentation

### License

This tool is available under the MIT License. Feel free to modify and use it in your projects.

## Notes
- Always test the tools in a safe environment before using them on critical projects.
- Ensure that the **JSON.NET Converters** package is installed to avoid runtime errors.
- Backups are stored in a compressed `.zip` format for easy management and portability.

---

## License
This project is licensed under the MIT License. See the `LICENSE` file for details.

---

## Contributing
Contributions are welcome! Feel free to submit issues or pull requests to improve the tools.

---

## Support
If you encounter any issues or have questions, please open an issue in the repository or contact the maintainer.

Happy coding! 🎮