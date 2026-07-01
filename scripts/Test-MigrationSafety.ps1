param(
    [string]$MigrationsPath = "src/STOKIO.Infrastructure/Persistence/Migrations"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MigrationsPath)) {
    throw "Migration path '$MigrationsPath' was not found."
}

$blockedCallPatterns = @(
    "migrationBuilder\.DropTable\s*\(",
    "migrationBuilder\.DropColumn\s*\(",
    "migrationBuilder\.DeleteData\s*\(",
    "migrationBuilder\.RenameTable\s*\(",
    "migrationBuilder\.RenameColumn\s*\("
)

$blockedSqlPattern = "(?i)\b(TRUNCATE\s+TABLE|DELETE\s+FROM|DROP\s+(TABLE|COLUMN|DATABASE|SCHEMA)|ALTER\s+TABLE\s+.+\s+DROP\s+COLUMN)\b"
$findings = New-Object System.Collections.Generic.List[string]

function Get-UpMethodLines {
    param([string[]]$Lines)

    $startIndex = -1
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($Lines[$i] -match "protected\s+override\s+void\s+Up\s*\(") {
            $startIndex = $i
            break
        }
    }

    if ($startIndex -lt 0) {
        return @()
    }

    $methodLines = New-Object System.Collections.Generic.List[object]
    $braceDepth = 0
    $started = $false

    for ($i = $startIndex; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        $opens = ([regex]::Matches($line, "\{")).Count
        $closes = ([regex]::Matches($line, "\}")).Count

        if ($opens -gt 0) {
            $started = $true
        }

        if ($started) {
            $methodLines.Add([pscustomobject]@{
                Number = $i + 1
                Text = $line
            })
        }

        $braceDepth += $opens - $closes
        if ($started -and $braceDepth -eq 0) {
            break
        }
    }

    return $methodLines
}

Get-ChildItem -LiteralPath $MigrationsPath -Filter "*.cs" |
    Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -ne "StokioDbContextModelSnapshot.cs" } |
    Sort-Object Name |
    ForEach-Object {
        $file = $_
        $lines = Get-Content -LiteralPath $file.FullName
        $upLines = Get-UpMethodLines -Lines $lines

        foreach ($entry in $upLines) {
            foreach ($pattern in $blockedCallPatterns) {
                if ($entry.Text -match $pattern) {
                    $findings.Add("$($file.FullName):$($entry.Number): $($entry.Text.Trim())")
                }
            }

            if ($entry.Text -match $blockedSqlPattern) {
                $findings.Add("$($file.FullName):$($entry.Number): $($entry.Text.Trim())")
            }
        }
    }

if ($findings.Count -gt 0) {
    Write-Error @"
Potentially destructive migration changes were found in Up() methods.
Create an explicit reviewed migration strategy before merging:
$($findings -join [Environment]::NewLine)
"@
}

Write-Host "Migration safety check passed: no destructive Up() operations were found."
