namespace Lucia.Server.Client.Components.Modals;

public interface IModalContext {

    /// <summary>
    /// キャンセル
    /// </summary>
    public Task Cancel();

    /// <summary>
    /// 閉じる
    /// </summary>
    public Task Close();

}
