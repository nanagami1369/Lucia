using Microsoft.AspNetCore.Components.Web;

namespace Lucia.Server.Client.Components.Modals;

/// <summary>
/// 確認ダイアログのオプション
/// </summary>
public class ConfirmOptions {

    /// <summary>確認種別</summary>
    public required ConfirmType Type { get; init; }

    /// <summary>タイトル（省略可）</summary>
    public string? Title { get; init; }

    /// <summary>本文メッセージ</summary>
    public required string Message { get; init; }

    /// <summary>OKボタンのラベル</summary>
    public required string OkMessage { get; init; }

    /// <summary>キャンセルボタンのラベル</summary>
    public required string CancelMessage { get; init; }

}
