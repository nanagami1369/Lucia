using System;
using System.Collections.Generic;
using System.Text;

namespace LuciaServer.Shared;

/// <summary>
/// 電源管理 ハブ
/// </summary>
public interface IPowerHub {

    /// <summary>
    /// シャットダウン
    /// </summary>
    Task Restart();

    /// <summary>
    /// 再起動
    /// </summary>
    Task Shutdown();

}
