param(
    [string] $PackageDirectory = "artifacts/packages",
    [string[]] $Frameworks = @("net6.0", "net7.0", "net8.0", "net9.0", "net10.0")
)

$packages = Get-ChildItem -Path $PackageDirectory -Filter "*.nupkg" -File
if ($packages.Count -eq 0) {
    throw "No .nupkg files found in '$PackageDirectory'."
}

foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entryNames = $archive.Entries | ForEach-Object { $_.FullName }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($framework in $Frameworks) {
        $prefix = "lib/$framework/"
        $hasFramework = $entryNames | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) } | Select-Object -First 1
        if (-not $hasFramework) {
            throw "Package '$($package.Name)' does not contain '$prefix'. NuGet will not show $framework as an included target framework."
        }
    }

    Write-Host "Package '$($package.Name)' contains: $($Frameworks -join ', ')"
}
