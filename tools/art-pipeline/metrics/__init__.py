"""Feature 007 comparison metrics (T603): SC-003 silhouette IoU, SC-004 machine area, SC-006 dead area.

The scripts read capture PNGs and the JSON layout dump the capture rig writes next to them (data-model "ComparisonCapture", research R18).
`METRICS_VERSION` is frozen into every baseline value file, so a later run can tell whether the method changed.
"""
METRICS_VERSION = "1.0.0"
