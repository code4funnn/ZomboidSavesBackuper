using Newtonsoft.Json;

namespace ZomboidSavesBackuper.Data
{
    public class SavesBackupingDataStorage
    {
        private const string FileName = "SavesBackupingData.json";

        public SavesBackupingData LoadOrCreate()
        {
            if (!File.Exists(FileName))
            {
                var data = new SavesBackupingData(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Zomboid\\Saves\\",
                    ["Survivor", "Builder", "Sandbox"],
                    3);

                Save(data);
                return data;
            }

            return JsonConvert.DeserializeObject<SavesBackupingData>(File.ReadAllText(FileName));
        }

        public void Save(SavesBackupingData data)
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
    }
}
