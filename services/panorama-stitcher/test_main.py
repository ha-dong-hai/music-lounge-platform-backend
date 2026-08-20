"""Tests for the LoFTR rescue path added to main.py's connectivity check.

Deliberately does NOT touch torch/kornia/Hugin - main.py imports those lazily inside functions
(see _get_loftr_matcher), so importing main.py here doesn't require them, and _loftr_match_pair
is monkeypatched with canned results everywhere it matters. This is the one genuinely mockable
piece of logic in the file; everything else in main.py shells out to real Hugin binaries or cv2.
"""

import os

import main


def _write_fake_pto(path: str, image_lines: list[str], control_point_lines: list[str] = ()) -> None:
    with open(path, "w", encoding="utf-8") as f:
        f.writelines(image_lines)
        f.writelines(control_point_lines)


# ---------------------------------------------------------------------------
# _append_control_points
# ---------------------------------------------------------------------------


def test_append_control_points_inserts_after_last_c_line(tmp_path):
    pto = tmp_path / "project.pto"
    _write_fake_pto(
        pto,
        image_lines=["p f2 w4000 h2000 v360 n\"TIFF_m\"\n", "i w1200 h1600 f0 v90 n\"in0.jpg\"\n",
                     "i w1200 h1600 f0 v90 n\"in1.jpg\"\n"],
        control_point_lines=["c n0 N1 x100.00 y200.00 X110.00 Y210.00 t0\n"],
    )

    main._append_control_points(str(pto), 0, 2, [(1.0, 2.0, 3.0, 4.0, 0.9)])

    lines = pto.read_text(encoding="utf-8").splitlines()
    c_lines = [l for l in lines if l.startswith("c ")]
    assert c_lines == [
        "c n0 N1 x100.00 y200.00 X110.00 Y210.00 t0",
        "c n0 N2 x1.00 y2.00 X3.00 Y4.00 t0",
    ]
    # inserted right after the existing c-line block, not scattered elsewhere in the file
    assert lines.index("c n0 N2 x1.00 y2.00 X3.00 Y4.00 t0") == lines.index(
        "c n0 N1 x100.00 y200.00 X110.00 Y210.00 t0"
    ) + 1


def test_append_control_points_inserts_after_last_i_line_when_no_c_lines_exist(tmp_path):
    pto = tmp_path / "project.pto"
    _write_fake_pto(
        pto,
        image_lines=["p f2 w4000 h2000 v360 n\"TIFF_m\"\n", "i w1200 h1600 f0 v90 n\"in0.jpg\"\n",
                     "i w1200 h1600 f0 v90 n\"in1.jpg\"\n"],
    )

    main._append_control_points(str(pto), 0, 1, [(5.0, 6.0, 7.0, 8.0, 0.8)])

    lines = pto.read_text(encoding="utf-8").splitlines()
    assert lines[-1] == "c n0 N1 x5.00 y6.00 X7.00 Y8.00 t0"


def test_append_control_points_writes_one_line_per_match(tmp_path):
    pto = tmp_path / "project.pto"
    _write_fake_pto(pto, image_lines=["i w100 h100 n\"in0.jpg\"\n", "i w100 h100 n\"in1.jpg\"\n"])

    matches = [(float(k), float(k), float(k), float(k), 0.5) for k in range(5)]
    main._append_control_points(str(pto), 0, 1, matches)

    lines = pto.read_text(encoding="utf-8").splitlines()
    assert len([l for l in lines if l.startswith("c ")]) == 5


# ---------------------------------------------------------------------------
# _rescue_disconnected_with_loftr
# ---------------------------------------------------------------------------


def _fake_pto_for_rescue(tmp_path, n_images: int) -> str:
    pto = tmp_path / "project.pto"
    _write_fake_pto(pto, image_lines=[f"i w100 h100 n\"in{i}.jpg\"\n" for i in range(n_images)])
    return str(pto)


def test_rescue_is_noop_when_loftr_unavailable(tmp_path, monkeypatch):
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: None)
    calls = []
    monkeypatch.setattr(main, "_loftr_match_pair", lambda a, b: calls.append((a, b)) or [])

    pto = _fake_pto_for_rescue(tmp_path, 3)
    result = main._rescue_disconnected_with_loftr(pto, ["p0", "p1", "p2"], [{0, 1}, {2}])

    assert result is False
    assert calls == []  # never even tries matching if the model isn't available


def test_rescue_stops_after_first_successful_bridge_per_group(tmp_path, monkeypatch):
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: object())  # truthy sentinel
    good_match = [(1.0, 1.0, 1.0, 1.0, 0.9)] * main._LOFTR_MIN_MATCHES_TO_TRUST
    calls = []

    def fake_match(path_i, path_j):
        calls.append((path_i, path_j))
        return good_match  # every attempt "succeeds"

    monkeypatch.setattr(main, "_loftr_match_pair", fake_match)

    pto = _fake_pto_for_rescue(tmp_path, 4)
    input_paths = ["p0", "p1", "p2", "p3"]
    # group {0,1,2} is majority, {3} is the lone disconnected image
    result = main._rescue_disconnected_with_loftr(pto, input_paths, [{0, 1, 2}, {3}])

    assert result is True
    # only ONE candidate pair tried for the single minority image, since it succeeds immediately
    assert calls == [("p3", "p0")]

    c_lines = [l for l in open(pto, encoding="utf-8").readlines() if l.startswith("c ")]
    assert len(c_lines) == main._LOFTR_MIN_MATCHES_TO_TRUST


def test_rescue_tries_next_candidate_after_a_failed_pair(tmp_path, monkeypatch):
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: object())
    good_match = [(1.0, 1.0, 1.0, 1.0, 0.9)] * main._LOFTR_MIN_MATCHES_TO_TRUST
    calls = []

    def fake_match(path_i, path_j):
        calls.append((path_i, path_j))
        return [] if path_j == "p0" else good_match  # first candidate fails, second succeeds

    monkeypatch.setattr(main, "_loftr_match_pair", fake_match)

    pto = _fake_pto_for_rescue(tmp_path, 4)
    result = main._rescue_disconnected_with_loftr(pto, ["p0", "p1", "p2", "p3"], [{0, 1, 2}, {3}])

    assert result is True
    assert calls == [("p3", "p0"), ("p3", "p1")]


def test_rescue_returns_false_when_no_pair_clears_the_bar(tmp_path, monkeypatch):
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: object())
    monkeypatch.setattr(main, "_loftr_match_pair", lambda a, b: [])  # nothing ever matches

    pto = _fake_pto_for_rescue(tmp_path, 4)
    result = main._rescue_disconnected_with_loftr(pto, ["p0", "p1", "p2", "p3"], [{0, 1, 2}, {3}])

    assert result is False
    c_lines = [l for l in open(pto, encoding="utf-8").readlines() if l.startswith("c ")]
    assert c_lines == []


def test_rescue_respects_max_attempts_per_group(tmp_path, monkeypatch):
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: object())
    calls = []

    def fake_match(path_i, path_j):
        calls.append((path_i, path_j))
        return []  # never succeeds, so every majority candidate gets tried up to the cap

    monkeypatch.setattr(main, "_loftr_match_pair", fake_match)

    n_majority = main._LOFTR_MAX_ATTEMPTS_PER_GROUP + 5
    input_paths = [f"p{i}" for i in range(n_majority + 1)]
    majority = set(range(n_majority))
    minority = {n_majority}
    pto = _fake_pto_for_rescue(tmp_path, n_majority + 1)

    main._rescue_disconnected_with_loftr(pto, input_paths, [majority, minority])

    assert len(calls) == main._LOFTR_MAX_ATTEMPTS_PER_GROUP


def test_rescue_bridges_only_one_image_leaving_rest_of_group_to_union_find(tmp_path, monkeypatch):
    """The rescue itself only ever appends control points for ONE bridging pair per minority
    group - the other members of that group are expected to already be mutually connected via
    cpfind's own (pre-existing) edges, and get pulled in by _parse_pto_connectivity's union-find
    re-run in _check_connectivity, not by the rescue appending more points for them directly."""
    monkeypatch.setattr(main, "_get_loftr_matcher", lambda: object())
    good_match = [(1.0, 1.0, 1.0, 1.0, 0.9)] * main._LOFTR_MIN_MATCHES_TO_TRUST
    monkeypatch.setattr(main, "_loftr_match_pair", lambda a, b: good_match)

    pto = _fake_pto_for_rescue(tmp_path, 5)
    # minority group has TWO images (3 and 4); only one of them should get bridged
    main._rescue_disconnected_with_loftr(pto, [f"p{i}" for i in range(5)], [{0, 1, 2}, {3, 4}])

    c_lines = [l for l in open(pto, encoding="utf-8").readlines() if l.startswith("c ")]
    assert len(c_lines) == main._LOFTR_MIN_MATCHES_TO_TRUST  # one pair's worth, not two


# ---------------------------------------------------------------------------
# _check_connectivity wiring (mocks cpfind's subprocess call + the rescue itself)
# ---------------------------------------------------------------------------


def test_check_connectivity_uses_rescue_result_to_shrink_disconnected_list(tmp_path, monkeypatch):
    work_dir = str(tmp_path)
    input_paths = [os.path.join(work_dir, f"in{i}.jpg") for i in range(3)]

    def fake_run(cmd, cwd):
        # emulate pto_gen/cpfind by writing a .pto where image 2 is disconnected
        pto_path = os.path.join(work_dir, "project.pto")
        _write_fake_pto(
            pto_path,
            image_lines=[f"i w100 h100 n\"in{i}.jpg\"\n" for i in range(3)],
            control_point_lines=["c n0 N1 x1.00 y1.00 X2.00 Y2.00 t0\n"] * main._MIN_CONTROL_POINTS,
        )

    monkeypatch.setattr(main, "_run", fake_run)

    def fake_rescue(pto_path, paths, groups):
        # simulate LoFTR successfully bridging image 2 into the majority group
        main._append_control_points(
            pto_path, 0, 2, [(1.0, 1.0, 1.0, 1.0, 0.9)] * main._MIN_CONTROL_POINTS
        )
        return True

    monkeypatch.setattr(main, "_rescue_disconnected_with_loftr", fake_rescue)

    disconnected = main._check_connectivity(work_dir, input_paths)
    assert disconnected == []  # rescue fixed the disconnect, no photos blamed


def test_check_connectivity_falls_back_to_original_result_when_rescue_finds_nothing(
    tmp_path, monkeypatch
):
    work_dir = str(tmp_path)
    input_paths = [os.path.join(work_dir, f"in{i}.jpg") for i in range(3)]

    def fake_run(cmd, cwd):
        pto_path = os.path.join(work_dir, "project.pto")
        _write_fake_pto(
            pto_path,
            image_lines=[f"i w100 h100 n\"in{i}.jpg\"\n" for i in range(3)],
            control_point_lines=["c n0 N1 x1.00 y1.00 X2.00 Y2.00 t0\n"] * main._MIN_CONTROL_POINTS,
        )

    monkeypatch.setattr(main, "_run", fake_run)
    monkeypatch.setattr(main, "_rescue_disconnected_with_loftr", lambda *a: False)

    disconnected = main._check_connectivity(work_dir, input_paths)
    assert disconnected == [2]  # unchanged from cpfind's own (unrescued) result
