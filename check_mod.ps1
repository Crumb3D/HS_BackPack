# Pre-boot check. Exit 1 if anything is wrong.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fail = 0

function Fail($msg) {
    Write-Host "FAIL  $msg" -ForegroundColor Red
    $script:fail++
}
function Ok($msg) { Write-Host "ok    $msg" -ForegroundColor Green }

foreach ($xml in @(
    "Config\XUi_InGame\windows.xml",
    "Config\items.xml",
    "Config\recipes.xml",
    "Config\progression.xml",
    "Config\loot.xml",
    "Config\traders.xml",
    "Config\ui_display.xml"
)) {
    $path = Join-Path $Root $xml
    if (-not (Test-Path $path)) { Fail "$xml missing"; continue }
    try { [xml](Get-Content $path -Raw) | Out-Null; Ok $xml } catch { Fail "$xml : $_" }
}

$windows = Get-Content (Join-Path $Root "Config\XUi_InGame\windows.xml") -Raw
if ($windows -notmatch 'rows="5"' -or $windows -notmatch 'cols="8"' -or $windows -notmatch 'backpackPager') { Fail "windowBackpack is not 8x5 pages" } else { Ok "8x5 pager" }
if ($windows -match 'scrollview') { Fail "backpack window still has a scrollview" } else { Ok "no scrollview" }
if ($windows -match 'pagerBox' -or $windows -match 'pos="175,-6"') { Fail "pager covering lock or slots" } else { Ok "pager in header gap" }
if ($windows -notmatch "append xpath=`"/windows/window\[@name='windowBackpack'\]/rect\[@name='header'\]`"") { Fail "pager not on the inventory header" } else { Ok "pager on inventory header" }

$items = Get-Content (Join-Path $Root "Config\items.xml") -Raw
foreach ($n in @("backpackSmall", "backpackMedium", "backpackLarge", "backpackSkillMagazine")) {
    if ($items -notmatch "item name=`"$n`"") { Fail "missing item $n" } else { Ok "item $n" }
}

$recipes = Get-Content (Join-Path $Root "Config\recipes.xml") -Raw
foreach ($n in @("backpackSmall", "backpackMedium", "backpackLarge")) {
    if ($recipes -notmatch "recipe name=`"$n`"") { Fail "missing recipe $n" } else { Ok "recipe $n" }
}

$prog = Get-Content (Join-Path $Root "Config\progression.xml") -Raw
if ($prog -notmatch 'crafting_skill name="craftingBackpack"') { Fail "missing craftingBackpack" } else { Ok "craftingBackpack" }

$loc = Join-Path $Root "Config\Localization.csv"
if (-not (Test-Path $loc)) { Fail "Localization.csv missing" }
else {
    $locTxt = Get-Content $loc -Raw
    foreach ($k in @("backpackSmall", "backpackMedium", "backpackLarge", "craftingBackpackName", "lblCategoryBackpack", "lblBackpack", "hsBackpackDropped", "hsBackpackNothingEquipped")) {
        if ($locTxt -notmatch "(?m)^$k,") { Fail "missing loc $k" } else { Ok "loc $k" }
    }
}

$dll = Join-Path $Root "HS_BackPack.dll"
$dll33 = Join-Path $Root "HS_BackPack_33.dll.off"
if (-not (Test-Path $dll)) { Fail "HS_BackPack.dll missing" }
else {
    $ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
    foreach ($t in @("HSBackPackMod", "HSBackpackPages", "HSBackpackSlot32", "HSPatchEquipmentCtor", "HSPatchBagResize32", "HSBackpackDrop", "MetaKey", "DropKey")) {
        if ($ascii.IndexOf($t) -lt 0) { Fail "dual DLL missing '$t'" } else { Ok "dual $t" }
    }
}
if (-not (Test-Path $dll33)) { Fail "HS_BackPack_33.dll.off missing" }
else {
    $ascii33 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll33))
    if ($ascii33.IndexOf("HSBackpackPages") -lt 0) { Fail "3.3 DLL missing paging" } else { Ok "3.3 paging" }
    if ($ascii33.IndexOf("HSBackpackDrop") -lt 0) { Fail "3.3 DLL missing drop" } else { Ok "3.3 drop" }
    if ($ascii33.IndexOf("HSBackpackSlot32") -ge 0) { Fail "3.3 DLL still has 3.2 slot code" } else { Ok "3.3 has no 3.2 slot code" }
}
$settings = Join-Path $Root "HSBackPackSettings.json"
if (-not (Test-Path $settings)) { Fail "HSBackPackSettings.json missing" }
else {
    $st = Get-Content $settings -Raw
    try { $st | ConvertFrom-Json | Out-Null; Ok "settings json" } catch { Fail "settings json: $_" }
    if ($st -notmatch '"DropKey"') { Fail "settings missing DropKey" } else { Ok "DropKey" }
}

powershell -NoProfile -File (Join-Path $Root "check_refs.ps1") -Dll $dll
if ($LASTEXITCODE -ne 0) { Fail "dual DLL game references missing on 3.2 or 3.3" } else { Ok "dual game references" }

if ($fail -gt 0) {
    Write-Host ("{0} check(s) failed." -f $fail) -ForegroundColor Red
    exit 1
}
Write-Host "All checks passed." -ForegroundColor Green
exit 0
