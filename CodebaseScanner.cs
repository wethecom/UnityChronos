using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace CodebaseManagement
{
    public class CodebaseScanner 
    {
        [SerializeField] private string repoName;
        [SerializeField] private string rootDirectory;
        [SerializeField] private string backupDirectory;

        private string timestamp;
        private string repoBackupDirectory;
        private string previousBackup;
        private Dictionary<string, FileData> fileData;

        private readonly string[] fileTypes = { ".cs", ".uss", ".uxml" }; // Unity file types

        private class FileData
        {
            public string content;
            public string hash;
        }

        [Serializable]
        private class CodebaseStructure
        {
            public string repository_name;
            public string name;
            public string type;
            public List<CodebaseStructure> children;
            public ScanMetadata scan_metadata;
            public string content;
            public string hash;
            public string extension;
            public long size;
            public string path;
            public string last_modified;
        }

        [Serializable]
        private class ScanMetadata
        {
            public string timestamp;
            public string datetime;
            public string[] target_extensions;
            public string scan_path;
            public string backup_path;
            public bool is_restore_point;
            public string previous_backup;
            public DiffReport diff_report;
        }

        [Serializable]
        private class DiffReport
        {
            public DiffSummary summary;
            public List<string> new_files;
            public List<string> modified_files;
            public List<string> deleted_files;
            public Dictionary<string, string> file_diffs;
        }

        [Serializable]
        private class DiffSummary
        {
            public int new_files;
            public int modified_files;
            public int deleted_files;
        }

        private void Awake()
        {
            InitializeDefaults();
        }
        // Add constructor
        public CodebaseScanner(string rootDir, string repoName, string backupDir)
        {
            this.rootDirectory = rootDir;
            this.repoName = repoName;
            this.backupDirectory = backupDir;
            InitializeDefaults();
        }

        // Add save_results method
        public string save_results()
        {
            Scan();
            // Return the path of the latest backup file
            return Path.Combine(repoBackupDirectory, $"{repoName}_{timestamp}.zip");
        }
        private void InitializeDefaults()
        {
            rootDirectory ??= Application.dataPath;
            repoName ??= Path.GetFileName(rootDirectory);
            backupDirectory ??= Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "codebase_backups"
            );

            repoBackupDirectory = Path.Combine(backupDirectory, repoName);
            Directory.CreateDirectory(repoBackupDirectory);

            timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            previousBackup = FindLatestBackup();
            fileData = new Dictionary<string, FileData>();
        }

        private string FindLatestBackup()
        {
            if (!Directory.Exists(repoBackupDirectory)) return null;

            var backupFiles = Directory.GetFiles(repoBackupDirectory)
                .Where(f => Path.GetFileName(f).StartsWith(repoName) && f.EndsWith(".zip"))
                .OrderByDescending(f => f)
                .ToList();

            return backupFiles.Count > 0 ? backupFiles[0] : null;
        }

        public void Scan()
        {
            Debug.Log($"Scanning repository: {repoName}");
            Debug.Log($"Directory: {rootDirectory}");

            var totalFiles = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                .Count(f => fileTypes.Any(ext => f.EndsWith(ext)));

            Debug.Log($"Found {totalFiles} files to process");

            var codebaseStructure = new CodebaseStructure
            {
                repository_name = repoName,
                name = Path.GetFileName(rootDirectory),
                type = "directory",
                children = new List<CodebaseStructure>(),
                scan_metadata = new ScanMetadata
                {
                    timestamp = timestamp,
                    datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    target_extensions = fileTypes,
                    scan_path = rootDirectory,
                    backup_path = repoBackupDirectory,
                    is_restore_point = previousBackup != null,
                    previous_backup = previousBackup != null ? Path.GetFileName(previousBackup) : null
                }
            };

            ProcessDirectory(rootDirectory, codebaseStructure);
            SaveResults(codebaseStructure);
        }

        private void ProcessDirectory(string directory, CodebaseStructure structure)
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                if (dir.Contains("codebase_backups")) continue;

                var dirStructure = new CodebaseStructure
                {
                    name = Path.GetFileName(dir),
                    type = "directory",
                    children = new List<CodebaseStructure>()
                };
                structure.children.Add(dirStructure);
                ProcessDirectory(dir, dirStructure);
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                if (!fileTypes.Any(ext => file.EndsWith(ext))) continue;

                try
                {
                    var fileInfo = new FileInfo(file);
                    var content = File.ReadAllText(file);
                    var hash = CalculateHash(content);
                    var relPath = Path.GetRelativePath(rootDirectory, file);

                    fileData[relPath] = new FileData
                    {
                        content = content,
                        hash = hash
                    };

                    structure.children.Add(new CodebaseStructure
                    {
                        name = Path.GetFileName(file),
                        type = "file",
                        extension = Path.GetExtension(file),
                        content = content,
                        hash = hash,
                        size = fileInfo.Length,
                        path = relPath,
                        last_modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing {file}: {e.Message}");
                }
            }
        }

        // Replace CalculateFileHash with the correct method name
        private string CalculateHash(string content)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void SaveResults(CodebaseStructure data)
        {
            var previousFiles = ExtractPreviousData();
            var diffReport = GenerateDiffReport(previousFiles);

            // Add diff report to metadata
            data.scan_metadata.diff_report = diffReport;

            var backupFilename = $"{repoName}_{timestamp}";
            var jsonFile = Path.Combine(repoBackupDirectory, $"{backupFilename}.json");
            var diffFile = Path.Combine(repoBackupDirectory, $"{backupFilename}_diff.json");
            var zipFile = Path.Combine(repoBackupDirectory, $"{backupFilename}.zip");

            // Save files
            File.WriteAllText(jsonFile, JsonConvert.SerializeObject(data, Formatting.Indented));
            File.WriteAllText(diffFile, JsonConvert.SerializeObject(diffReport, Formatting.Indented));

            // Create detailed diff report if there are changes
            string detailedDiffFile = null;
            if (diffReport.file_diffs?.Count > 0)
            {
                detailedDiffFile = Path.Combine(repoBackupDirectory, $"{backupFilename}_detailed_diff.txt");
                CreateDetailedDiffReport(diffReport, detailedDiffFile);
            }

            // Create ZIP archive
            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(jsonFile, Path.GetFileName(jsonFile));
                archive.CreateEntryFromFile(diffFile, Path.GetFileName(diffFile));

                if (detailedDiffFile != null)
                {
                    archive.CreateEntryFromFile(detailedDiffFile, Path.GetFileName(detailedDiffFile));
                }

                // Add metadata
                var metadata = new
                {
                    repository_name = repoName,
                    backup_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    timestamp = timestamp,
                    source_path = rootDirectory,
                    file_types_included = fileTypes,
                    is_restore_point = previousBackup != null,
                    previous_backup = previousBackup != null ? Path.GetFileName(previousBackup) : null,
                    changes_summary = new
                    {
                        new_files = diffReport.new_files.Count,
                        modified_files = diffReport.modified_files.Count,
                        deleted_files = diffReport.deleted_files.Count
                    }
                };

                var metadataEntry = archive.CreateEntry("metadata.json");
                using (var writer = new StreamWriter(metadataEntry.Open()))
                {
                    writer.Write(JsonConvert.SerializeObject(metadata, Formatting.Indented));
                }
            }

            // Clean up temporary files
            File.Delete(jsonFile);
            File.Delete(diffFile);
            if (detailedDiffFile != null) File.Delete(detailedDiffFile);

            // Print summary
            Debug.Log($"\nBackup created for repository '{repoName}'");
            if (previousBackup != null)
            {
                Debug.Log($"This is a restore point with changes from: {Path.GetFileName(previousBackup)}");
                Debug.Log($"Changes detected: {diffReport.new_files.Count} new, {diffReport.modified_files.Count} modified, {diffReport.deleted_files.Count} deleted files");
            }
            else
            {
                Debug.Log("This is the initial backup (no previous version found)");
            }
            Debug.Log($"Results saved to: {zipFile}");
        }

        private Dictionary<string, FileData> ExtractPreviousData()
        {
            if (previousBackup == null) return new Dictionary<string, FileData>();

            Debug.Log($"Found previous backup: {Path.GetFileName(previousBackup)}");
            Debug.Log("Extracting data for comparison...");

            var tempDir = Path.Combine(repoBackupDirectory, "temp_extract");
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var archive = ZipFile.OpenRead(previousBackup))
                {
                    var jsonFile = archive.Entries
                        .FirstOrDefault(e => e.Name.EndsWith(".json") &&
                                           !e.Name.EndsWith("_diff.json") &&
                                           e.Name != "metadata.json");

                    if (jsonFile == null) return new Dictionary<string, FileData>();

                    jsonFile.ExtractToFile(Path.Combine(tempDir, jsonFile.Name), true);
                    var previousData = JsonConvert.DeserializeObject<CodebaseStructure>(
                        File.ReadAllText(Path.Combine(tempDir, jsonFile.Name))
                    );

                    var previousFiles = new Dictionary<string, FileData>();
                    ExtractFilesFromStructure(previousData, previousFiles);
                    return previousFiles;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        // In CodebaseScanner.cs, find the section with the duplicate filePath and modify it:


        private void ProcessFiles(string currentDirectory, JObject currentLevel, ref int processedFiles, int totalFiles)
        {
            foreach (var currentFile in Directory.GetFiles(currentDirectory))
            {
                if (fileTypes.Any(ext => currentFile.EndsWith(ext)))
                {
                    string relPath = Path.GetRelativePath(rootDirectory, currentFile);

                    try
                    {
                        // Calculate file hash
                        string fileHash = CalculateHash(File.ReadAllText(currentFile));

                        // Read file content
                        string content = File.ReadAllText(currentFile, Encoding.UTF8);

                        // Store file data for diff generation
                        fileData[relPath] = new FileData
                        {
                            content = content,
                            hash = fileHash
                        };

                        var fileEntry = new JObject
                        {
                            ["name"] = Path.GetFileName(currentFile),
                            ["type"] = "file",
                            ["extension"] = Path.GetExtension(currentFile),
                            ["content"] = content,
                            ["hash"] = fileHash,
                            ["size"] = new FileInfo(currentFile).Length,
                            ["path"] = relPath,
                            ["last_modified"] = File.GetLastWriteTime(currentFile).ToString("yyyy-MM-dd HH:mm:ss")
                        };

                        ((JArray)currentLevel["children"]).Add(fileEntry);

                        processedFiles++;
                        if (processedFiles % 10 == 0 || processedFiles == totalFiles)
                        {
                            float progress = (float)processedFiles / totalFiles;
                            EditorUtility.DisplayProgressBar("Scanning Files",
                                $"Processed {processedFiles}/{totalFiles} files",
                                progress);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error processing {currentFile}: {e.Message}");
                    }
                }
            }
        }

        private void ExtractFilesFromStructure(CodebaseStructure node, Dictionary<string, FileData> filesDict, string currentPath = "")
        {
            if (node.type == "file")
            {
                filesDict[currentPath] = new FileData
                {
                    content = node.content,
                    hash = node.hash
                };
            }
            else if (node.type == "directory" && node.children != null)
            {
                foreach (var child in node.children)
                {
                    var childPath = string.IsNullOrEmpty(currentPath) ?
                        child.name :
                        Path.Combine(currentPath, child.name);

                    ExtractFilesFromStructure(child, filesDict, childPath);
                }
            }
        }
        private DiffReport GenerateDiffReport(Dictionary<string, FileData> previousFiles)
        {
            var currentPaths = new HashSet<string>(fileData.Keys);
            var previousPaths = new HashSet<string>(previousFiles.Keys);

            var newFiles = currentPaths.Except(previousPaths).ToList();
            var deletedFiles = previousPaths.Except(currentPaths).ToList();
            var modifiedFiles = new List<string>();
            var fileDiffs = new Dictionary<string, string>();

            foreach (var path in currentPaths.Intersect(previousPaths))
            {
                if (fileData[path].hash != previousFiles[path].hash)
                {
                    modifiedFiles.Add(path);
                    fileDiffs[path] = GenerateFileDiff(
                        previousFiles[path].content,
                        fileData[path].content,
                        path
                    );
                }
            }

            return new DiffReport
            {
                summary = new DiffSummary
                {
                    new_files = newFiles.Count,
                    modified_files = modifiedFiles.Count,
                    deleted_files = deletedFiles.Count
                },
                new_files = newFiles,
                modified_files = modifiedFiles,
                deleted_files = deletedFiles,
                file_diffs = fileDiffs
            };
        }

        private string GenerateFileDiff(string oldContent, string newContent, string path)
        {
            // Simple diff implementation - in practice you might want to use a more sophisticated diff algorithm
            var oldLines = oldContent.Split('\n');
            var newLines = newContent.Split('\n');
            var diff = new StringBuilder();

            diff.AppendLine($"--- previous/{path}");
            diff.AppendLine($"+++ current/{path}");

            for (int i = 0; i < Math.Max(oldLines.Length, newLines.Length); i++)
            {
                if (i >= oldLines.Length)
                    diff.AppendLine($"+ {newLines[i]}");
                else if (i >= newLines.Length)
                    diff.AppendLine($"- {oldLines[i]}");
                else if (oldLines[i] != newLines[i])
                {
                    diff.AppendLine($"- {oldLines[i]}");
                    diff.AppendLine($"+ {newLines[i]}");
                }
            }

            return diff.ToString();
        }

        private void CreateDetailedDiffReport(DiffReport diffReport, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine($"Diff Report for {repoName} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine("".PadLeft(80, '=') + "\n");

                writer.WriteLine($"New Files ({diffReport.new_files.Count}):");
                foreach (var file in diffReport.new_files)
                    writer.WriteLine($"  + {file}");

                writer.WriteLine($"\nModified Files ({diffReport.modified_files.Count}):");
                foreach (var file in diffReport.modified_files)
                    writer.WriteLine($"  * {file}");

                writer.WriteLine($"\nDeleted Files ({diffReport.deleted_files.Count}):");
                foreach (var file in diffReport.deleted_files)
                    writer.WriteLine($"  - {file}");

                writer.WriteLine("\n\nDetailed Changes:");
                writer.WriteLine("".PadLeft(80, '=') + "\n");

                foreach (var (path, diffContent) in diffReport.file_diffs)
                {
                    writer.WriteLine($"File: {path}");
                    writer.WriteLine("".PadLeft(80, '-'));
                    writer.WriteLine(diffContent);
                    writer.WriteLine();
                }
            }
        }
    }
}