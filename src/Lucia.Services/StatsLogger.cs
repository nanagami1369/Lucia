using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Lucia.Models.Abstracts;

using Microsoft.Extensions.Logging;

namespace Lucia.Services;

/// <summary>
/// 統計ロガー
/// </summary>
public class StatsLogger<TService> where TService : IService {

    /// <summary>
    /// ドメイン
    /// </summary>
    private static readonly string Domain = typeof(TService).Name.Replace("Service", "").ToLower();

    /// <summary>
    /// 
    /// </summary>
    private readonly ILogger<StatsLogger<TService>> _logger;

    public StatsLogger(ILogger<StatsLogger<TService>> logger) {
        _logger = logger;
    }

    public void LogAction(bool success, object? additionalData = null, [CallerMemberName] string methodName = "") {
        var action = ToSnakeCase(methodName);
        var actionName = $"{Domain}.{action}";

        var logData = new {
            timestamp = DateTime.UtcNow,
            action = actionName,
            success,
            data = additionalData
        };

        _logger.LogInformation("{@Stats}", logData);

        // スネークケースに変換する
        static string ToSnakeCase(string text) {
            return Regex.Replace(text, "([a-z])([A-Z])", "$1_$2").ToLower();
        }

    }

}
