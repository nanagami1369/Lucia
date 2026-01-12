
using Lucia.Models.Abstracts;

namespace Lucia.Server.Hubs;

/// <summary>
/// 電源管理サービス
/// </summary>
public interface IPowerService : IService {

    /// <summary>
    /// シャットダウン
    /// </summary>
    Task Restart();

    /// <summary>
    /// 再起動
    /// </summary>
    Task Shutdown();

}
