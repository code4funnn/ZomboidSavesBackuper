namespace ZomboidSavesBackuper.Logger
{
    internal class SimpleLogEntryBuilder: ILogEntryBuilder
    {
        public string Build(string message, LogType logType)
        {
            return ($"[{DateTime.Now.ToString("HH:mm:ss")}] [{logType}] {message}");
        }
    }
}
