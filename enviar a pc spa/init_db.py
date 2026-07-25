#!/usr/bin/env python3
"""Crea portero_spa.db y carpetas de logs si no existen."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from config import DB_PATH, LOG_DIR, RAW_LOG_DIR, SESSION_LOG_DIR
from database import Database


def main() -> int:
    for folder in (LOG_DIR, RAW_LOG_DIR, SESSION_LOG_DIR):
        Path(folder).mkdir(parents=True, exist_ok=True)

    existed = Path(DB_PATH).is_file()
    Database(DB_PATH)  # init_db() crea tablas si faltan
    status = "ya existía (tablas verificadas)" if existed else "creada"
    print(f"OK base de datos: {DB_PATH} ({status})")
    print(f"OK logs: {LOG_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
