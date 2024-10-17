using ZomboidSavesBackuper.Data;
using ZomboidSavesBackuper.Logger;

namespace ZomboidSavesBackuper
{
    public class SavesBackuper
    {
        private enum BackupResult
        {
            None,
            Skipped,
            Success,
            Fail
        }

        private readonly ILogger _logger;
        private readonly SavesBackupingDataStorage _savesBackupingDataStorage;

        public SavesBackuper()
        {
            var logEntryBuilder = new SimpleLogEntryBuilder();

            _logger = new MultiLogger([
                new ConsoleLogger(logEntryBuilder),
                new FileLogger("log.txt", logEntryBuilder)]);

            _savesBackupingDataStorage = new SavesBackupingDataStorage();
        }

        public void Start()
        {
            SavesBackupingData savesBackupingData = null;

            try
            {
                savesBackupingData = _savesBackupingDataStorage.LoadOrCreate();
            }
            catch (Exception e)
            {
                _logger.Log($"Error on getting backups data: {e.Message}", LogType.Error);
                return;
            }

            BackupLoop(savesBackupingData);
        }

        private void BackupLoop(SavesBackupingData savesBackupingData)
        {
            _logger.Log("Checking saves to make backups...");

            bool isFirstLoop = true;

            while (true)
            {
                var isBackuped = BackupSaves(savesBackupingData);

                if (isBackuped)
                {
                    _logger.Log("Saves backuped successful. Waiting for new saves changes...");
                }
                else
                {
                    if (isFirstLoop)
                    {
                        _logger.Log("All saves backups are up to date. Waiting for saves changes...");
                    }
                }

                isFirstLoop = false;

                Thread.Sleep(5000);
            }
        }

        private bool BackupSaves(SavesBackupingData savesBackupingData)
        {
            var originalChangedSaves = FindOriginalChangedSaves(savesBackupingData);

            bool anyBackuped = false;

            for (var i = 0; i < originalChangedSaves.Count; i++)
            {
                var originalSave = originalChangedSaves[i];

                var backupedSave = savesBackupingData.GetBackupedSave(originalSave.LocationFolder, originalSave.SaveFolder, true);
                var savesLocationPath = savesBackupingData.SavesPath + backupedSave.LocationFolder + "\\";
                var originalSavePath = savesLocationPath + backupedSave.OriginalSaveFolder + "\\";

                var originalSaveModifiedUTC = Directory.GetLastWriteTimeUtc(originalSavePath);

                var backupResult = BackupResult.None;

                while (backupResult != BackupResult.Success && backupResult != BackupResult.Skipped)
                {
                    backupResult = BackupSave(
                        backupedSave,
                        savesBackupingData.SaveMaxBackupCount,
                        savesLocationPath,
                        originalSave.SaveFolder,
                        originalSavePath,
                        (progress, file) => _logger.Log($"Backuping saves {i + 1}/{originalChangedSaves.Count} {backupedSave.OriginalSaveRelativeFolder}: {(int)(progress * 100)}%, {file}"),
                        () => _savesBackupingDataStorage.Save(savesBackupingData));

                    if (backupResult == BackupResult.Fail)
                    {
                        _logger.Log("Retrying in 5 seconds...");
                        Thread.Sleep(5000);
                    }
                }

                if (backupResult == BackupResult.Success)
                {
                    anyBackuped = true;
                }
            }

            return anyBackuped;
        }

        private BackupResult BackupSave(
            BackupedSave backupedSave,
            int maxBackupsCount,
            string saveslocationPath,
            string originalSaveFolder,
            string originalSavePath,
            Action<float, string> progressCallback,
            Action saveBackupingData)
        {
            BackupResult backupResult;

            var originalSaveModifiedUTC = Directory.GetLastWriteTimeUtc(originalSavePath);
            var saveBackupInProgressFolder = $"{originalSaveFolder} [Do not load - Backup is in progress]";
            var saveBackupInProgressPath = $"{saveslocationPath}{saveBackupInProgressFolder}\\";
            var saveBackupToRewrite = backupedSave.GetBackupToRewrite(maxBackupsCount);
            var saveBackupFiles = new List<SaveFileSnapshot>();

            if (saveBackupToRewrite != null)
            {
                var saveBackupToRewritePath = $"{saveslocationPath}{saveBackupToRewrite.FolderName}\\";
                if (!Directory.Exists(saveBackupToRewritePath))
                {
                    saveBackupToRewrite = null;
                }
                else if (saveBackupToRewritePath != saveBackupInProgressPath)
                {
                    if (Directory.Exists(saveBackupInProgressPath))
                    {
                        Directory.Delete(saveBackupInProgressPath, true);
                    }
                    Directory.Move(saveBackupToRewritePath, saveBackupInProgressPath);
                }
            }

            var saveBackupInProgress = new SaveBackup(
                saveBackupInProgressFolder,
                DateTime.MinValue,
                saveBackupToRewrite?.FilesSnapshots.ToList() ?? []);
            backupedSave.AddBackup(saveBackupInProgress, maxBackupsCount, false);

            saveBackupingData?.Invoke();

            try
            {
                var anyCopied = CopySaveFiles(
                    originalSavePath,
                    saveBackupInProgressPath,
                    originalSaveModifiedUTC,
                    saveBackupInProgressFolder,
                    saveBackupToRewrite,
                    ref saveBackupFiles,
                    progressCallback);

                backupResult = anyCopied ? BackupResult.Success : BackupResult.Skipped;
            }
            catch (Exception backupException)
            {
                _logger.Log($"Can't complete backupig: {backupException.Message}", LogType.Warning);

                backupResult = BackupResult.Fail;
            }

            if (backupResult == BackupResult.Skipped || backupResult == BackupResult.Success)
            {
                var saveBackupCompletedFolder = $"{originalSaveFolder} [Backup-{Directory.GetLastWriteTime(originalSavePath):yyyy_MM_dd-HH_mm_ss]}";
                var saveBackupCompletedPath = $"{saveslocationPath}{saveBackupCompletedFolder}\\";

                if (Directory.Exists(saveBackupCompletedPath))
                {
                    Directory.Delete(saveBackupCompletedPath, true);
                }
                Directory.Move(saveBackupInProgressPath, saveBackupCompletedPath);

                if (backupResult == BackupResult.Success)
                {
                    var saveBackup = new SaveBackup(saveBackupCompletedFolder, originalSaveModifiedUTC, saveBackupFiles);
                    backupedSave.AddBackup(saveBackup, maxBackupsCount, true);
                }
            }
            else if (backupResult == BackupResult.Fail)
            {
                var saveBackup = new SaveBackup(saveBackupInProgressFolder, originalSaveModifiedUTC, saveBackupFiles);
                backupedSave.AddBackup(saveBackup, maxBackupsCount, false);
            }

            saveBackupingData?.Invoke();

            return backupResult;
        }

        private bool CopySaveFiles(
            string originalSavePath,
            string saveBackupPath,
            DateTime originalSaveModifiedUTC,
            string backupSaveFolder,
            SaveBackup backupToRewrite,
            ref List<SaveFileSnapshot> resultFilesSnapshots,
            Action<float, string> ProgressCallback)
        {
            bool anyCopied = false;

            var relativeSaveFilesPaths = FindRecursiveRelativeFilesPaths(originalSavePath);
            for (var i = 0; i < relativeSaveFilesPaths.Count; i++)
            {
                var relativeSaveFilePath = relativeSaveFilesPaths[i];

                var originalSaveFilePath = originalSavePath + relativeSaveFilePath;
                var originalSaveFileModifiedUTC = File.GetLastWriteTimeUtc(originalSaveFilePath);

                SaveFileSnapshot fileSnapshot = null;
                backupToRewrite?.TryGetFileSnapshot(relativeSaveFilePath, out fileSnapshot);

                if (fileSnapshot == null)
                {
                    fileSnapshot = new SaveFileSnapshot(relativeSaveFilePath);
                }
                else if (fileSnapshot.ModifiedUTC == originalSaveFileModifiedUTC)
                {
                    resultFilesSnapshots.Add(fileSnapshot);
                    continue;
                }

                var backupfilePath = saveBackupPath + relativeSaveFilePath;
                var backupDirectory = Path.GetDirectoryName(backupfilePath);

                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                ProgressCallback?.Invoke((float)i / relativeSaveFilesPaths.Count, relativeSaveFilePath);

                File.WriteAllBytes(backupfilePath, File.ReadAllBytes(originalSaveFilePath));

                var currentOriginalSaveModifiedUTC = Directory.GetLastWriteTimeUtc(originalSavePath);

                if (originalSaveModifiedUTC != currentOriginalSaveModifiedUTC)
                {
                    throw new Exception("Original save was modified in backuping process, needs to check all files again");
                }

                anyCopied = true;

                fileSnapshot.SetModifiedUTC(originalSaveFileModifiedUTC);
                resultFilesSnapshots.Add(fileSnapshot);
            }

            return anyCopied;
        }

        private List<string> FindRecursiveRelativeFilesPaths(string rootPath, string relativePath = "")
        {
            var path = rootPath + relativePath;

            var result = new List<string>();
            result.AddRange(Directory.GetFiles(path).Select(p => relativePath + Path.GetFileName(p)));

            var folders = Directory.GetDirectories(path).Select(Path.GetFileName);
            foreach (var folder in folders)
            {
                result.AddRange(FindRecursiveRelativeFilesPaths(rootPath, relativePath + folder + "\\"));
            }

            return result;
        }

        private static List<ExistedSave> FindOriginalChangedSaves(SavesBackupingData savesBackupingData)
        {
            var originalChangedSaves = new List<ExistedSave>();

            foreach (var locationFolder in savesBackupingData.SavesLocationFolders)
            {
                var savesLocationPath = savesBackupingData.SavesPath + locationFolder;

                if (!Directory.Exists(savesLocationPath))
                {
                    continue;
                }

                foreach (var savePath in Directory.GetDirectories(savesLocationPath))
                {
                    var saveFolder = Path.GetFileName(savePath);

                    if (IsChangedOriginalSave(savesBackupingData, locationFolder, saveFolder))
                    {
                        originalChangedSaves.Add(new ExistedSave(locationFolder, saveFolder));
                    }
                }
            }

            return originalChangedSaves;
        }

        private static bool IsChangedOriginalSave(SavesBackupingData savesBackupingData, string locationFolder, string saveFolder)
        {
            if (savesBackupingData.IsBackup(locationFolder, saveFolder))
            {
                return false;
            }
          
            var backupedSave = savesBackupingData.GetBackupedSave(locationFolder, saveFolder, false);

            var lastBackupIsUpToDate = false;
            if (backupedSave != null)
            {
                var lastBackup = backupedSave.GetLastBackup();

                if (lastBackup != null)
                {
                    var lastBackupExists = Directory.Exists(savesBackupingData.SavesPath + locationFolder + "\\" + lastBackup.FolderName);

                    if (lastBackupExists)
                    {
                        var originalSavePath = savesBackupingData.GetOriginalSavePath(backupedSave);
                        lastBackupIsUpToDate = lastBackup.IsMostActual(Directory.GetLastWriteTimeUtc(originalSavePath));
                    }
                }
            }

            return backupedSave == null || !lastBackupIsUpToDate;
        }
    }
}
