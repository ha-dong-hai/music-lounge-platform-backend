<#
.SYNOPSIS
    MLACP-324. Sinh hoac xoa bo du lieu trinh dien cho cac nhanh AI.

.DESCRIPTION
    Du lieu AI tren moi truong that qua mong de trinh dien: moi nhanh cua he goi y deu doi du lieu,
    nen nhin vao se tuong AI khong lam gi. Script nay dung du lieu DAU VAO (buoi dien, so thich tu
    khai, ve, luot luu quan tam, nhat ky hanh vi) roi chay dung job tinh diem cua he thong — diem so
    va goi y la do he thong tu tinh, khong phai do script viet thang vao bang.

    Co y KHONG lam thanh endpoint API: mot endpoint sinh du lieu gia ma lo chay tren moi truong that
    thi khong go lai duoc.

    Moi dong sinh ra deu mang dau nhan biet (buoi dien bat dau bang "[DEMO] ", tai khoan co duoi
    @demo.musiclounge.test) nen -Clean xoa lai duoc sach.

.PARAMETER ConnectionString
    Chuoi ket noi toi database dich. Khong co gia tri mac dinh — phai go ro.

.PARAMETER Clean
    Xoa toan bo du lieu demo thay vi sinh.

.EXAMPLE
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..."
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..." -Clean
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/MusicLounge.Tests.Integration/MusicLounge.Tests.Integration.csproj'

if (-not (Test-Path $project)) {
    throw "Khong tim thay du an test tai $project"
}

$target = if ($Clean) { 'Clean' } else { 'Seed' }

if ($Clean) {
    Write-Host "Se XOA toan bo du lieu demo khoi database da chi dinh." -ForegroundColor Yellow
} else {
    Write-Host "Se SINH du lieu demo vao database da chi dinh." -ForegroundColor Yellow
}
$answer = Read-Host "Go 'yes' de tiep tuc"
if ($answer -ne 'yes') {
    Write-Host 'Da huy.'
    return
}

$env:DEMO_SEED_CONNECTION = $ConnectionString
$env:DEMO_SEED_CONFIRM = 'yes-seed-demo-data'

try {
    dotnet test $project --filter "FullyQualifiedName~DemoDataScript.$target" --logger 'console;verbosity=detailed'
}
finally {
    # Khong de chuoi ket noi va cau xac nhan nam lai trong phien lam viec.
    Remove-Item Env:DEMO_SEED_CONNECTION -ErrorAction SilentlyContinue
    Remove-Item Env:DEMO_SEED_CONFIRM -ErrorAction SilentlyContinue
}
