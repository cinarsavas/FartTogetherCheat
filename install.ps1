<#
  Fart Together Cheat - one click installer
  Run this with: right click -> "Run with PowerShell"
  (or from a terminal:  irm https://raw.githubusercontent.com/<user>/<repo>/main/install.ps1 | iex )
#>

$ErrorActionPreference = "Stop"

# ---- EDIT THESE TWO LINES after you create the GitHub repo ----------------
$GitHubUser = "cinarsavas"
$GitHubRepo = "FartTogetherCheat"
# ----------------------------------------------------------------------------

$GameFolderName = "Fart Together Demo"
$BepInExVersion = "5.4.23.2"
$BepInExZipName = "BepInEx_win_x64_$BepInExVersion.zip"
$BepInExUrl     = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/$BepInExZipName"

function Find-GamePath {
    $libraryFolders = @()
    $steamRegPaths = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"
    )
    foreach ($regPath in $steamRegPaths) {
        try {
            $steamPath = (Get-ItemProperty -Path $regPath -ErrorAction Stop).SteamPath
            if ($steamPath) { $libraryFolders += $steamPath }
        } catch {}
    }
    if ($libraryFolders.Count -eq 0) { $libraryFolders += "C:\Program Files (x86)\Steam" }

    $allLibraries = @()
    foreach ($steam in $libraryFolders) {
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        $allLibraries += (Join-Path $steam "steamapps\common")
        if (Test-Path $vdf) {
            $matches = Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"'
            foreach ($m in $matches) {
                $p = $m.Matches[0].Groups[1].Value -replace '\\\\', '\'
                $allLibraries += (Join-Path $p "steamapps\common")
            }
        }
    }

    foreach ($lib in $allLibraries) {
        $candidate = Join-Path $lib $GameFolderName
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

Write-Host "Fart Together Cheat installer" -ForegroundColor Cyan
Write-Host "Looking for the game..."

$gamePath = Find-GamePath
if (-not $gamePath) {
    $gamePath = Read-Host "Couldn't find it automatically. Paste the full path to the '$GameFolderName' folder"
}
if (-not (Test-Path $gamePath)) {
    Write-Error "That folder doesn't exist: $gamePath"
    exit 1
}
Write-Host "Game folder: $gamePath" -ForegroundColor Green

# ---- Step 1: BepInEx -------------------------------------------------------
$bepinexCore = Join-Path $gamePath "BepInEx\core\BepInEx.dll"
if (-not (Test-Path $bepinexCore)) {
    Write-Host "BepInEx not found, installing it..."
    $tmpZip = Join-Path $env:TEMP $BepInExZipName
    Invoke-WebRequest -Uri $BepInExUrl -OutFile $tmpZip
    Expand-Archive -Path $tmpZip -DestinationPath $gamePath -Force
    Remove-Item $tmpZip
    Write-Host "BepInEx installed. You need to run the game once now so it finishes setting up," -ForegroundColor Yellow
    Write-Host "then run this installer again to finish installing the cheat." -ForegroundColor Yellow
    Read-Host "Press Enter to launch the game now (close this window after it reaches the main menu)"
    Start-Process "steam://run/$(Get-Content (Join-Path $gamePath 'steam_appid.txt') -ErrorAction SilentlyContinue)"
    exit 0
}
Write-Host "BepInEx is already installed." -ForegroundColor Green

# ---- Step 2: the cheat plugin ----------------------------------------------
$pluginsDir = Join-Path $gamePath "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null

Write-Host "Downloading the latest FartTogetherCheat.dll from GitHub..."
$releaseApi = "https://api.github.com/repos/$GitHubUser/$GitHubRepo/releases/latest"
$release = Invoke-RestMethod -Uri $releaseApi -Headers @{ "User-Agent" = "FartTogetherCheat-Installer" }
$asset = $release.assets | Where-Object { $_.name -eq "FartTogetherCheat.dll" }
if (-not $asset) {
    Write-Error "No FartTogetherCheat.dll found in the latest release of $GitHubUser/$GitHubRepo."
    exit 1
}
$dllDest = Join-Path $pluginsDir "FartTogetherCheat.dll"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $dllDest
Write-Host "Installed $($release.tag_name) to:" -ForegroundColor Green
Write-Host "  $dllDest"

Write-Host ""
Write-Host "Done. Start the game, host a lobby, and click the tab on the left edge" -ForegroundColor Cyan
Write-Host "of the screen (or press F1 / Insert) to open the cheat menu."
Read-Host "Press Enter to close"
