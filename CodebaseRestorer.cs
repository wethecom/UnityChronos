using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace CodebaseManagement
{
    [Serializable]
    public class ChangesSummary
    {
        public int new_files;
        public int modified_files;
        public int deleted_files;
    }

    [Serializable]
    public class RestorePoint
    {
        public string filename;
        public string timestamp;
        public string date;
        public bool isRestorePoint;
        public ChangesSummary changes;
        public string DisplayName =>
            $"{(isRestorePoint ? "Restore point" : "Initial backup")}: {date}" +
            (changes != null ? $" ({changes.new_files} new, {changes.modified_files} modified, {changes.deleted_files} deleted)" : "");
    }

    public class CodebaseRestorer 
    {
        [SerializeField] private string repoName;
        [SerializeField] private string backupDirectory;
        [SerializeField] private string targetDirectory;

        private string RepoBackupDirectory =>
            repoName != null ? Path.Combine(backupDirectory, repoName) : null;

        private void Awake()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            targetDirectory ??= Application.dataPath;
            repoName ??= Path.GetFileName(targetDirectory);
            backupDirectory ??= Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "codebase_backups"
            );
        }
        public CodebaseRestorer(string repoName, string backupDir, string targetDir)
        {
            this.repoName = repoName;
            this.backupDirectory = backupDir;
            this.targetDirectory = targetDir;
            InitializeDefaults();
        }
        public List<RestorePoint> ListRestorePoints()
        {
            if (string.IsNullOrEmpty(repoName))
            {
                Debug.LogWarning("No repository name specified.");
                return new List<RestorePoint>();
            }

            if (!Directory.Exists(RepoBackupDirectory))
            {
                Debug.LogWarning($"No backups found for repository '{repoName}'.");
                return new List<RestorePoint>();
            }

            var backups = Directory.GetFiles(RepoBackupDirectory)
                .Where(f => Path.GetFileName(f).StartsWith(repoName) && f.EndsWith(".zip"))
                .OrderByDescending(f => f)
                .ToList();

            if (!backups.Any())
            {
                Debug.LogWarning($"No restore points found for repository '{repoName}'.");
                return new List<RestorePoint>();
            }

            var restorePoints = new List<RestorePoint>();
            foreach (var backup in backups)
            {
                var metadata = ExtractMetadata(backup);
                if (metadata != null)
                {
                    restorePoints.Add(new RestorePoint
                    {
                        filename = Path.GetFileName(backup),
                        timestamp = metadata.GetValue("timestamp")?.ToString(),
                        date = metadata.GetValue("backup_date")?.ToString(),
                        isRestorePoint = metadata.GetValue("is_restore_point")?.ToObject<bool>() ?? false,
                        changes = metadata.GetValue("changes_summary")?.ToObject<ChangesSummary>()
                    });
                }
            }

            return restorePoints;
        }

        private JObject ExtractMetadata(string zipPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var metadataEntry = archive.GetEntry("metadata.json");
                    if (metadataEntry != null)
                    {
                        using (var reader = new StreamReader(metadataEntry.Open()))
                        {
                            return JObject.Parse(reader.ReadToEnd());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading metadata from {zipPath}: {e.Message}");
            }
            return null;
        }

        public bool RestoreToPoint(int pointIndex)
        {
            var restorePoints = ListRestorePoints();
            if (!restorePoints.Any() || pointIndex < 0 || pointIndex >= restorePoints.Count)
            {
                Debug.LogError("Invalid restore point index.");
                return false;
            }

            var targetPoint = restorePoints[pointIndex];
            Debug.Log($"Restoring to: {targetPoint.DisplayName}");

            var backupPath = Path.Combine(RepoBackupDirectory, targetPoint.filename);
            var codebaseData = ExtractCodebaseJson(backupPath);
            if (codebaseData == null)
            {
                Debug.LogError("Failed to extract codebase data from backup.");
                return false;
            }

            // Create backup before restore
            var currentTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupBeforeRestore = Path.Combine(targetDirectory, $"pre_restore_backup_{currentTimestamp}");
            try
            {
                Directory.CreateDirectory(backupBeforeRestore);
                foreach (var item in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(targetDirectory, item);
                    var targetPath = Path.Combine(backupBeforeRestore, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    File.Copy(item, targetPath, true);
                }
                Debug.Log($"Created backup of current state at: {backupBeforeRestore}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to create backup of current state: {e.Message}");
            }

            try
            {
                RestoreFilesFromStructure(codebaseData);
                Debug.Log($"Successfully restored to: {targetPoint.DisplayName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during restore: {e.Message}");
                Debug.Log($"You can recover your files from the backup at: {backupBeforeRestore}");
                return false;
            }
        }

        private JObject ExtractCodebaseJson(string zipPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var jsonFiles = archive.Entries
                        .Where(e => e.Name.EndsWith(".json") &&
                                  !e.Name.EndsWith("_diff.json") &&
                                  e.Name != "metadata.json")
                        .ToList();

                    if (jsonFiles.Any())
                    {
                        using (var reader = new StreamReader(jsonFiles[0].Open()))
                        {
                            return JObject.Parse(reader.ReadToEnd());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error extracting codebase data from {zipPath}: {e.Message}");
            }
            return null;
        }

        private void RestoreFilesFromStructure(JObject node, string currentPath = "")
        {
            if (node["type"]?.ToString() == "directory" && node["children"] != null)
            {
                foreach (var child in node["children"])
                {
                    var childPath = Path.Combine(currentPath, child["name"].ToString());

                    if (child["type"].ToString() == "directory")
                    {
                        Directory.CreateDirectory(childPath);
                        RestoreFilesFromStructure(child as JObject, childPath);
                    }
                    else if (child["type"].ToString() == "file" && child["content"] != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(childPath));
                        File.WriteAllText(childPath, child["content"].ToString());
                    }
                }
            }
        }

        public Dictionary<string, List<string>> FindChangesBetweenPoints(int fromIndex, int toIndex)
        {
            var restorePoints = ListRestorePoints();
            if (!restorePoints.Any() ||
                fromIndex < 0 || fromIndex >= restorePoints.Count ||
                toIndex < 0 || toIndex >= restorePoints.Count)
            {
                Debug.LogError("Invalid restore point indices.");
                return null;
            }

            var fromPoint = restorePoints[fromIndex];
            var toPoint = restorePoints[toIndex];

            Debug.Log($"Analyzing changes from:\n[{fromIndex}] {fromPoint.DisplayName}\nTo:\n[{toIndex}] {toPoint.DisplayName}");

            var toZipPath = Path.Combine(RepoBackupDirectory, toPoint.filename);
            if (toPoint.isRestorePoint)
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(toZipPath))
                    {
                        var diffFiles = archive.Entries.Where(e => e.Name.EndsWith("_diff.json")).ToList();
                        if (diffFiles.Any())
                        {
                            using (var reader = new StreamReader(diffFiles[0].Open()))
                            {
                                return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(reader.ReadToEnd());
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error extracting diff data: {e.Message}");
                }
            }

            // Compare the two points directly if diff not available
            var fromZipPath = Path.Combine(RepoBackupDirectory, fromPoint.filename);
            var fromData = ExtractCodebaseJson(fromZipPath);
            var toData = ExtractCodebaseJson(toZipPath);

            if (fromData == null || toData == null)
            {
                Debug.LogError("Failed to extract codebase data for comparison.");
                return null;
            }

            return CompareCodebaseStructures(fromData, toData);
        }

        private Dictionary<string, List<string>> CompareCodebaseStructures(JObject fromData, JObject toData)
        {
            var fromFiles = new Dictionary<string, JObject>();
            var toFiles = new Dictionary<string, JObject>();

            ExtractFilesFromStructure(fromData, fromFiles);
            ExtractFilesFromStructure(toData, toFiles);

            var fromPaths = new HashSet<string>(fromFiles.Keys);
            var toPaths = new HashSet<string>(toFiles.Keys);

            return new Dictionary<string, List<string>>
            {
                ["new_files"] = toPaths.Except(fromPaths).ToList(),
                ["modified_files"] = fromPaths.Intersect(toPaths)
                    .Where(path => fromFiles[path]["hash"].ToString() != toFiles[path]["hash"].ToString())
                    .ToList(),
                ["deleted_files"] = fromPaths.Except(toPaths).ToList()
            };
        }

        private void ExtractFilesFromStructure(JObject node, Dictionary<string, JObject> filesDict, string currentPath = "")
        {
            if (node["type"]?.ToString() == "file")
            {
                filesDict[currentPath] = node as JObject;
            }
            else if (node["type"]?.ToString() == "directory" && node["children"] != null)
            {
                foreach (var child in node["children"])
                {
                    var childPath = string.IsNullOrEmpty(currentPath) ?
                        child["name"].ToString() :
                        Path.Combine(currentPath, child["name"].ToString());
                    ExtractFilesFromStructure(child as JObject, filesDict, childPath);
                }
            }
        }
    }
}