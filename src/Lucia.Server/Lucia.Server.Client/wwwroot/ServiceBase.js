/**
 * ServiceBase.ts
 *
 * JavaScript側のサービスライフサイクルを管理する基底クラス
 * Custom Element（CleanupSentinel）を使用してDOM削除を自動検知し、
 * 確実にクリーンアップ処理を実行する
 *
 * 【設計の特徴】
 * - parentNode削除でも確実に動作（MutationObserverの欠点を解消）
 * - メモリリークを防止（DOM要素への参照を自動的に切断）
 * - CleanupSentinelを外部に公開しない（完全なカプセル化）
 *
 * 【使い方】
 * 1. このクラスを継承
 * 2. register() でイベントリスナーなどを登録
 * 3. unregister() でクリーンアップ処理を実装
 * 4. mount() を呼ぶとDOM削除時の自動クリーンアップが設定される
 */
// ============================================
// 内部実装（外部に公開しない）
// ============================================
const CLEANUP_SENTINEL_TAG = 'service-cleanup-sentinel';
/**
 * クリーンアップを自動実行するCustom Element
 *
 * このクラスは外部に公開されず、ServiceBase内でのみ使用される
 * disconnectedCallback はDOM削除時に必ず発火するため、
 * MutationObserverと異なりparentNode削除でも確実に動作する
 */
class CleanupSentinel extends HTMLElement {
    /** DOM削除時に実行するコールバック関数 */
    cleanupCallback;
    constructor() {
        super();
        // display:none だけでスクリーンリーダーからも除外される
        this.style.display = 'none';
    }
    /**
     * DOM から切り離されたときに自動的に呼ばれる
     *
     * 重要：このメソッドは以下のケースで確実に発火する
     * - 要素自体が削除された場合
     * - 親要素が削除された場合（MutationObserverでは検知できない）
     * - 祖先要素が削除された場合
     */
    disconnectedCallback() {
        if (this.cleanupCallback) {
            try {
                this.cleanupCallback();
            }
            catch (error) {
                // エラーが発生してもクリーンアップは継続
                console.error('[CleanupSentinel] Cleanup error:', error);
            }
            // コールバックを削除して二重実行を防止
            this.cleanupCallback = undefined;
        }
    }
}
// Custom Elementをグローバルに1回だけ登録
// モジュール読み込み時に実行される
if (!customElements.get(CLEANUP_SENTINEL_TAG)) {
    customElements.define(CLEANUP_SENTINEL_TAG, CleanupSentinel);
}
// ============================================
// 公開API
// ============================================
/**
 * JavaScript側のサービスライフサイクルを管理する基底クラス
 *
 * 【Blazorとの連携】
 * Blazorの DisposeAsync() 実行時は既にDOMが削除されているため、
 * JavaScript側で独自にクリーンアップ機構を持つ必要がある
 * このクラスはCustom Elementを使用してDOM削除を自動検知する
 */
export class ServiceBase {
    /** マウント対象のHTML要素 */
    target;
    /** クリーンアップ用のセンサー要素（CleanupSentinel） */
    currentSentinel;
    /**
     * サービスを要素にマウント
     *
     * この時点でイベントリスナーなどが登録され、
     * DOM削除時の自動クリーンアップが設定される
     *
     * @param target HTML要素 または セレクタ文字列 または null/undefined
     * @param args register()に渡す追加引数（例：DotNetObjectReference）
     *
     * @example
     * // HTML要素を直接指定
     * service.mount(buttonElement, dotnetRef);
     *
     * @example
     * // セレクタで指定
     * service.mount('#my-button', dotnetRef);
     */
    mount = (target, ...args) => {
        // 前回のmountが残っている場合はクリーンアップ
        // 同じインスタンスで複数回mountを呼んだ場合の対策
        if (this.currentSentinel) {
            this.cleanup();
        }
        // ターゲット要素の取得
        if (target instanceof HTMLElement) {
            this.target = target;
        }
        else if (typeof target === "string") {
            // セレクタ文字列の場合はquerySelector で検索
            this.target = document.querySelector(target);
        }
        else {
            this.target = undefined;
        }
        // ターゲットが見つからない場合は何もしない
        if (this.target == null) {
            return;
        }
        // 派生クラスの登録処理を実行
        // 失敗した場合は何もしない（センサーも配置しない）
        if (!this.register(this.target, ...args)) {
            return;
        }
        // クリーンアップセンサーを作成
        // document.createElement()の返り値はHTMLElementなので型アサーションが必要
        // 同一ファイル内なので CleanupSentinel 型が使える（exportは不要）
        const sentinel = document.createElement(CLEANUP_SENTINEL_TAG);
        // DOM削除時に実行するコールバックを設定
        // アロー関数を使用して this を束縛
        sentinel.cleanupCallback = () => {
            // 派生クラスのクリーンアップ処理を実行
            this.unregister();
            // 循環参照を切断（メモリリーク防止）
            this.target = undefined;
            this.currentSentinel = undefined;
        };
        // センサーをターゲット要素の子として追加
        // これにより、ターゲットが削除されるとセンサーも削除され、
        // disconnectedCallback が自動的に発火する
        this.target.appendChild(sentinel);
        // センサーへの参照を保持（複数回mount時のクリーンアップに使用）
        this.currentSentinel = sentinel;
    };
    /**
     * 内部クリーンアップ処理
     *
     * 複数回mount時や手動unmount時に使用
     * 通常はDOM削除で自動的に実行されるため、明示的に呼ぶ必要はない
     */
    cleanup() {
        // センサーを削除（disconnectedCallbackでクリーンアップ実行）
        if (this.currentSentinel) {
            this.currentSentinel.remove();
            this.currentSentinel = undefined;
        }
        // 念のための保険処理
        if (this.target) {
            this.unregister();
            this.target = undefined;
        }
    }
    /**
     * 公開API: 明示的なアンマウント
     *
     * 通常は不要（DOM削除で自動的に実行される）
     * 以下のケースでのみ使用する：
     * - 同じインスタンスを別の要素に再マウントする場合
     * - DOM削除前に手動でクリーンアップしたい場合
     *
     * 注意：Blazor側の DisposeAsync() では呼ばないこと
     * （その時点で既にDOMが削除されている）
     */
    unmount = () => {
        this.cleanup();
    };
}
//# sourceMappingURL=ServiceBase.js.map