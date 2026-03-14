namespace Lucia.Server.Client.Components.Modals;

/// <summary>
/// 確認ダイアログの種別
/// </summary>
public enum ConfirmType {
    /// <summary>情報</summary>
    Info,
    /// <summary>警告</summary>
    Warm,
    /// <summary>危険</summary>
    Danger
}

/// <summary>
/// <see cref="ConfirmType"/> の拡張メソッド
/// </summary>
public static class ConfirmTypeExtensions {

    /// <summary>
    /// タイトルなしの <see cref="ConfirmOptions"/> を生成する
    /// </summary>
    /// <param name="type">確認種別</param>
    /// <param name="message">本文メッセージ</param>
    /// <param name="okMessage">OKボタンのラベル</param>
    /// <param name="cancelMessage">キャンセルボタンのラベル</param>
    public static ConfirmOptions Create(this ConfirmType type, string message, string okMessage, string cancelMessage) {
        return new ConfirmOptions {
            Type = type, Message = message, OkMessage = okMessage, CancelMessage = cancelMessage
        };
    }

    /// <summary>
    /// タイトルありの <see cref="ConfirmOptions"/> を生成する
    /// </summary>
    /// <param name="type">確認種別</param>
    /// <param name="title">タイトル</param>
    /// <param name="message">本文メッセージ</param>
    /// <param name="okMessage">OKボタンのラベル</param>
    /// <param name="cancelMessage">キャンセルボタンのラベル</param>
    public static ConfirmOptions Create(this ConfirmType type, string title, string message, string okMessage, string cancelMessage) {
        return new ConfirmOptions {
            Type = type, Title = title, Message = message, OkMessage = okMessage, CancelMessage = cancelMessage
        };
    }
}
