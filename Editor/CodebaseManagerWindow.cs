using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodebaseManagement
{
    public class CodebaseManagerWindow : EditorWindow
    {
        private enum Tab
        {
            Scan,
            Restore
        }

        private Tab currentTab = Tab.Scan;
        private Vector2 scrollPosition;
        private string repoName;
        private string rootDirectory;
        private string backupDirectory;
        private List<RestorePoint> restorePoints;
        private int selectedRestorePoint = -1;
        private int compareFromIndex = -1;
        private int compareToIndex = -1;
        private CodebaseScanner scanner;
        private CodebaseRestorer restorer;

        [MenuItem("Tools/Codebase Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<CodebaseManagerWindow>("Codebase Manager");
            window.InitializeDefaults();
            window.Show();
        }

        private void InitializeDefaults()
        {
            rootDirectory = Application.dataPath;
            backupDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "codebase_backups"
            );
            repoName = System.IO.Path.GetFileName(
                System.IO.Directory.GetParent(Application.dataPath).FullName
            );
        }

        private void OnGUI()
        {
            DrawToolbar();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            switch (currentTab)
            {
                case Tab.Scan:
                    DrawScanTab();
                    break;
                case Tab.Restore:
                    DrawRestoreTab();
                    break;
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Toggle(currentTab == Tab.Scan, "Scan", EditorStyles.toolbarButton))
                currentTab = Tab.Scan;
            if (GUILayout.Toggle(currentTab == Tab.Restore, "Restore", EditorStyles.toolbarButton))
                currentTab = Tab.Restore;
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawScanTab()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Codebase Scanner", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            
            repoName = EditorGUILayout.TextField("Repository Name", repoName);
            
            EditorGUILayout.BeginHorizontal();
            rootDirectory = EditorGUILayout.TextField("Root Directory", rootDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string dir = EditorUtility.OpenFolderPanel("Select Root Directory", rootDirectory, "");
                if (!string.IsNullOrEmpty(dir))
                    rootDirectory = dir;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            backupDirectory = EditorGUILayout.TextField("Backup Directory", backupDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string dir = EditorUtility.OpenFolderPanel("Select Backup Directory", backupDirectory, "");
                if (!string.IsNullOrEmpty(dir))
                    backupDirectory = dir;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Scan Codebase", GUILayout.Height(30)))
            {
                PerformScan();
            }
        }

        private void DrawRestoreTab()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Codebase Restorer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (GUILayout.Button("Refresh Restore Points", GUILayout.Height(25)))
            {
                RefreshRestorePoints();
            }

            EditorGUILayout.Space(10);

            if (restorePoints != null && restorePoints.Any())
            {
                EditorGUILayout.LabelField("Available Restore Points:", EditorStyles.boldLabel);
                
                // Draw restore points list
                for (int i = 0; i < restorePoints.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    bool isSelected = EditorGUILayout.Toggle(selectedRestorePoint == i, GUILayout.Width(20));
                    if (isSelected)
                        selectedRestorePoint = i;
                    EditorGUILayout.LabelField($"[{i + 1}] {restorePoints[i].DisplayName}");
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(10);

                if (selectedRestorePoint >= 0)
                {
                    if (GUILayout.Button("Restore to Selected Point", GUILayout.Height(30)))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Restore",
                            "This will restore your codebase to the selected point. Any unsaved changes will be lost. " +
                            "A backup of the current state will be created before proceeding.\n\nContinue?",
                            "Yes, Restore", "Cancel"))
                        {
                            PerformRestore(selectedRestorePoint);
                        }
                    }
                }

                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Compare Restore Points:", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                compareFromIndex = EditorGUILayout.IntField("From Point (index)", compareFromIndex);
                compareToIndex = EditorGUILayout.IntField("To Point (index)", compareToIndex);
                EditorGUILayout.EndHorizontal();

                if (compareFromIndex >= 0 && compareToIndex >= 0)
                {
                    if (GUILayout.Button("Compare Points", GUILayout.Height(25)))
                    {
                        CompareRestorePoints();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No restore points found.", MessageType.Info);
            }
        }

        private void PerformScan()
        {
            try
            {
                scanner = new CodebaseScanner(rootDirectory, repoName, backupDirectory);
                EditorUtility.DisplayProgressBar("Scanning Codebase", "Initializing scan...", 0f);

                string result = scanner.save_results();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Scan Complete",
                    $"Codebase scan completed successfully.\nBackup saved to: {result}", "OK");

                RefreshRestorePoints();
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to scan codebase: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private void RefreshRestorePoints()
        {
            try
            {
                restorer = new CodebaseRestorer(repoName, backupDirectory, rootDirectory);
                restorePoints = restorer.ListRestorePoints();
                selectedRestorePoint = -1;
                compareFromIndex = -1;
                compareToIndex = -1;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load restore points: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private void PerformRestore(int index)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Restoring Codebase", "Preparing to restore...", 0f);
                
                bool success = restorer.RestoreToPoint(index + 1);
                
                EditorUtility.ClearProgressBar();
                
                if (success)
                {
                    EditorUtility.DisplayDialog("Restore Complete", 
                        "Codebase has been restored successfully.\nYou may need to restart Unity to see all changes.", 
                        "OK");
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to restore codebase: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private void CompareRestorePoints()
        {
            try
            {
                var changes = restorer.FindChangesBetweenPoints(compareFromIndex + 1, compareToIndex + 1);
                if (changes != null)
                {
                    string report = GenerateComparisonReport(changes);
                    EditorUtility.DisplayDialog("Comparison Results", report, "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to compare restore points: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private string GenerateComparisonReport(Dictionary<string, List<string>> changes)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("Changes Summary:");
            report.AppendLine($"New files: {changes["new_files"].Count}");
            report.AppendLine($"Modified files: {changes["modified_files"].Count}");
            report.AppendLine($"Deleted files: {changes["deleted_files"].Count}\n");

            void AppendFiles(string type, List<string> files)
            {
                if (files.Count > 0)
                {
                    report.AppendLine($"\n{type} files:");
                    foreach (var file in files.Take(10))
                    {
                        report.AppendLine($"  {(type == "New" ? "+" : type == "Modified" ? "*" : "-")} {file}");
                    }
                    if (files.Count > 10)
                    {
                        report.AppendLine($"  ... and {files.Count - 10} more");
                    }
                }
            }

            AppendFiles("New", changes["new_files"]);
            AppendFiles("Modified", changes["modified_files"]);
            AppendFiles("Deleted", changes["deleted_files"]);

            return report.ToString();
        }
    }
}