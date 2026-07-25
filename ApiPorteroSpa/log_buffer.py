"""Buffer en memoria de logs para el panel web."""
from __future__ import annotations

import logging
import re
import threading
from collections import deque
from datetime import datetime, timezone

ANSI_RE = re.compile(r"\x1b\[[0-9;]*m")
_MAX_LINES = 2000
_lock = threading.Lock()
_lines: deque[dict] = deque(maxlen=_MAX_LINES)
_seq = 0


def _now_iso() -> str:
    return datetime.now(timezone.utc).astimezone().strftime("%Y-%m-%d %H:%M:%S")


def append(text: str, level: str = "INFO") -> None:
    global _seq
    clean = ANSI_RE.sub("", text or "").rstrip("\r\n")
    if not clean:
        return
    with _lock:
        _seq += 1
        _lines.append({
            "id": _seq,
            "ts": _now_iso(),
            "level": (level or "INFO").upper(),
            "msg": clean,
        })


def get_lines(limit: int = 300, since: int = 0) -> dict:
    """Líneas con id > since. Si since=0, últimas `limit`."""
    limit = max(1, min(int(limit or 300), 1000))
    since = max(0, int(since or 0))
    with _lock:
        snapshot = list(_lines)
        total = len(snapshot)
        if since <= 0:
            chunk = snapshot[-limit:]
        else:
            chunk = [row for row in snapshot if row["id"] > since][-limit:]
        next_cursor = chunk[-1]["id"] if chunk else since
        return {
            "lines": chunk,
            "total": total,
            "next": next_cursor,
        }


def clear() -> None:
    with _lock:
        _lines.clear()


class MemoryLogHandler(logging.Handler):
    def emit(self, record: logging.LogRecord) -> None:
        try:
            msg = self.format(record)
        except Exception:
            msg = record.getMessage()
        append(msg, level=record.levelname)


class TeeStream:
    """Duplica stdout/stderr hacia el buffer (print también aparece en el panel)."""

    def __init__(self, original, level: str = "INFO"):
        self._original = original
        self._level = level
        self._buf = ""

    def write(self, data):
        if not isinstance(data, str):
            data = str(data)
        self._original.write(data)
        self._buf += data
        while "\n" in self._buf:
            line, self._buf = self._buf.split("\n", 1)
            append(line, level=self._level)
        return len(data)

    def flush(self):
        if self._buf:
            append(self._buf, level=self._level)
            self._buf = ""
        self._original.flush()

    def isatty(self):
        return getattr(self._original, "isatty", lambda: False)()

    def fileno(self):
        return self._original.fileno()


def install(max_lines: int = _MAX_LINES) -> MemoryLogHandler:
    global _lines, _seq
    with _lock:
        _lines = deque(maxlen=max(100, int(max_lines)))
        _seq = 0
    handler = MemoryLogHandler()
    handler.setLevel(logging.INFO)
    handler.setFormatter(logging.Formatter("%(asctime)s - %(levelname)s - %(message)s"))
    root = logging.getLogger()
    root.addHandler(handler)
    return handler


def install_stdio_tee() -> None:
    import sys
    if not isinstance(sys.stdout, TeeStream):
        sys.stdout = TeeStream(sys.stdout, "INFO")  # type: ignore[assignment]
    if not isinstance(sys.stderr, TeeStream):
        sys.stderr = TeeStream(sys.stderr, "ERROR")  # type: ignore[assignment]
