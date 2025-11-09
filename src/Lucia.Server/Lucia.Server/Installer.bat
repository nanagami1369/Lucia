@echo off
setlocal

:: 固定値
set "ServiceName=LuciaServer"
set "ExePath=%~dp0Lucia.Server.exe"
set "Port=6100"

:: 引数チェック（install または uninstall）
if "%~1"=="" (
    echo 使用方法: %~nx0 ^[install^|uninstall^]
    exit /b 1
)

if /i "%~1"=="install" (
    echo サービスを登録中...
    sc.exe create "%ServiceName%" binPath= "\"%ExePath%\" --urls http://0.0.0.0:%Port%" start= auto >nul
    netsh advfirewall firewall add rule name="%ServiceName%" dir=in action=allow program="%ExePath%" protocol=TCP localport=%Port% remoteip=192.168.0.0/16 profile=private >nul
    echo サービス '%ServiceName%' を登録し、ポート %Port% を開放しました。
    exit /b 0
)

if /i "%~1"=="uninstall" (
    echo サービスを削除中...
    sc.exe stop "%ServiceName%" >nul
    sc.exe delete "%ServiceName%" >nul
    netsh advfirewall firewall delete rule name="%ServiceName%" >nul
    echo サービス '%ServiceName%' を削除しました。
    exit /b 0
)

echo ? 不正な引数です: %~1
exit /b 1
