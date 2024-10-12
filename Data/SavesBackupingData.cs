using Newtonsoft.Json;
using System.Xml;

namespace ZomboidSavesBackuper.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SavesBackupingData
    {
        [JsonProperty("savesPath")]
        private readonly string _savesPath;

        [JsonProperty("savesLocationsFolders")]
        private readonly string[] _savesLocationFolders;

        [JsonProperty("saveMaxBackupsCount")]
        private readonly int _saveMaxBackupsCount;

        [JsonProperty("backupedSaves")]
        private readonly Dictionary<string, Dictionary<string, BackupedSave>> _backupedSaves;

        public string SavesPath => _savesPath;

        public string[] SavesLocationFolders => _savesLocationFolders;

        public int SaveMaxBackupCount => _saveMaxBackupsCount; 

        public IEnumerable<BackupedSave> BackupedSaves => _backupedSaves.Values.SelectMany(bl => bl.Values);

        public SavesBackupingData()
        {

        }

        public SavesBackupingData(string savesPath, string[] savesLocationFolder, int saveMaxBackupsCount)
        {
            _savesPath = savesPath;
            _savesLocationFolders = savesLocationFolder;
            _saveMaxBackupsCount = saveMaxBackupsCount;
            _backupedSaves = [];
        }

        public string GetOriginalSavePath(BackupedSave save)
        {
            return $"{SavesPath}{save.LocationFolder}\\{save.OriginalSaveFolder}\\";
        }

        public bool ContainsBackupFolder(string locationFolder, string folderName)
        {
            if (_backupedSaves.TryGetValue(locationFolder, out var locationSavesFolders) &&
                locationSavesFolders.Values.Any(a => a.ContainsBackup(folderName)))
            {
                return true;
            }

            return false;
        }

        public BackupedSave GetOrAddBackupedSave(string locationFolder, string saveFolder)
        {
            if (!_backupedSaves.TryGetValue(locationFolder, out var locationSavesFolders))
            {
                locationSavesFolders = new Dictionary<string, BackupedSave>();
                _backupedSaves[locationFolder] = locationSavesFolders;
            }

            if (!locationSavesFolders.TryGetValue(saveFolder, out var backupedSave))
            {
                backupedSave = new BackupedSave(locationFolder, saveFolder, _saveMaxBackupsCount);
                _backupedSaves[locationFolder][saveFolder] = backupedSave;
            }

            return backupedSave;
        }

        public void RevoveBackupedSave(BackupedSave backupedSave)
        {
            if (!_backupedSaves.TryGetValue(backupedSave.LocationFolder, out var locationSaves))
            {
                return;
            }

            if (!locationSaves.ContainsKey(backupedSave.OriginalSaveFolder))
            {
                return;
            }

            locationSaves.Remove(backupedSave.OriginalSaveFolder);
            if (locationSaves.Count == 0)
            {
                _backupedSaves.Remove(backupedSave.LocationFolder);
            }
        }
    }
}
