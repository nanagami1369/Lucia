using Lucia.Models.Abstracts;

namespace Lucia.Services.Power;

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
