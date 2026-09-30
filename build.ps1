# Compila o DiscordNodeStereo -> dist\DiscordNodeStereo.exe  (um .exe único, com o patch embutido)
#
# O patch de 512 kbps são DOIS arquivos que só funcionam juntos:
#   discord_voice.node  (pasta 512kbps)  +  index.js  (pasta Optional)
#
#   .\build.ps1                               usa ..\Standard\Standard.zip (os dois arquivos soltos no zip)
#   .\build.ps1 -Package C:\pasta-ou-zip      zip ou pasta com discord_voice.node e index.js
#   .\build.ps1 -Test                         compila e roda os testes antes
#
# Não precisa instalar nada: usa o csc.exe que já vem com o .NET Framework 4 do Windows.
param(
    [string]$Package = "..\Standard\Standard.zip",
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe do .NET Framework 4 não encontrado." }
New-Item -ItemType Directory -Force obj, dist | Out-Null
$common = '/nologo', '/codepage:65001', '/optimize+', '/platform:anycpu', '/warn:4'

if ($Test) {
    Write-Host "Testes:" -ForegroundColor Cyan
    & $csc @common /target:exe /out:obj\CoreTests.exe src\Core.cs tests\CoreTests.cs
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar os testes." }
    & .\obj\CoreTests.exe
    if ($LASTEXITCODE -ne 0) { throw "Testes falharam." }
}

# Junta os dois arquivos do patch em obj\patch
if (-not (Test-Path -LiteralPath $Package)) { throw "Pacote não encontrado: $Package  (use -Package <zip ou pasta>)" }
$stage = Join-Path $PSScriptRoot "obj\patch"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
$pkg = (Resolve-Path -LiteralPath $Package).Path
if ((Get-Item -LiteralPath $pkg).PSIsContainer) {
    Copy-Item -LiteralPath "$pkg\discord_voice.node", "$pkg\index.js" -Destination $stage
} else {
    Expand-Archive -LiteralPath $pkg -DestinationPath $stage -Force
}

$resources = @()
foreach ($name in "discord_voice.node", "index.js") {
    $file = Get-ChildItem -LiteralPath $stage -Recurse -File -Filter $name | Select-Object -First 1
    if (-not $file) { throw "O pacote não tem $name (precisa ter discord_voice.node e index.js)." }
    # compactado + tamanho/SHA-256 (o app compara sem precisar descompactar)
    $in = [IO.File]::OpenRead($file.FullName)
    $out = [IO.File]::Create("$PSScriptRoot\obj\$name.gz")
    $gz = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Optimal)
    $in.CopyTo($gz); $gz.Dispose(); $out.Dispose(); $in.Dispose()
    $info = "{0}`n{1}`n" -f $file.Length, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    [IO.File]::WriteAllText("$PSScriptRoot\obj\$name.info", $info)
    $resources += "/resource:obj\$name.gz,$name.gz", "/resource:obj\$name.info,$name.info"
    Write-Host ("  patch: {0,-20} {1,12:N0} bytes" -f $name, $file.Length)
}

& $csc @common /target:winexe /out:dist\DiscordNodeStereo.exe `
    /win32icon:assets\icon.ico /win32manifest:src\app.manifest `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /resource:assets\icon.ico,icon.ico @resources `
    src\Core.cs src\Settings.cs src\Ui.cs src\MainForm.cs src\RestartDialog.cs src\Program.cs
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o DiscordNodeStereo." }

$exe = Get-Item dist\DiscordNodeStereo.exe
Write-Host ("`nPronto: {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)) -ForegroundColor Green
