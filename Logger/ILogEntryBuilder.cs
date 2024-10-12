namespace ZomboidSavesBackuper.Logger
{
    public interface ILogEntryBuilder
    {
        string Build(string message, LogType logType);
    }
}
