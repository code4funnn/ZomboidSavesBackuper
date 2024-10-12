using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace ZomboidSavesBackuper.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BackupedSave
    {
        [JsonProperty("locationFolder")]
        private readonly string _locationFolder;

        [JsonProperty("originalSaveFolder")]
        private readonly string _originalSaveFolder;

        [JsonProperty("isInProgress")]
        private bool _isInProgress;

        [JsonProperty("saveBackups")]
        private readonly List<SaveBackup> _saveBackups = new();

        public string LocationFolder => _locationFolder;

        public string OriginalSaveFolder => _originalSaveFolder;

        public string OriginalSaveRelativeFolder => $"{LocationFolder}\\{_originalSaveFolder}";

        public BackupedSave(string locationFolder, string originalSaveFolder, int backupsCount)
        {
            _locationFolder = locationFolder;
            _originalSaveFolder = originalSaveFolder;
        }

        public SaveBackup GetLastBackup()
        {
            return _saveBackups.LastOrDefault();
        }

        public SaveBackup GetBackupToRewrite(int maxBackupsCount)
        {
            if (_isInProgress)
            {
                return _saveBackups.LastOrDefault();
            }

            if (_saveBackups.Count < maxBackupsCount)
            {
                return null;
            }

            return _saveBackups.FirstOrDefault();
        }

        public bool ContainsBackup(string folderName)
        {
            return _saveBackups.Any(b => b.FolderName == folderName);
        }

        public void AddBackup(SaveBackup backup, int maxBackupsCount, bool completed)
        {
            if (_isInProgress)
            {
                _saveBackups.RemoveAt(_saveBackups.Count - 1);
            }
            else
            {
                if (_saveBackups.Count >= maxBackupsCount)
                {
                    _saveBackups.RemoveAt(0);
                }
            }
        
            _saveBackups.Add(backup);
            _isInProgress = !completed;
        }

        public bool RemoveSaveBackup(SaveBackup backup)
        {
            return backup != null ? _saveBackups.Remove(backup) : false;
        }

    }
}