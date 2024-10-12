namespace ZomboidSavesBackuper.Logger
{
    public interface ILogger
    {
        void Log(string message, LogType logType = LogType.Info);
    }
}
