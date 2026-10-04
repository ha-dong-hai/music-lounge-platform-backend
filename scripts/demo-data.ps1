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

.PARAMETER Sample
    MLACP-577. Dung bo DU LIEU MAU da dang cho ca san thay vi bo demo AI: ten nhu that (khong co tien to "[DEMO] "),
    ve mua qua dung API nen co thanh toan + so cai + lich quyet toan do he thong tinh, co buoi da dien va danh gia.
    Dau nhan biet de don nam o cho nguoi xem khong thay (tai khoan danh dau + duoi email @mau.musiclounge.test).
    Dung kem -Clean de xoa dung bo nay. Hai bo doc lap: co the co ca hai, va don tung bo rieng.

.EXAMPLE
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..."
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..." -Clean
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..." -Sample
    ./scripts/demo-data.ps1 -ConnectionString "Server=...;Database=SU26SE039;..." -Sample -Clean
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [switch]$Clean,

    [switch]$Sample
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/MusicLounge.Tests.Integration/MusicLounge.Tests.Integration.csproj'

if (-not (Test-Path $project)) {
    throw "Khong tim thay du an test tai $project"
}

$target = if ($Clean) { 'Clean' } else { 'Seed' }
# Hai bo dung hai lop rieng va hai cap bien moi truong rieng (cau xac nhan khac nhau) de khong chay nham bo nay thay bo kia.
$script = if ($Sample) { 'SampleDataScript' } else { 'DemoDataScript' }
$prefix = if ($Sample) { 'SAMPLE_SEED' } else { 'DEMO_SEED' }
$phrase = if ($Sample) { 'yes-seed-sample-data' } else { 'yes-seed-demo-data' }
$ten = if ($Sample) { 'du lieu MAU (MLACP-577)' } else { 'du lieu demo' }

if ($Clean) {
    Write-Host "Se XOA toan bo $ten khoi database da chi dinh." -ForegroundColor Yellow
} else {
    Write-Host "Se SINH $ten vao database da chi dinh." -ForegroundColor Yellow
}
$answer = Read-Host "Go 'yes' de tiep tuc"
if ($answer -ne 'yes') {
    Write-Host 'Da huy.'
    return
}

Set-Item "Env:${prefix}_CONNECTION" $ConnectionString
Set-Item "Env:${prefix}_CONFIRM" $phrase

try {
    dotnet test $project --filter "FullyQualifiedName~$script.$target" --logger 'console;verbosity=detailed'
}
finally {
    # Khong de chuoi ket noi va cau xac nhan nam lai trong phien lam viec.
    Remove-Item "Env:${prefix}_CONNECTION" -ErrorAction SilentlyContinue
    Remove-Item "Env:${prefix}_CONFIRM" -ErrorAction SilentlyContinue
}
