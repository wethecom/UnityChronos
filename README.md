# CodebaseScanner and CodebaseRestorer for Unity

This repository contains two powerful tools for managing and maintaining Unity projects: **CodebaseScanner** and **CodebaseRestorer**. These tools are designed to help developers create structured backups of their Unity project files, track changes between versions, and restore the project to specific restore points when needed.

---

## Features

### CodebaseScanner
- **File Scanning**: Scans the Unity project directory for specific file types (`.cs`, `.uss`, `.uxml`) and creates a structured representation of the codebase.
- **Backup Creation**: Saves the scanned data as a backup in a compressed `.zip` file, including metadata and a diff report.
- **Diff Reporting**: Compares the current state of the project with the previous backup and generates a detailed report of:
  - New files
  - Modified files
  - Deleted files
  - Line-by-line differences for modified files
- **File Hashing**: Uses SHA-256 hashing to detect changes in file content.
- **ZIP Archiving**: Stores backups and reports in a compressed format for easy management.

### CodebaseRestorer
- **Restore Points**: Lists all available restore points for a Unity project.
- **Project Restoration**: Restores the project to a specific restore point, ensuring that all files are reverted to their previous state.
- **Change Comparison**: Allows comparison between two restore points to identify differences in the project over time.
- **Backup Before Restore**: Automatically creates a backup of the current state before performing a restore operation.

---

## Requirements

This project requires the **JSON.NET Converters** package from the Unity Asset Store. This package is used for handling JSON serialization and deserialization in Unity.

### Installation Instructions
1. Download and import the **JSON.NET Converters** package from the Unity Asset Store:
   [JSON.NET Converters - Simple Compatible Solution](https://assetstore.unity.com/packages/tools/input-management/json-net-converters-simple-compatible-solution-58621)
2. Ensure the package is properly installed in your Unity project before using the tools.

---

## How to Use

### CodebaseScanner
1. Attach the `CodebaseScanner` script to a GameObject in your Unity scene.
2. Configure the following fields in the Unity Inspector:
   - **Root Directory**: The directory to scan (defaults to the Unity project directory).
   - **Repository Name**: The name of the repository (defaults to the project folder name).
   - **Backup Directory**: The directory where backups will be stored (defaults to a `codebase_backups` folder in the parent directory).
3. Call the `SaveResults()` method to scan the project and create a backup.

### CodebaseRestorer
1. Attach the `CodebaseRestorer` script to a GameObject in your Unity scene.
2. Configure the following fields in the Unity Inspector:
   - **Repository Name**: The name of the repository to restore.
   - **Backup Directory**: The directory containing the backups.
   - **Target Directory**: The directory to restore files to (defaults to the Unity project directory).
3. Use the following methods:
   - `ListRestorePoints()`: Lists all available restore points.
   - `RestoreToPoint(index)`: Restores the project to a specific restore point by index.
   - `FindChangesBetweenPoints(fromIndex, toIndex)`: Compares two restore points and lists the differences.

---

## Example Usage

### Scanning the Codebase
```csharp
CodebaseScanner scanner = new CodebaseScanner();
scanner.SaveResults();
```

### Restoring the Codebase
```csharp
CodebaseRestorer restorer = new CodebaseRestorer();
restorer.RestoreToPoint(1); // Restore to the first restore point
```

---

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