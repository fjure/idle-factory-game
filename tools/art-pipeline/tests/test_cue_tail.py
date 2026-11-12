"""F006 T524: the production badge pointer tail (OD-12, K4) is deterministic, palette-exact and consistent with the T517 mock."""
import pytest

from art_pipeline import pixel_pass as pp
from art_pipeline.f006 import a03_anchor as a
from art_pipeline.f006 import cue_tail as ct


@pytest.fixture(scope="module")
def build():
    return ct.build_all()


def test_all_checks_pass_and_five_badges(build):
    assert build["report"]["passed"]
    assert len(build["sprites"]) == 5


def test_tail_rows_and_box(build):
    for s in build["sprites"]:
        assert pp.opaque_bbox(s.image) == ct.BOX_WITH_TAIL == (48, 10, 71, 37)
        names = {r.name: r for r in s.results}
        assert names["tail_geometry"].details["rowWidths"] == [7, 5, 3, 1]


def test_production_tail_equals_the_t517_mock(build):
    inp = a.load_inputs()
    kinds = {"f003-badge-missing-input": "blocked", "f003-badge-storage-full": "faulted"}
    k4 = next(c for c in a.CANDIDATES if c.cid == "K4")
    for s in build["sprites"]:
        if s.asset_id in kinds:
            mock = a.candidate_sprite(inp, k4, kinds[s.asset_id])
            assert list(mock.getdata()) == list(s.image.getdata()), s.asset_id


def test_rerun_is_byte_identical(build):
    again = ct.build_all()
    for x, y in zip(build["sprites"], again["sprites"]):
        assert pp.png_bytes(x.image) == pp.png_bytes(y.image)
