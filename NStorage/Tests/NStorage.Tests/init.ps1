# initialize data for integration tests
# NOTE: if script is not running, please use next command in current Powershell session
# Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

$LargeTestDataFolder = Join-Path $PSScriptRoot "LargeTestData"
$SmallTestDataFolder = Join-Path $PSScriptRoot "SmallTestData"

function GenerateTestData {
    param(
        $dataFolderPath,
        $fileSizeInBytes,
        $fileCount
    )

    Remove-Item -Path $dataFolderPath -Recurse -Force -ErrorAction SilentlyContinue

    New-Item -Path $dataFolderPath -ItemType Directory | Out-Null

    for($i = 0; $i -le $fileCount; $i++) {
        $fileName = "$((New-Guid).ToString()).dat"
        $fileContent = Get-Random -Count ($fileSizeInBytes/32) -Minimum 0 -Maximum 100000
        $fileContent | Out-File (Join-Path $dataFolderPath $fileName)
    }
}

GenerateTestData $SmallTestDataFolder 1000 5000
GenerateTestData $LargeTestDataFolder 1000000 500
