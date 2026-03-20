using System.Diagnostics;

namespace Lucia.Installer.Installers;

/// <summary>
/// Windows イベントログのソースを登録・削除するクラス。
/// </summary>
public static class EventLogInstaller
{
    /// <summary>
    /// イベントログにソースを登録する。既に存在する場合は何もしない。
    /// </summary>
    /// <param name="sourceName">登録するイベントログソース名。</param>
    /// <param name="logName">登録先のログ名（省略時は "Application"）。</param>
    public static void Register(string sourceName, string logName = "Application")
    {
        if (!EventLog.SourceExists(sourceName))
            EventLog.CreateEventSource(sourceName, logName);
    }

    /// <summary>
    /// イベントログのソース登録を削除する。存在しない場合は何もしない。
    /// </summary>
    /// <param name="sourceName">削除するイベントログソース名。</param>
    public static void Unregister(string sourceName)
    {
        if (EventLog.SourceExists(sourceName))
            EventLog.DeleteEventSource(sourceName);
    }
}
