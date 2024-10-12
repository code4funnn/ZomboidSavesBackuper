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
                ValidateBackups(savesBackupingData);

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

        private void ValidateBackups(SavesBackupingData backupsData)
        {
            var invalidBackupedSaves = new List<BackupedSave>();

            foreach (var backupedSave in backupsData.BackupedSaves)
            {
                var originalSavePath = backupsData.GetOriginalSavePath(backupedSave);
                if (!Directory.Exists(originalSavePath))
                {
                    invalidBackupedSaves.Add(backupedSave);
                }
            }

            foreach (var backupedSave in invalidBackupedSaves)
            {
                _logger.Log($"Missed save {backupedSave.OriginalSaveRelativeFolder}. Backup data will be removed", LogType.Warning);
                backupsData.RevoveBackupedSave(backupedSave);
            }

            _savesBackupingDataStorage.Save(backupsData);
        }

        private bool BackupSaves(SavesBackupingData savesBackupingData)
        {
            var originalSavesFolders = FindOriginalSavesFolders(savesBackupingData);

            var currentProcessingSaveNum = 0;
            var totalSavesToProcessCount = originalSavesFolders.Sum(f => f.Value.Count);

            var backupedSaves = new List<string>();

            foreach (var originalSavesFolder in originalSavesFolders)
            {
                var locationFolder = originalSavesFolder.Key;
                foreach (var originalSaveFolder in originalSavesFolder.Value)
                {
                    currentProcessingSaveNum++;

                    var backupedSave = savesBackupingData.GetOrAddBackupedSave(locationFolder, originalSaveFolder);
                    var savesLocationPath = savesBackupingData.SavesPath + backupedSave.LocationFolder + "\\";
                    var originalSavePath = savesLocationPath + backupedSave.OriginalSaveFolder + "\\";

                    var originalSaveModifiedUTC = Directory.GetLastWriteTimeUtc(originalSavePath);

                    if (!backupedSave.IsNeedMakeBackup(originalSaveModifiedUTC))
                    {
                        continue;
                    }

                    var backupResult = BackupResult.None;

                    while (backupResult != BackupResult.Success && backupResult != BackupResult.Skipped)
                    {
                        backupResult = BackupSave(
                            backupedSave,
                            savesBackupingData.SaveMaxBackupCount,
                            savesLocationPath,
                            originalSaveFolder,
                            originalSavePath,
                            (progress, file) => _logger.Log($"Backuping saves {currentProcessingSaveNum}/{totalSavesToProcessCount} {backupedSave.OriginalSaveRelativeFolder}: {(int)(progress * 100)}%, {file}"),
                            () => _savesBackupingDataStorage.Save(savesBackupingData));

                        _savesBackupingDataStorage.Save(savesBackupingData);

                        if (backupResult == BackupResult.Fail)
                        {
                            _logger.Log("Retrying in 5 seconds...");
                            Thread.Sleep(5000);
                        }
                    }


                    if (backupResult == BackupResult.Success)
                    {
                        backupedSaves.Add(backupedSave.OriginalSaveRelativeFolder);
                    }
                }
            }

            return backupedSaves.Count > 0;
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
                    Directory.Move(saveBackupToRewritePath, saveBackupInProgressPath);
                }
            }

            backupedSave.AddBackupInProgress(
                new SaveBackup(saveBackupInProgressFolder, saveBackupToRewrite?.FilesSnapshots.ToList() ?? []), maxBackupsCount);
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
                _logger.Log($"Can't complete backupig: {backupException.Message}", LogType.Error);

                backupResult = BackupResult.Fail;
            }
            
            if (backupResult == BackupResult.Skipped || backupResult == BackupResult.Success)
            {
                var saveBackupCompletedFolder = $"{originalSaveFolder} [Backup-{originalSaveModifiedUTC:yyyy_MM_dd-HH_mm_ss]}";
                var saveBackupCompletedPath = $"{saveslocationPath}{saveBackupCompletedFolder}\\";

                Directory.Move(saveBackupInProgressPath, saveBackupCompletedPath);

                if(backupResult == BackupResult.Success)
                {
                    var saveBackup = new SaveBackup(saveBackupCompletedFolder, saveBackupFiles);
                    backupedSave.AddCompletedBackup(originalSaveModifiedUTC, saveBackup, maxBackupsCount);
                }
            }
            else if (backupResult == BackupResult.Fail)
            {
                var saveBackup = new SaveBackup(saveBackupInProgressFolder, saveBackupFiles);
                backupedSave.AddBackupInProgress(saveBackup, maxBackupsCount);
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

        private static Dictionary<string, List<string>> FindOriginalSavesFolders(SavesBackupingData workData)
        {
            var originalSavesFolders = new Dictionary<string, List<string>>();

            foreach (var locationFolderName in workData.SavesLocationFolders)
            {
                var savesPath = workData.SavesPath + locationFolderName;

                if (!Directory.Exists(savesPath))
                {
                    continue;
                }

                var directories = Directory.GetDirectories(savesPath);
                var saveFolders = directories
                    .Select(Path.GetFileName)
                    .Where(f => f != null && !workData.ContainsBackupFolder(locationFolderName, f))
                    .ToList();

                if (saveFolders.Count > 0)
                {
                    originalSavesFolders[locationFolderName] = saveFolders;
                }
            }

            return originalSavesFolders;
        }
    }
}