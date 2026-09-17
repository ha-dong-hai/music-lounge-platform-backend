"""MLACP-431: xac thuc va chan SSRF cho dich vu ghep anh.

Truoc day POST /stitch khong co xac thuc va requests.get() voi BAT KY URL nao, lai tu di theo redirect. Rao chan SSRF chi
nam o backend .NET, nen goi thang vao dich vu nay la vuot qua: bat server tai dia chi noi bo, endpoint metadata cua cloud
(169.254.169.254), hoac lam te liet CPU bang cac lan ghep gia. Phai chan truoc khi trien khai cong khai len Azure Container
Apps.

Khong tai anh that va khong can Hugin/torch: requests.get bi thay bang ban ghi lai.
"""

import pytest
from fastapi.testclient import TestClient

import main

client = TestClient(main.app)

KHOA = "khoa-bi-mat-kiem-thu"
NGUON = "https://musiclounge-api.azurewebsites.net"
HOP_LE = [f"{NGUON}/uploads/a.jpg", f"{NGUON}/uploads/b.jpg"]


@pytest.fixture(autouse=True)
def cau_hinh_day_du(monkeypatch):
    monkeypatch.setenv("STITCHER_API_KEY", KHOA)
    monkeypatch.setenv("ALLOWED_IMAGE_ORIGINS", NGUON)


@pytest.fixture
def lan_tai(monkeypatch):
    """Ghi lai moi lan dich vu dinh tai anh — test chan phai chung minh KHONG co lan tai nao."""
    calls = []

    def tai_gia(url, **kwargs):
        calls.append((url, kwargs))
        raise RuntimeError("test khong duoc tai anh that")

    monkeypatch.setattr(main.requests, "get", tai_gia)
    return calls


def _goi(urls, khoa=KHOA):
    headers = {} if khoa is None else {"X-Stitcher-Key": khoa}
    return client.post("/stitch", json={"image_urls": urls}, headers=headers)


def test_thieu_khoa_bi_tu_choi_va_khong_tai_gi(lan_tai):
    assert _goi(HOP_LE, khoa=None).status_code == 401
    assert lan_tai == []


def test_sai_khoa_bi_tu_choi_va_khong_tai_gi(lan_tai):
    assert _goi(HOP_LE, khoa="doan-mo").status_code == 401
    assert lan_tai == []


def test_dich_vu_chua_cau_hinh_khoa_thi_tu_choi_het(monkeypatch, lan_tai):
    # Lo trien khai thieu bien moi truong cung KHONG duoc thanh cong vien mo — ke ca khi gui header rong.
    monkeypatch.delenv("STITCHER_API_KEY")
    assert _goi(HOP_LE, khoa="").status_code == 503
    assert lan_tai == []


def test_chua_cau_hinh_danh_sach_nguon_thi_tu_choi_het(monkeypatch, lan_tai):
    monkeypatch.delenv("ALLOWED_IMAGE_ORIGINS")
    assert _goi(HOP_LE).status_code == 503
    assert lan_tai == []


@pytest.mark.parametrize(
    "url",
    [
        "http://169.254.169.254/latest/meta-data/",  # metadata cua cloud
        "http://localhost:8000/health",  # chinh no / mang noi bo
        "https://evil.example.com/a.jpg",
        "https://musiclounge-api.azurewebsites.net.evil.com/a.jpg",  # gia dang duoi ten mien
        "https://musiclounge-api.azurewebsites.net@evil.com/a.jpg",  # giau host that sau dau @
        "http://musiclounge-api.azurewebsites.net/uploads/a.jpg",  # dung host, sai giao thuc
        "https://musiclounge-api.azurewebsites.net:8443/uploads/a.jpg",  # dung host, sai cong
        "file:///etc/passwd",
    ],
)
def test_url_ngoai_danh_sach_bi_chan_truoc_khi_tai_bat_ky_anh_nao(url, lan_tai):
    # URL xau dat O SAU mot URL hop le: phai kiem HET roi moi tai, khong duoc tai xong anh dau moi phat hien.
    assert _goi([HOP_LE[0], url]).status_code == 400
    assert lan_tai == []


def test_url_hop_le_moi_duoc_tai_va_khong_di_theo_redirect(lan_tai):
    _goi(HOP_LE)

    assert lan_tai, "URL hợp lệ phải được tải"
    url, kwargs = lan_tai[0]
    assert url == HOP_LE[0]
    # Mot host hop le van co the redirect sang dia chi noi bo — khong duoc di theo.
    assert kwargs.get("allow_redirects") is False


def test_health_van_mo_de_container_apps_kiem_tra_va_backend_danh_thuc():
    assert client.get("/health").status_code == 200
