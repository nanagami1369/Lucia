param([string]$Action)

# 管理者権限チェック・昇格
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Definition
    Start-Process PowerShell -ArgumentList "-NoExit -File `"$scriptPath`" -Action $Action" -Verb RunAs
    exit
}

$ServiceName = 'LuciaServer'
$ExePath = Join-Path $PSScriptRoot 'Lucia.Server.exe'
$Port = 6100
$LogFile = Join-Path $PSScriptRoot 'Installer.log'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logMsg = "[$timestamp] [$Level] $Message"
    Add-Content -Path $LogFile -Value $logMsg -Encoding UTF8
    Write-Host $logMsg
}

switch ($Action) {
    'install' {
        Write-Log '=== インストール開始 ===' 'INFO'

        Write-Log "ファイル確認: $ExePath" 'INFO'
        if (-not (Test-Path $ExePath)) {
            Write-Log 'エラー: exeが見つかりません' 'ERROR'
            exit 1
        }

        Write-Log "サービス '$ServiceName' を作成" 'INFO'
        if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
            Write-Log 'エラー: サービスは既に登録されています' 'ERROR'
            exit 1
        }

        New-Service -Name $ServiceName `
            -BinaryPathName "`"$ExePath`" --urls http://0.0.0.0:$Port" `
            -StartupType Automatic `
            -DisplayName 'Lucia Session Monitor' | Out-Null

        Write-Log 'サービスを起動' 'INFO'
        Start-Service -Name $ServiceName

        Write-Log "ファイアウォール: ポート$Port を開放 (192.168.0.0/16, プライベートプロファイルのみ)" 'INFO'
        Remove-NetFirewallRule -DisplayName $ServiceName -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName $ServiceName `
            -Direction Inbound -Action Allow -Protocol TCP `
            -LocalPort $Port -Program $ExePath `
            -RemoteAddress '192.168.0.0/16' `
            -Profile Private -ErrorAction SilentlyContinue | Out-Null

        Write-Log "✓ インストール完了 (ログ: $LogFile)" 'SUCCESS'
        Read-Host 'キーを押して終了'
    }

    'uninstall' {
        Write-Log '=== アンインストール開始 ===' 'INFO'

        if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
            Write-Log '警告: サービスが見つかりません' 'WARNING'
            exit 1
        }

        Write-Log "サービス '$ServiceName' を停止・削除" 'INFO'
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        & sc.exe delete $ServiceName | Out-Null

        Write-Log 'ファイアウォール: ルールを削除' 'INFO'
        Remove-NetFirewallRule -DisplayName $ServiceName -ErrorAction SilentlyContinue

        Write-Log "✓ アンインストール完了 (ログ: $LogFile)" 'SUCCESS'
        Read-Host 'キーを押して終了'
    }

    default {
        Write-Host '使用方法: .\Installer.ps1 -Action install|uninstall'
        exit 1
    }
}
