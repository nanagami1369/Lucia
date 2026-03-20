using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Lucia.Installer.Installers;

/// <summary>
/// Windows Service の作成・削除を行うクラス。
/// Service Control Manager API（advapi32.dll）を P/Invoke で直接呼び出す。
/// </summary>
public static class ServiceInstaller {
    /// <summary>サービスの表示名。</summary>
    public const string ServiceDisplayName = "Lucia Session Monitor";

    private const uint ScManagerCreateService = 0x0002;
    private const uint ScManagerAllAccess = 0xF003F;
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceAutoStart = 0x00000002;
    private const uint ServiceErrorNormal = 0x00000001;
    private const uint ServiceAllAccess = 0xF01FF;
    private const uint DeleteAccess = 0x00010000;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(
        string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateService(
        IntPtr hScManager,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(
        IntPtr hScManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(IntPtr hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hObject);

    /// <summary>
    /// Windows Service を新規作成して起動タイプを自動（Automatic）に設定する。
    /// </summary>
    /// <param name="serviceName">登録するサービス名。</param>
    /// <param name="binaryPathName">サービスの実行ファイルパスと引数（例: "C:\path\app.exe --flag"）。</param>
    /// <exception cref="Win32Exception">SCM API の呼び出しに失敗した場合にスローされる。</exception>
    public static void Create(string serviceName, string binaryPathName) {
        var hScManager = OpenSCManager(null, null, ScManagerCreateService | ScManagerAllAccess);
        if (hScManager == IntPtr.Zero) {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Service Control Manager を開けませんでした。");
        }

        try {
            var hService = CreateService(
                hScManager,
                serviceName,
                ServiceDisplayName,
                ServiceAllAccess,
                ServiceWin32OwnProcess,
                ServiceAutoStart,
                ServiceErrorNormal,
                binaryPathName,
                null,
                IntPtr.Zero,
                null,
                null,
                null);

            if (hService == IntPtr.Zero)
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(), $"サービス '{serviceName}' の作成に失敗しました。");

            CloseServiceHandle(hService);
        } finally {
            CloseServiceHandle(hScManager);
        }
    }

    /// <summary>
    /// 停止済みの Windows Service をサービス制御データベースから削除する。
    /// </summary>
    /// <param name="serviceName">削除するサービス名。</param>
    /// <exception cref="Win32Exception">SCM API の呼び出しに失敗した場合にスローされる。</exception>
    public static void Delete(string serviceName) {
        var hScManager = OpenSCManager(null, null, ScManagerAllAccess);
        if (hScManager == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Service Control Manager を開けませんでした。");

        try {
            var hService = OpenService(hScManager, serviceName, DeleteAccess);
            if (hService == IntPtr.Zero)
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(), $"サービス '{serviceName}' を開けませんでした。");

            try {
                if (!DeleteService(hService))
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(), $"サービス '{serviceName}' の削除に失敗しました。");
            } finally {
                CloseServiceHandle(hService);
            }
        } finally {
            CloseServiceHandle(hScManager);
        }
    }

    /// <summary>
    /// 指定名のサービスが存在すれば <see cref="ServiceController"/> を返す。存在しない場合は null。
    /// </summary>
    /// <param name="serviceName">検索するサービス名。</param>
    /// <returns>サービスが存在する場合は <see cref="ServiceController"/>、存在しない場合は null。</returns>
    public static ServiceController? FindService(string serviceName)
        => ServiceController.GetServices()
            .FirstOrDefault(service => service.ServiceName == serviceName);
}
