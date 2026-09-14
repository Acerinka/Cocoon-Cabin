using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CocoonPrototype.Editor
{
    [InitializeOnLoad]
    public static class CocoonGitLfsAutoPuller
    {
        private const string SessionAttemptKey = "Cocoon.GitLfsAutoPuller.Attempted";
        private const string CocoonTaxiLfsInclude = "Assets/ImportedAssets/CocoonTaxi/*.obj";
        private static readonly string[] RequiredLfsAssetPaths =
        {
            "Assets/ImportedAssets/CocoonTaxi/ZOOX.obj",
            "Assets/ImportedAssets/CocoonTaxi/solid.obj",
            "Assets/ImportedAssets/CocoonTaxi/bag.obj",
            "Assets/ImportedAssets/CocoonTaxi/seat.obj",
            "Assets/ImportedAssets/CocoonTaxi/wheelchair.obj"
        };

        private static readonly object OutputLock = new object();
        private static readonly StringBuilder ProcessOutput = new StringBuilder();
        private static Process activePullProcess;
        private static bool pendingRefresh;
        private static int pendingExitCode;

        static CocoonGitLfsAutoPuller()
        {
            EditorApplication.delayCall += EnsureLfsAssetsOnEditorStart;
        }

        [MenuItem("Cocoon/Git LFS/Check Cocoon Imported Assets")]
        public static void CheckCocoonImportedAssets()
        {
            List<string> pointerAssets = FindMissingOrPointerAssets();
            if (pointerAssets.Count == 0)
            {
                UnityEngine.Debug.Log("Cocoon Git LFS check passed. Cocoon imported OBJ assets are present as real files, not LFS pointers.");
                return;
            }

            UnityEngine.Debug.LogWarning("Cocoon Git LFS check found " + pointerAssets.Count +
                " missing/pointer asset(s): " + string.Join(", ", pointerAssets) +
                ". Use Cocoon/Git LFS/Pull Missing Cocoon Assets or run git lfs pull.");
        }

        [MenuItem("Cocoon/Git LFS/Pull Missing Cocoon Assets")]
        public static void PullMissingCocoonAssets()
        {
            StartGitLfsPull(force: true);
        }

        private static void EnsureLfsAssetsOnEditorStart()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SessionState.GetBool(SessionAttemptKey, false))
            {
                return;
            }

            List<string> pointerAssets = FindMissingOrPointerAssets();
            if (pointerAssets.Count == 0)
            {
                return;
            }

            SessionState.SetBool(SessionAttemptKey, true);
            UnityEngine.Debug.LogWarning("Cocoon Git LFS detected missing/pointer imported asset(s): " +
                string.Join(", ", pointerAssets) +
                ". Starting git lfs pull for " + CocoonTaxiLfsInclude + ".");
            StartGitLfsPull(force: false);
        }

        private static List<string> FindMissingOrPointerAssets()
        {
            var results = new List<string>();
            for (int i = 0; i < RequiredLfsAssetPaths.Length; i++)
            {
                string projectPath = RequiredLfsAssetPaths[i];
                string absolutePath = ToProjectAbsolutePath(projectPath);
                if (!File.Exists(absolutePath) || IsGitLfsPointerFile(absolutePath))
                {
                    results.Add(projectPath);
                }
            }

            return results;
        }

        private static bool IsGitLfsPointerFile(string absolutePath)
        {
            FileInfo fileInfo = new FileInfo(absolutePath);
            if (!fileInfo.Exists || fileInfo.Length > 1024)
            {
                return false;
            }

            try
            {
                using (var reader = new StreamReader(absolutePath))
                {
                    string firstLine = reader.ReadLine();
                    return firstLine != null &&
                           firstLine.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal);
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void StartGitLfsPull(bool force)
        {
            if (activePullProcess != null && !activePullProcess.HasExited)
            {
                UnityEngine.Debug.Log("Cocoon Git LFS pull is already running.");
                return;
            }

            if (!force && FindMissingOrPointerAssets().Count == 0)
            {
                return;
            }

            if (!RunGitCommand("lfs install --local", 15000, out string installOutput))
            {
                UnityEngine.Debug.LogWarning("Cocoon Git LFS could not run 'git lfs install --local'. Install Git LFS, then run 'git lfs pull'. Output:\n" + installOutput);
                return;
            }

            lock (OutputLock)
            {
                ProcessOutput.Length = 0;
            }

            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "lfs pull --include=\"" + CocoonTaxiLfsInclude + "\" --exclude=\"\"",
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += AppendProcessOutput;
            process.ErrorDataReceived += AppendProcessOutput;
            process.Exited += OnGitLfsPullExited;

            try
            {
                if (!process.Start())
                {
                    UnityEngine.Debug.LogWarning("Cocoon Git LFS pull did not start.");
                    return;
                }

                activePullProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                EditorApplication.update -= PollGitLfsPull;
                EditorApplication.update += PollGitLfsPull;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Cocoon Git LFS pull failed to start. Install Git LFS, then run 'git lfs pull'. " + exception.Message);
            }
        }

        private static bool RunGitCommand(string arguments, int timeoutMilliseconds, out string output)
        {
            var processOutput = new StringBuilder();
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = ProjectRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    process.Start();
                    processOutput.Append(process.StandardOutput.ReadToEnd());
                    processOutput.Append(process.StandardError.ReadToEnd());
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        output = processOutput.ToString();
                        return false;
                    }

                    output = processOutput.ToString();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception exception)
            {
                output = exception.Message;
                return false;
            }
        }

        private static void AppendProcessOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Data))
            {
                return;
            }

            lock (OutputLock)
            {
                ProcessOutput.AppendLine(args.Data);
            }
        }

        private static void OnGitLfsPullExited(object sender, EventArgs args)
        {
            var process = sender as Process;
            pendingExitCode = process != null ? process.ExitCode : -1;
            pendingRefresh = true;
        }

        private static void PollGitLfsPull()
        {
            if (!pendingRefresh)
            {
                return;
            }

            pendingRefresh = false;
            EditorApplication.update -= PollGitLfsPull;
            string output;
            lock (OutputLock)
            {
                output = ProcessOutput.ToString();
            }

            if (activePullProcess != null)
            {
                activePullProcess.Dispose();
                activePullProcess = null;
            }

            if (pendingExitCode != 0)
            {
                UnityEngine.Debug.LogWarning("Cocoon Git LFS pull failed with exit code " + pendingExitCode + ". Output:\n" + output);
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            for (int i = 0; i < RequiredLfsAssetPaths.Length; i++)
            {
                if (File.Exists(ToProjectAbsolutePath(RequiredLfsAssetPaths[i])))
                {
                    AssetDatabase.ImportAsset(RequiredLfsAssetPaths[i], ImportAssetOptions.ForceUpdate);
                }
            }

            List<string> remainingPointers = FindMissingOrPointerAssets();
            if (remainingPointers.Count > 0)
            {
                UnityEngine.Debug.LogWarning("Cocoon Git LFS pull finished, but these assets are still missing/pointers: " +
                    string.Join(", ", remainingPointers) + ". Output:\n" + output);
                return;
            }

            UnityEngine.Debug.Log("Cocoon Git LFS pull completed and Cocoon imported OBJ assets were refreshed.");
        }

        private static string ToProjectAbsolutePath(string projectRelativePath)
        {
            return Path.Combine(ProjectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }
    }
}
