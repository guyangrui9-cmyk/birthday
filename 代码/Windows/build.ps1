$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assets = Join-Path $root '图像素材'
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $root '我们的小屋.exe'
$room = Join-Path $assets '房间/room-game.png'
$map = Join-Path $assets '房间/map-game.png'
$player = Join-Path $assets '人物/player-game.png'
$companion = Join-Path $assets '人物/companion-game.png'
$outfitFiles = Get-ChildItem -LiteralPath (Join-Path $assets '人物/游戏装扮') -Filter '*.png' | Sort-Object Name
$outfits = $outfitFiles | ForEach-Object { "/resource:$($_.FullName),outfit/$($_.Name)" }
& $csc /nologo /target:winexe "/out:$exe" "/resource:$(Join-Path $root '代码/walk-profiles.csv'),walk-profiles.csv" "/resource:$room,room.png" "/resource:$map,map.png" "/resource:$player,player.png" "/resource:$companion,companion.png" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @outfits (Join-Path $PSScriptRoot 'Game.cs') (Join-Path $PSScriptRoot 'Wardrobe.cs') (Join-Path $PSScriptRoot 'WalkMotion.cs')
if ($LASTEXITCODE -ne 0) { throw 'Game compilation failed.' }
Get-Item -LiteralPath $exe
