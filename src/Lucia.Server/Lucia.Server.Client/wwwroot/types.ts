/**
 * BlazorからJavaScriptに渡されたC#インスタンスの参照
 */
export declare type DotNetObject = {

    /**
     * C#メソッドを非同期的に呼び出します
     *
     * @template T メソッドの戻り値の型
     * @param methodIdentifier 呼び出すメソッド名（[JSInvokable]属性で指定した識別子）
     * @param args C#メソッドに渡す引数（JSON シリアライズ可能である必要があります）
     * @returns C#メソッドの戻り値をresolveするPromise
     *
     * @example
     * const result = await dotNetObject.invokeMethodAsync<string>('GetMessage', 'World');
     */
    invokeMethodAsync<T>(methodIdentifier: string, ...args: any[]): Promise<T>;

};
