using Newtonsoft.Json;

namespace ZomboidSavesBackuper.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SaveFileSnapshot
    {
        [JsonProperty("relativePath")]
        private readonly string _relativePath;

        [JsonProperty("lastModifiedUTC")]
        private DateTime _modifiedUTC;


        public string RelativePath => _relativePath;

        public DateTime ModifiedUTC => _modifiedUTC;

        public SaveFileSnapshot(string relativePath)
        {
            _relativePath = relativePath;
        }

        public void SetModifiedUTC(DateTime time)
        {
            _modifiedUTC = time;
        }
    }
}