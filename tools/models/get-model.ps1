# Downloads the chat model into models\ at the repo root (gitignored). The build copies it next to the exe
# (bin\...\models\) and the publish task into publish\models\.
#
#   powershell -ExecutionPolicy Bypass -File tools\models\get-model.ps1
#
# Qwen3.5-2B, quantized to 6 bits (Q6_K, 1.5 GB) by unsloth: https://huggingface.co/unsloth/Qwen3.5-2B-GGUF
# Another quant works too: download it into models\ and set Chat.ModelFile in settings.json.

$ErrorActionPreference = 'Stop'
$name = 'Qwen3.5-2B-Q6_K.gguf'
$url = "https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/$name"
$sha256 = 'fc90339420b4298887aafb307a4291c55440b730133bbffe6ba9630503dcb548'

$folder = Join-Path $PSScriptRoot '..\..\models'
New-Item -ItemType Directory -Force $folder | Out-Null
$target = Join-Path (Resolve-Path $folder) $name

if ((Test-Path $target) -and (Get-FileHash $target -Algorithm SHA256).Hash -eq $sha256) {
    Write-Host "$name is already there."
    exit 0
}

Write-Host "Downloading $name (1.5 GB)..."
$part = "$target.part"
& curl.exe -L --fail --retry 3 -C - -o $part $url
if ($LASTEXITCODE -ne 0) { throw "Download failed (curl exit code $LASTEXITCODE)." }

$hash = (Get-FileHash $part -Algorithm SHA256).Hash
if ($hash -ne $sha256) {
    Remove-Item $part
    throw "The download is damaged (SHA-256 $hash). Run the script again."
}

Move-Item -Force $part $target
Write-Host "Done: $target"
