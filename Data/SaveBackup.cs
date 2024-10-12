using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ZomboidSavesBackuper.Data.BackupedSave;

namespace ZomboidSavesBackuper.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SaveBackup
    {
        [JsonProperty("folderName")]
        private readonly string _folderName;

        [JsonProperty("originalSaveModifiedUTC")]
        private readonly DateTime _originalSaveModifiedUTC;

        [JsonProperty("filesSnapshots")]
        private readonly List<SaveFileSnapshot> _filesSnapshots;

        public IReadOnlyList<SaveFileSnapshot> FilesSnapshots => _filesSnapshots;

        private readonly Dictionary<string, SaveFileSnapshot> _filesRelativePathsSnapshots;
        private Dictionary<string, SaveFileSnapshot> FilesRelativePathsSnapshots
        {
            get
            {
                if (_filesRelativePathsSnapshots == null)
                {
                    var _filesRelativePathsSnapshots = new Dictionary<string, SaveFileSnapshot>();
                    foreach (var fileSnapshot in _filesSnapshots)
                    {
                        _filesRelativePathsSnapshots[fileSnapshot.RelativePath] = fileSnapshot;
                    }
                    return _filesRelativePathsSnapshots;
                }

                return _filesRelativePathsSnapshots;
            }
        }

        public string FolderName => _folderName;

        public SaveBackup(string folderName, DateTime originalSaveModifiedUTC, List<SaveFileSnapshot> filesSnapshots)
        {
            _folderName = folderName;
            _originalSaveModifiedUTC = originalSaveModifiedUTC;
            _filesSnapshots = filesSnapshots;
        }

        public bool TryGetFileSnapshot(string relativeFilePath, out SaveFileSnapshot snapshot)
        {
            return FilesRelativePathsSnapshots.TryGetValue(relativeFilePath, out snapshot);
        }

        public bool IsMostActual(DateTime originalSaveModifiedUTC)
        {
            return _originalSaveModifiedUTC == originalSaveModifiedUTC;
        }
    }
}
