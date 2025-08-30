$csFiles = Get-ChildItem -Recurse -Filter *.cs
$totalLines = 0
$totalCharsNoSpaces = 0

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $lines = $content -split "`r?`n"
    $totalLines += $lines.Count
    $charsNoSpaces = ($content -replace "\s", "").Length
    $totalCharsNoSpaces += $charsNoSpaces
}

Write-Output "Всего строк: $totalLines"
Write-Output "Всего символов без пробелов: $totalCharsNoSpaces"
