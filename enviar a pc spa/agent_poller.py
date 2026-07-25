#!/usr/bin/env python3
"""
Agente pull: ApiPorteroSpa consulta GestionSpa cada N segundos y aplica comandos.
Así Railway nunca necesita llegar a la PC del spa.
"""
from __future__ import annotations

import logging
import threading
import time

import requests

from config import (
    AGENT_ENABLED,
    AGENT_EMISOR_SLUG,
    AGENT_POLL_SECONDS,
    API_KEY,
    GESTION_BASE_URL,
)
from database import Database
from socio_service import abrir_puerta, crear_socio, eliminar_socio

logger = logging.getLogger(__name__)


class GestionAgentPoller:
    def __init__(self):
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self.db = Database()

    def start(self):
        if not AGENT_ENABLED:
            logger.info("Agente pull deshabilitado (faltan GESTION_BASE_URL / EMISOR_SLUG)")
            return
        if self._thread and self._thread.is_alive():
            return
        self._stop.clear()
        self._thread = threading.Thread(target=self._loop, name="gestion-agent", daemon=True)
        self._thread.start()
        logger.info(
            "Agente pull iniciado -> %s (emisor=%s, cada %ss)",
            GESTION_BASE_URL,
            AGENT_EMISOR_SLUG,
            AGENT_POLL_SECONDS,
        )

    def stop(self):
        self._stop.set()

    def _headers(self):
        return {
            "X-API-Key": API_KEY,
            "Content-Type": "application/json",
            "Accept": "application/json",
        }

    def _base(self) -> str:
        return f"{GESTION_BASE_URL.rstrip('/')}/api/portero/agent/{AGENT_EMISOR_SLUG}"

    def _loop(self):
        while not self._stop.is_set():
            try:
                self._heartbeat()
                self._pull_and_run()
            except Exception as ex:
                logger.error("Agente pull error: %s", ex)
            self._stop.wait(AGENT_POLL_SECONDS)

    def _heartbeat(self):
        url = f"{self._base()}/heartbeat"
        try:
            r = requests.post(
                url,
                headers=self._headers(),
                json={"version": "ApiPorteroSpa-pull", "pendingLocalCommands": 0},
                timeout=15,
            )
            if r.status_code >= 400:
                logger.warning("Heartbeat HTTP %s: %s", r.status_code, r.text[:200])
        except requests.RequestException as ex:
            logger.warning("Heartbeat fallo: %s", ex)

    def _pull_and_run(self):
        url = f"{self._base()}/comandos"
        r = requests.get(url, headers=self._headers(), params={"limit": 50}, timeout=30)
        if r.status_code == 401:
            logger.error("API key inválida o emisor incorrecto al pedir comandos")
            return
        r.raise_for_status()
        data = r.json() or {}
        comandos = data.get("comandos") or []
        if not comandos:
            return

        logger.info("Recibidos %s comando(s) de GestionSpa", len(comandos))
        for cmd in comandos:
            self._run_one(cmd)

    def _run_one(self, cmd: dict):
        cmd_id = cmd.get("id")
        tipo = (cmd.get("tipo") or "").lower()
        payload = cmd.get("payload") or {}
        ok = False
        error = None
        try:
            if tipo == "upsert_socio":
                crear_socio(
                    self.db,
                    nombre=payload.get("nombre") or "",
                    cedula=str(payload.get("cedula") or ""),
                    celular=payload.get("celular") or "",
                    email=payload.get("email") or "",
                    device_sn=payload.get("device_sn") or "7674222960189",
                    membership_type=payload.get("membership_type") or "socio",
                    access_level=int(payload.get("access_level") or 1),
                    valid_days=int(payload.get("valid_days") or 365),
                )
                ok = True
            elif tipo == "delete_socio":
                eliminar_socio(
                    self.db,
                    pin=str(payload.get("cedula") or payload.get("pin") or ""),
                    device_sn=payload.get("device_sn") or "7674222960189",
                )
                ok = True
            elif tipo == "abrir_puerta":
                abrir_puerta(
                    self.db,
                    device_sn=payload.get("device_sn") or "7674222960189",
                )
                ok = True
            else:
                error = f"Tipo desconocido: {tipo}"
        except Exception as ex:
            error = str(ex)
            logger.exception("Error ejecutando comando %s (%s)", cmd_id, tipo)

        self._ack(cmd_id, ok, error)

    def _ack(self, cmd_id, ok: bool, error: str | None):
        if cmd_id is None:
            return
        url = f"{self._base()}/comandos/{cmd_id}/ack"
        try:
            r = requests.post(
                url,
                headers=self._headers(),
                json={"ok": ok, "error": error},
                timeout=15,
            )
            if r.status_code >= 400:
                logger.warning("Ack comando %s HTTP %s: %s", cmd_id, r.status_code, r.text[:200])
        except requests.RequestException as ex:
            logger.warning("Ack comando %s fallo: %s", cmd_id, ex)


_poller: GestionAgentPoller | None = None


def start_agent_poller():
    global _poller
    _poller = GestionAgentPoller()
    _poller.start()
    return _poller
