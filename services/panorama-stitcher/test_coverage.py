"""MLACP-438: tu choi bo anh chua xoay du mot vong.

Du lieu yaw/hfov ben duoi la SO DO THAT (17/09/2026): chay _check_connectivity + _optimize_geometry cua chinh file
main.py voi Hugin cuc bo tren bo anh co dap an bo-4-quan-bar-toi-hfov65 (9 anh, moi anh lech 40 do, HFOV 65 do).
Khong can Hugin/torch de chay cac test nay.
"""

import numpy as np
import pytest
from fastapi.testclient import TestClient

import main

DU_VONG = [(-120.00, 64.82), (-80.07, 64.82), (-40.12, 64.82), (-0.45, 64.82), (39.46, 64.82),
           (79.39, 64.82), (119.10, 64.82), (158.85, 64.82), (-161.14, 64.82)]
NUA_VONG = [(-60.00, 64.63), (-20.30, 64.63), (19.47, 64.63), (59.29, 64.63), (98.92, 64.63)]


def _geoms(cap):
    return [main._ImageGeometry(yaw=yaw, pitch=0.0, hfov=hfov, vfov=hfov * 0.75) for yaw, hfov in cap]


def test_bo_du_vong_do_that_phu_360_do():
    assert main._horizontal_coverage_deg(_geoms(DU_VONG)) == pytest.approx(360.0)


def test_bo_nua_vong_do_that_phu_khoang_224_do():
    # Dap an 225 do (4 buoc 40 do + 65 do); Hugin uoc luong HFOV 64.63.
    assert main._horizontal_coverage_deg(_geoms(NUA_VONG)) == pytest.approx(223.6, abs=0.1)


def test_hai_cung_vat_qua_moc_180_do_tinh_dung_tren_vong_tron():
    # yaw 170 va -170, moi anh 30 do: phu 155..185 va 175..205 -> hop 155..205 = 50 do.
    # Cach tinh ngay tho max - min tren -180..180 se ra ~370.
    assert main._horizontal_coverage_deg(_geoms([(170.0, 30.0), (-170.0, 30.0)])) == pytest.approx(50.0)


def test_cac_cung_roi_nhau_cong_don():
    assert main._horizontal_coverage_deg(_geoms([(0.0, 40.0), (180.0, 40.0)])) == pytest.approx(80.0)


def test_mot_anh_rong_tu_360_do_tro_len_la_du_vong():
    assert main._horizontal_coverage_deg(_geoms([(0.0, 360.0)])) == 360.0


KHOA = "khoa-kiem-thu"
NGUON = "https://musiclounge-api.azurewebsites.net"


@pytest.fixture
def dich_vu(monkeypatch):
    """/stitch voi moi buoc nang (tai anh, Hugin, OpenCV) thay bang ban ghi lai."""
    monkeypatch.setenv("STITCHER_API_KEY", KHOA)
    monkeypatch.setenv("ALLOWED_IMAGE_ORIGINS", NGUON)
    monkeypatch.setattr(main, "_download_and_correct_orientation", lambda url: np.zeros((120, 160, 3), np.uint8))
    monkeypatch.setattr(main, "_check_connectivity", lambda work_dir, paths: [])
    goi_ghep = []

    def ghep_gia(images):
        goi_ghep.append(len(images))
        return True, np.zeros((200, 2000, 3), np.uint8), None

    monkeypatch.setattr(main, "_stitch_with_opencv", ghep_gia)
    return goi_ghep


def _goi():
    return TestClient(main.app).post(
        "/stitch", headers={"X-Stitcher-Key": KHOA},
        json={"image_urls": [f"{NGUON}/uploads/{i}.jpg" for i in range(5)]})


def test_bo_nua_vong_bi_tu_choi_422_truoc_khi_ghep_opencv(dich_vu, monkeypatch):
    monkeypatch.setattr(main, "_optimize_geometry", lambda work_dir: _geoms(NUA_VONG))

    res = _goi()

    assert res.status_code == 422
    assert "224°" in res.json()["detail"] and "đủ một vòng" in res.json()["detail"]
    assert dich_vu == [], "bộ ảnh bị từ chối không được tốn CPU ghép OpenCV"


def test_bo_du_vong_duoc_ghep(dich_vu, monkeypatch):
    monkeypatch.setattr(main, "_optimize_geometry", lambda work_dir: _geoms(DU_VONG))

    res = _goi()

    assert res.status_code == 200, res.text
    assert res.headers["content-type"] == "image/jpeg"
    assert dich_vu == [5]


def test_khong_co_hinh_hoc_tin_cay_thi_bo_qua_kiem_vong_nhu_truoc(dich_vu, monkeypatch):
    # _optimize_geometry tra None khi roll bat thuong: khong co du lieu dang tin de kiem, giu hanh vi cu.
    monkeypatch.setattr(main, "_optimize_geometry", lambda work_dir: None)

    res = _goi()

    assert res.status_code == 200, res.text
    assert dich_vu == [5]
