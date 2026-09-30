# Compila o DiscordNodeStereo -> dist\DiscordNodeStereo.exe  (um .exe único, com o .node embutido)
#
#   .\build.ps1                         usa ..\Standard\512kbps\discord_voice.node
#   .\build.ps1 -Node C:\outro.node     usa outro arquivo
#   .\build.ps1 -Test                   compila e roda os testes antes
#
# Não precisa instalar nada: usa o csc.exe que já vem com o .NET Framework 4 do Windows.
param(
    [string]$Node = "..\Standard\512kbps\discord_voice.node",
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

if (-not (Test-Path $Node)) { throw "Arquivo .node não encontrado: $Node  (use -Node <caminho>)" }
$nodePath = (Resolve-Path $Node).Path

# .node compactado + tamanho/SHA-256 (o DiscordNodeStereo compara sem precisar descompactar)
$in = [IO.File]::OpenRead($nodePath)
$out = [IO.File]::Create("$PSScriptRoot\obj\discord_voice.node.gz")
$gz = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Optimal)
$in.CopyTo($gz); $gz.Dispose(); $out.Dispose(); $in.Dispose()
$info = "{0}`n{1}`n" -f (Get-Item $nodePath).Length, (Get-FileHash $nodePath -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$PSScriptRoot\obj\discord_voice.node.info", $info)

& $csc @common /target:winexe /out:dist\DiscordNodeStereo.exe `
    /win32icon:assets\icon.ico /win32manifest:src\app.manifest `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /resource:assets\icon.ico,icon.ico `
    /resource:obj\discord_voice.node.gz,discord_voice.node.gz `
    /resource:obj\discord_voice.node.info,discord_voice.node.info `
    src\Core.cs src\Settings.cs src\Ui.cs src\MainForm.cs src\RestartDialog.cs src\Program.cs
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o DiscordNodeStereo." }

$exe = Get-Item dist\DiscordNodeStereo.exe
Write-Host ("`nPronto: {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)) -ForegroundColor Green
