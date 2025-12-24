$csFiles = Get-ChildItem -Recurse -Filter *.cs
$totalLines = 0
$totalCharsNoSpaces = 0
$totalFiles = 0

foreach ($file in $csFiles) {
    if ($file.FullName -match '[\\/](bin|obj)[\\/]') { 
        continue
    } 

    $totalFiles++
    $content = Get-Content $file.FullName -Raw
    $lines = $content -split "`r?`n"
    $totalLines += $lines.Count
    $charsNoSpaces = ($content -replace "\s", "").Length
    $totalCharsNoSpaces += $charsNoSpaces
}

Write-Output "Всего файлов: $totalFiles"
Write-Output "Всего строк: $totalLines"
Write-Output "Всего символов без пробелов: $totalCharsNoSpaces"
