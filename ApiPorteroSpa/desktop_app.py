#!/usr/bin/env python3
"""
Aplicación de escritorio para operar ApiPorteroSpa sin tocar su lógica.

- Inicia / detiene el mismo proceso que `python run_all.py`
- Muestra estado (corriendo / parado) y health de la API REST
- Logs en vivo (stdout/stderr del servicio)
"""
from __future__ import annotations

import os
import queue
import re
import socket
import subprocess
import sys
import threading
import time
import tkinter as tk
from datetime import datetime
from pathlib import Path
from tkinter import messagebox, scrolledtext, ttk
from urllib.error import URLError
from urllib.request import urlopen

ANSI_RE = re.compile(r"\x1b\[[0-9;]*m")

try:
    from zkteco_protocol import DEFAULT_DEVICE_SN
except Exception:
    DEFAULT_DEVICE_SN = "7674222960189"


def detect_lan_ips() -> list[str]:
    """IPs locales útiles para configurar el portero en la misma red."""
    ips: list[str] = []
    try:
        hostname = socket.gethostname()
        for info in socket.getaddrinfo(hostname, None, socket.AF_INET):
            ip = info[4][0]
            if ip and not ip.startswith("127.") and ip not in ips:
                ips.append(ip)
    except Exception:
        pass
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        if ip and not ip.startswith("127.") and ip not in ips:
            ips.insert(0, ip)
    except Exception:
        pass
    return ips or ["(no detectada — revisá la red)"]

ROOT = Path(__file__).resolve().parent
ENV_FILE = ROOT / ".env"
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from config import API_PORT, SERVER_PORT

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass


def read_env_file(path: Path = ENV_FILE) -> dict[str, str]:
    data: dict[str, str] = {}
    if not path.is_file():
        return data
    try:
        for raw in path.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            data[key.strip()] = value.strip().strip('"').strip("'")
    except OSError:
        pass
    return data


def write_env_file(values: dict[str, str], path: Path = ENV_FILE) -> None:
    lines = [
        "# Generado por el panel ApiPorteroSpa — no subir a git",
        f"PORTERO_API_KEY={values.get('PORTERO_API_KEY', '')}",
        f"PORTERO_GESTION_BASE_URL={values.get('PORTERO_GESTION_BASE_URL', '')}",
        f"PORTERO_EMISOR_SLUG={values.get('PORTERO_EMISOR_SLUG', '')}",
        f"PORTERO_POLL_SECONDS={values.get('PORTERO_POLL_SECONDS', '10')}",
        f"PORTERO_WEBHOOK_URL={values.get('PORTERO_WEBHOOK_URL', '')}",
        f"PORTERO_WEBHOOK_SECRET={values.get('PORTERO_WEBHOOK_SECRET', '')}",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def load_settings() -> dict[str, str]:
    file_vals = read_env_file()

    def pick(key: str, default: str = "") -> str:
        return os.environ.get(key) or file_vals.get(key) or default

    return {
        "PORTERO_API_KEY": pick("PORTERO_API_KEY", "portero-dev-key-change-me"),
        "PORTERO_GESTION_BASE_URL": pick("PORTERO_GESTION_BASE_URL"),
        "PORTERO_EMISOR_SLUG": pick("PORTERO_EMISOR_SLUG"),
        "PORTERO_POLL_SECONDS": pick("PORTERO_POLL_SECONDS", "10"),
        "PORTERO_WEBHOOK_URL": pick("PORTERO_WEBHOOK_URL"),
        "PORTERO_WEBHOOK_SECRET": pick("PORTERO_WEBHOOK_SECRET"),
    }


class PorteroDesktopApp:
    MAX_LOG_CHARS = 200_000
    HEALTH_EVERY_MS = 3000
    PUMP_EVERY_MS = 120

    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("ApiPorteroSpa — Panel de control")
        self.root.geometry("1040x860")
        self.root.minsize(900, 720)
        self._lan_ips = detect_lan_ips()
        self._primary_ip = self._lan_ips[0]
        self._settings = load_settings()

        self.process: subprocess.Popen[str] | None = None
        self.log_queue: queue.Queue[str] = queue.Queue()
        self._reader_threads: list[threading.Thread] = []
        self._stopping = False
        self._auto_scroll = tk.BooleanVar(value=True)
        self._status_text = tk.StringVar(value="Parado")
        self._health_text = tk.StringVar(value="API: —")
        self._uptime_text = tk.StringVar(value="Uptime: —")
        self._started_at: float | None = None
        self.var_api_key = tk.StringVar(value=self._settings["PORTERO_API_KEY"])
        self.var_gestion_url = tk.StringVar(value=self._settings["PORTERO_GESTION_BASE_URL"])
        self.var_emisor_slug = tk.StringVar(value=self._settings["PORTERO_EMISOR_SLUG"])
        self.var_poll_seconds = tk.StringVar(value=self._settings["PORTERO_POLL_SECONDS"])
        self.var_webhook_url = tk.StringVar(value=self._settings["PORTERO_WEBHOOK_URL"])
        self.var_webhook_secret = tk.StringVar(value=self._settings["PORTERO_WEBHOOK_SECRET"])

        self._build_style()
        self._build_ui()
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)
        self.root.after(self.PUMP_EVERY_MS, self._pump_logs)
        self.root.after(self.HEALTH_EVERY_MS, self._poll_health)

    def _build_style(self) -> None:
        self.colors = {
            "bg": "#1c2128",
            "panel": "#252b33",
            "text": "#e6edf3",
            "muted": "#9aa4b2",
            "ok": "#3fb950",
            "bad": "#f85149",
            "warn": "#d29922",
            "accent": "#2f81f7",
            "log_bg": "#0d1117",
        }
        self.root.configure(bg=self.colors["bg"])
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("TFrame", background=self.colors["bg"])
        style.configure("Panel.TFrame", background=self.colors["panel"])
        style.configure("TLabel", background=self.colors["bg"], foreground=self.colors["text"])
        style.configure("Muted.TLabel", background=self.colors["panel"], foreground=self.colors["muted"])
        style.configure("Panel.TLabel", background=self.colors["panel"], foreground=self.colors["text"])
        style.configure("Status.TLabel", background=self.colors["panel"], foreground=self.colors["bad"], font=("Segoe UI", 12, "bold"))
        style.configure("TButton", padding=8)
        style.configure("TCheckbutton", background=self.colors["panel"], foreground=self.colors["text"])

    def _build_ui(self) -> None:
        outer = ttk.Frame(self.root, padding=12)
        outer.pack(fill=tk.BOTH, expand=True)

        header = ttk.Frame(outer, style="Panel.TFrame", padding=12)
        header.pack(fill=tk.X, pady=(0, 10))

        left = ttk.Frame(header, style="Panel.TFrame")
        left.pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Label(left, text="ApiPorteroSpa", style="Panel.TLabel", font=("Segoe UI", 16, "bold")).pack(anchor=tk.W)
        self.hdr_net = ttk.Label(left, style="Muted.TLabel")
        self.hdr_net.pack(anchor=tk.W, pady=(4, 0))
        webhook = self._current_webhook_url() or "(sin webhook URL — configurá abajo)"
        ttk.Label(left, text=f"Webhook: {webhook}", style="Muted.TLabel").pack(anchor=tk.W)
        self.hdr_net.configure(
            text=f"IP LAN: {self._primary_ip}  ·  TCP :{SERVER_PORT}  ·  REST :{API_PORT}"
        )

        right = ttk.Frame(header, style="Panel.TFrame")
        right.pack(side=tk.RIGHT)
        self.status_label = ttk.Label(right, textvariable=self._status_text, style="Status.TLabel")
        self.status_label.pack(anchor=tk.E)
        ttk.Label(right, textvariable=self._health_text, style="Muted.TLabel").pack(anchor=tk.E, pady=(4, 0))
        ttk.Label(right, textvariable=self._uptime_text, style="Muted.TLabel").pack(anchor=tk.E)

        controls = ttk.Frame(outer)
        controls.pack(fill=tk.X, pady=(0, 10))

        self.btn_start = ttk.Button(controls, text="▶  Iniciar servicio", command=self.start_service)
        self.btn_start.pack(side=tk.LEFT)
        self.btn_stop = ttk.Button(controls, text="■  Detener", command=self.stop_service, state=tk.DISABLED)
        self.btn_stop.pack(side=tk.LEFT, padx=(8, 0))
        ttk.Button(controls, text="Limpiar logs", command=self._clear_logs).pack(side=tk.LEFT, padx=(8, 0))
        ttk.Button(controls, text="Abrir carpeta logs", command=self._open_logs_folder).pack(side=tk.LEFT, padx=(8, 0))
        ttk.Checkbutton(controls, text="Auto-scroll", variable=self._auto_scroll).pack(side=tk.RIGHT)

        # PanedWindow: arriba config, abajo logs (arrastra el separador para agrandar/achicar)
        paned = ttk.Panedwindow(outer, orient=tk.VERTICAL)
        paned.pack(fill=tk.BOTH, expand=True)

        top = ttk.Frame(paned)
        bottom = ttk.Frame(paned)
        try:
            paned.add(top, weight=2)
            paned.add(bottom, weight=3)
        except tk.TclError:
            paned.add(top)
            paned.add(bottom)

        self._build_settings_panel(top)
        self._build_config_panel(top)

        log_frame = ttk.Frame(bottom, style="Panel.TFrame", padding=8)
        log_frame.pack(fill=tk.BOTH, expand=True)
        log_hdr = ttk.Frame(log_frame, style="Panel.TFrame")
        log_hdr.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            log_hdr,
            text="Logs en vivo",
            style="Panel.TLabel",
            font=("Segoe UI", 11, "bold"),
        ).pack(side=tk.LEFT)
        ttk.Label(
            log_hdr,
            text="↕ Arrastrá el separador de arriba para agrandar o achicar",
            style="Muted.TLabel",
        ).pack(side=tk.RIGHT)

        self.log_text = scrolledtext.ScrolledText(
            log_frame,
            wrap=tk.WORD,
            height=12,
            bg=self.colors["log_bg"],
            fg=self.colors["text"],
            insertbackground=self.colors["text"],
            relief=tk.FLAT,
            font=("Consolas", 10),
        )
        self.log_text.pack(fill=tk.BOTH, expand=True)
        self.log_text.configure(state=tk.DISABLED)
        self.log_text.tag_configure("err", foreground=self.colors["bad"])
        self.log_text.tag_configure("ok", foreground=self.colors["ok"])
        self.log_text.tag_configure("meta", foreground=self.colors["accent"])

        self.root.after(80, lambda: self._set_sash(paned, 0.48))

        self._append_log("Panel listo. Pulsá «Iniciar servicio» para levantar ApiPorteroSpa.\n", "meta")
        self._append_log(self._config_summary_text() + "\n", "meta")

    def _set_sash(self, paned: ttk.Panedwindow, ratio: float) -> None:
        try:
            paned.update_idletasks()
            h = paned.winfo_height()
            if h > 80:
                paned.sashpos(0, int(h * ratio))
        except tk.TclError:
            pass

    def _build_settings_panel(self, parent: ttk.Frame) -> None:
        box = ttk.Frame(parent, style="Panel.TFrame", padding=12)
        box.pack(fill=tk.X, pady=(0, 10))

        title = ttk.Frame(box, style="Panel.TFrame")
        title.pack(fill=tk.X)
        ttk.Label(
            title,
            text="Configuración de esta API (se guarda en .env)",
            style="Panel.TLabel",
            font=("Segoe UI", 11, "bold"),
        ).pack(side=tk.LEFT)
        ttk.Button(title, text="Guardar", command=self._save_settings).pack(side=tk.RIGHT)

        ttk.Label(
            box,
            text=(
                "Modo pull: esta PC consulta GestionSpa (Railway). No hace falta abrir puertos ni túnel. "
                "Si el servicio está corriendo, guardá y reiniciá (Detener → Iniciar)."
            ),
            style="Muted.TLabel",
            wraplength=980,
            justify=tk.LEFT,
        ).pack(anchor=tk.W, pady=(6, 8))

        form = ttk.Frame(box, style="Panel.TFrame")
        form.pack(fill=tk.X)

        def row(r: int, label: str, var: tk.StringVar, width: int = 72) -> None:
            ttk.Label(form, text=label, style="Panel.TLabel", width=26).grid(row=r, column=0, sticky=tk.W, pady=3)
            entry = ttk.Entry(form, textvariable=var, width=width)
            entry.grid(row=r, column=1, sticky=tk.EW, pady=3, padx=(8, 0))

        form.columnconfigure(1, weight=1)
        row(0, "API Key (igual que GestionSpa)", self.var_api_key)
        row(1, "URL base GestionSpa", self.var_gestion_url)
        row(2, "Slug del emisor", self.var_emisor_slug)
        row(3, "Poll segundos", self.var_poll_seconds)
        row(4, "Webhook URL (opcional)", self.var_webhook_url)
        row(5, "Webhook Secret", self.var_webhook_secret)

        ttk.Label(
            box,
            text=(
                "URL base ejemplo: https://tu-api.up.railway.app  (sin /api al final)\n"
                "Slug: el del spa en GestionSpa. Webhook se completa solo si dejás Webhook URL vacío."
            ),
            style="Muted.TLabel",
            justify=tk.LEFT,
        ).pack(anchor=tk.W, pady=(8, 0))

    def _save_settings(self) -> None:
        poll = self.var_poll_seconds.get().strip() or "10"
        try:
            poll_n = max(5, int(poll))
        except ValueError:
            poll_n = 10
        self._settings = {
            "PORTERO_API_KEY": self.var_api_key.get().strip() or "portero-dev-key-change-me",
            "PORTERO_GESTION_BASE_URL": self.var_gestion_url.get().strip().rstrip("/"),
            "PORTERO_EMISOR_SLUG": self.var_emisor_slug.get().strip(),
            "PORTERO_POLL_SECONDS": str(poll_n),
            "PORTERO_WEBHOOK_URL": self.var_webhook_url.get().strip(),
            "PORTERO_WEBHOOK_SECRET": self.var_webhook_secret.get().strip(),
        }
        self.var_api_key.set(self._settings["PORTERO_API_KEY"])
        self.var_poll_seconds.set(self._settings["PORTERO_POLL_SECONDS"])
        try:
            write_env_file(self._settings)
        except OSError as exc:
            messagebox.showerror("No se pudo guardar", str(exc))
            return
        self._render_config_labels()
        self._append_log(f"[{self._now()}] Configuración guardada en {ENV_FILE.name}\n", "ok")
        if self.process and self.process.poll() is None:
            messagebox.showinfo(
                "Reinicio necesario",
                "Se guardó bien.\nDetené e iniciá el servicio para que tome la nueva config.",
            )
        else:
            messagebox.showinfo("Guardado", f"Configuración guardada en {ENV_FILE}")

    def _current_api_key(self) -> str:
        return self.var_api_key.get().strip() or self._settings["PORTERO_API_KEY"]

    def _current_gestion_url(self) -> str:
        return self.var_gestion_url.get().strip().rstrip("/")

    def _current_emisor_slug(self) -> str:
        return self.var_emisor_slug.get().strip()

    def _current_webhook_url(self) -> str:
        manual = self.var_webhook_url.get().strip()
        if manual:
            return manual
        base = self._current_gestion_url()
        slug = self._current_emisor_slug()
        if base and slug:
            return f"{base}/api/webhooks/portero/{slug}"
        return ""

    def _current_webhook_secret(self) -> str:
        return self.var_webhook_secret.get().strip()

    def _build_config_panel(self, parent: ttk.Frame) -> None:
        box = ttk.Frame(parent, style="Panel.TFrame", padding=12)
        box.pack(fill=tk.X, pady=(0, 10))

        title_row = ttk.Frame(box, style="Panel.TFrame")
        title_row.pack(fill=tk.X)
        ttk.Label(
            title_row,
            text="Datos para configurar",
            style="Panel.TLabel",
            font=("Segoe UI", 11, "bold"),
        ).pack(side=tk.LEFT)
        ttk.Button(title_row, text="Actualizar IP", command=self._refresh_ips).pack(side=tk.RIGHT)
        ttk.Button(title_row, text="Copiar todo", command=self._copy_all_config).pack(side=tk.RIGHT, padx=(0, 8))

        cols = ttk.Frame(box, style="Panel.TFrame")
        cols.pack(fill=tk.X, pady=(10, 0))

        left = ttk.Frame(cols, style="Panel.TFrame")
        left.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 8))
        right = ttk.Frame(cols, style="Panel.TFrame")
        right.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(8, 0))

        ttk.Label(left, text="En el PORTERO físico (menú Cloud / servidor)", style="Panel.TLabel", font=("Segoe UI", 10, "bold")).pack(anchor=tk.W)
        self.lbl_portero_ip = ttk.Label(left, style="Muted.TLabel", justify=tk.LEFT)
        self.lbl_portero_ip.pack(anchor=tk.W, pady=(4, 6))
        btn_row_l = ttk.Frame(left, style="Panel.TFrame")
        btn_row_l.pack(anchor=tk.W)
        ttk.Button(btn_row_l, text="Copiar IP", command=lambda: self._copy(self._primary_ip, "IP")).pack(side=tk.LEFT)
        ttk.Button(btn_row_l, text="Copiar puerto TCP", command=lambda: self._copy(str(SERVER_PORT), "Puerto TCP")).pack(side=tk.LEFT, padx=(6, 0))

        ttk.Label(right, text="En GestionSpa → Configuración → Portero", style="Panel.TLabel", font=("Segoe UI", 10, "bold")).pack(anchor=tk.W)
        self.lbl_gestion = ttk.Label(right, style="Muted.TLabel", justify=tk.LEFT)
        self.lbl_gestion.pack(anchor=tk.W, pady=(4, 6))
        btn_row_r = ttk.Frame(right, style="Panel.TFrame")
        btn_row_r.pack(anchor=tk.W)
        ttk.Button(btn_row_r, text="Copiar apiKey", command=lambda: self._copy(self._current_api_key(), "apiKey")).pack(side=tk.LEFT)
        ttk.Button(btn_row_r, text="Copiar deviceSn", command=lambda: self._copy(DEFAULT_DEVICE_SN, "deviceSn")).pack(side=tk.LEFT, padx=(6, 0))
        ttk.Button(btn_row_r, text="Copiar slug", command=lambda: self._copy(self._current_emisor_slug(), "slug")).pack(side=tk.LEFT, padx=(6, 0))

        note = ttk.Label(
            box,
            text=(
                "Modo pull: GestionSpa encola comandos; esta PC los pide sola. "
                "Solo necesitás internet de salida. El ZKTeco sigue en LAN (IP + puerto 8081)."
            ),
            style="Muted.TLabel",
            wraplength=980,
            justify=tk.LEFT,
        )
        note.pack(anchor=tk.W, pady=(10, 0))
        self._render_config_labels()

    def _api_url(self) -> str:
        return f"http://{self._primary_ip}:{API_PORT}"

    def _render_config_labels(self) -> None:
        otras = ", ".join(self._lan_ips[1:]) if len(self._lan_ips) > 1 else "—"
        self.lbl_portero_ip.configure(
            text=(
                f"Dirección / IP del servidor:  {self._primary_ip}\n"
                f"Puerto:  {SERVER_PORT}\n"
                f"Protocolo:  TCP (Cloud / ADMS)\n"
                f"Otras IPs de esta PC:  {otras}\n"
                f"SN equipo por defecto:  {DEFAULT_DEVICE_SN}"
            )
        )
        webhook = self._current_webhook_url() or "(poné URL base GestionSpa + slug arriba)"
        secret = self._current_webhook_secret() or "(completar Webhook Secret)"
        self.lbl_gestion.configure(
            text=(
                f"habilitado:  sí\n"
                f"modo:  pull (sin apiUrl hacia esta PC)\n"
                f"apiKey:  {self._current_api_key()}\n"
                f"deviceSn:  {DEFAULT_DEVICE_SN}\n"
                f"sincronizarAutomatico:  sí\n"
                f"webhookSecret:  {secret}\n"
                f"Gestion URL en esta PC:  {self._current_gestion_url() or '—'}\n"
                f"Slug:  {self._current_emisor_slug() or '—'}\n"
                f"Webhook fichajes:  {webhook}"
            )
        )

    def _config_summary_text(self) -> str:
        pull = (
            f"Pull ON → {self._current_gestion_url()}/{self._current_emisor_slug()}"
            if self._current_gestion_url() and self._current_emisor_slug()
            else "Pull OFF (faltan URL GestionSpa o slug)"
        )
        return (
            f"Portero físico → IP {self._primary_ip} puerto {SERVER_PORT}\n"
            f"{pull} | apiKey {self._current_api_key()} | deviceSn {DEFAULT_DEVICE_SN}"
        )

    def _refresh_ips(self) -> None:
        self._lan_ips = detect_lan_ips()
        self._primary_ip = self._lan_ips[0]
        self._render_config_labels()
        self.hdr_net.configure(
            text=f"IP LAN: {self._primary_ip}  ·  TCP :{SERVER_PORT}  ·  REST :{API_PORT}"
        )
        self._append_log(f"[{self._now()}] IP actualizada: {self._primary_ip}\n", "meta")

    def _copy(self, value: str, label: str) -> None:
        self.root.clipboard_clear()
        self.root.clipboard_append(value)
        self.root.update_idletasks()
        self._append_log(f"[{self._now()}] Copiado {label}: {value}\n", "ok")

    def _copy_all_config(self) -> None:
        text = (
            "=== PORTERO FÍSICO ===\n"
            f"IP servidor: {self._primary_ip}\n"
            f"Puerto TCP: {SERVER_PORT}\n"
            f"SN: {DEFAULT_DEVICE_SN}\n\n"
            "=== GESTIONSPA (Configuración → Portero) ===\n"
            f"habilitado: true\n"
            f"apiKey: {self._current_api_key()}\n"
            f"deviceSn: {DEFAULT_DEVICE_SN}\n"
            f"sincronizarAutomatico: true\n"
            f"webhookSecret: {self._current_webhook_secret() or '<definir>'}\n"
            f"(modo pull: no hace falta apiUrl hacia la PC)\n\n"
            "=== ESTA PC (panel ApiPortero) ===\n"
            f"PORTERO_GESTION_BASE_URL: {self._current_gestion_url()}\n"
            f"PORTERO_EMISOR_SLUG: {self._current_emisor_slug()}\n"
            f"Webhook: {self._current_webhook_url()}\n"
        )
        self._copy(text, "configuración completa")

    def _set_running_ui(self, running: bool) -> None:
        if running:
            self._status_text.set("En ejecución")
            self.status_label.configure(foreground=self.colors["ok"])
            self.btn_start.configure(state=tk.DISABLED)
            self.btn_stop.configure(state=tk.NORMAL)
        else:
            self._status_text.set("Parado")
            self.status_label.configure(foreground=self.colors["bad"])
            self.btn_start.configure(state=tk.NORMAL)
            self.btn_stop.configure(state=tk.DISABLED)
            self._health_text.set("API: —")
            self._uptime_text.set("Uptime: —")
            self._started_at = None

    def start_service(self) -> None:
        if self.process and self.process.poll() is None:
            messagebox.showinfo("Ya está corriendo", "El servicio ya está en ejecución.")
            return

        run_all = ROOT / "run_all.py"
        if not run_all.exists():
            messagebox.showerror("Error", f"No se encontró {run_all}")
            return

        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env["PYTHONIOENCODING"] = "utf-8"
        env["PORTERO_API_KEY"] = self._current_api_key()
        env["PORTERO_GESTION_BASE_URL"] = self._current_gestion_url()
        env["PORTERO_EMISOR_SLUG"] = self._current_emisor_slug()
        env["PORTERO_POLL_SECONDS"] = self.var_poll_seconds.get().strip() or "10"
        env["PORTERO_WEBHOOK_URL"] = self._current_webhook_url()
        env["PORTERO_WEBHOOK_SECRET"] = self._current_webhook_secret()

        creationflags = 0
        if sys.platform == "win32":
            creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)

        try:
            self.process = subprocess.Popen(
                [sys.executable, "-u", str(run_all)],
                cwd=str(ROOT),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding="utf-8",
                errors="replace",
                bufsize=1,
                env=env,
                creationflags=creationflags,
            )
        except Exception as exc:
            messagebox.showerror("No se pudo iniciar", str(exc))
            return

        self._stopping = False
        self._started_at = time.time()
        self._set_running_ui(True)
        self._append_log(f"\n[{self._now()}] Iniciando run_all.py (pid {self.process.pid})\n", "ok")

        for stream in (self.process.stdout, self.process.stderr):
            if stream is None:
                continue
            t = threading.Thread(target=self._read_stream, args=(stream,), daemon=True)
            t.start()
            self._reader_threads.append(t)

        threading.Thread(target=self._watch_process, daemon=True).start()

    def stop_service(self) -> None:
        if not self.process or self.process.poll() is not None:
            self._set_running_ui(False)
            return
        self._stopping = True
        self._append_log(f"\n[{self._now()}] Deteniendo servicio...\n", "meta")
        proc = self.process
        try:
            proc.terminate()
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=3)
        except Exception as exc:
            self._append_log(f"Error al detener: {exc}\n", "err")
        finally:
            self.process = None
            self._set_running_ui(False)
            self._append_log(f"[{self._now()}] Servicio detenido.\n", "meta")

    def _read_stream(self, stream) -> None:
        try:
            for line in stream:
                # Flask / logging escriben INFO en stderr: no es un error
                self.log_queue.put(ANSI_RE.sub("", line))
        except Exception as exc:
            self.log_queue.put(f"[reader] {exc}\n")

    def _watch_process(self) -> None:
        proc = self.process
        if proc is None:
            return
        code = proc.wait()
        if self._stopping:
            return
        self.log_queue.put(f"\n[{self._now()}] El proceso terminó solo (código {code}).\n")
        self.root.after(0, self._set_running_ui, False)

    def _pump_logs(self) -> None:
        try:
            while True:
                line = self.log_queue.get_nowait()
                self._append_log(line, self._tag_for_line(line))
        except queue.Empty:
            pass

        if self.process and self.process.poll() is None and self._started_at:
            secs = int(time.time() - self._started_at)
            h, rem = divmod(secs, 3600)
            m, s = divmod(rem, 60)
            self._uptime_text.set(f"Uptime: {h:02d}:{m:02d}:{s:02d}")

        self.root.after(self.PUMP_EVERY_MS, self._pump_logs)

    def _poll_health(self) -> None:
        running = self.process is not None and self.process.poll() is None
        if not running:
            self.root.after(self.HEALTH_EVERY_MS, self._poll_health)
            return

        def check() -> None:
            url = f"http://127.0.0.1:{API_PORT}/api/health"
            try:
                with urlopen(url, timeout=2) as resp:
                    status = resp.status
                msg = "API: OK (health)" if status == 200 else f"API: HTTP {status}"
                self.root.after(0, self._health_text.set, msg)
            except URLError:
                self.root.after(0, self._health_text.set, "API: sin respuesta")
            except Exception as exc:
                self.root.after(0, self._health_text.set, f"API: error ({exc})")

        threading.Thread(target=check, daemon=True).start()
        self.root.after(self.HEALTH_EVERY_MS, self._poll_health)

    @staticmethod
    def _tag_for_line(line: str) -> str | None:
        upper = line.upper()
        if "TRACEBACK" in upper or "EXCEPTION" in upper:
            return "err"
        if re.search(r"\bERROR\b", upper) or re.search(r"\bCRITICAL\b", upper):
            return "err"
        if "WARNING: THIS IS A DEVELOPMENT SERVER" in upper:
            return "meta"  # aviso conocido; no es falla del portero
        if re.search(r"\bWARNING\b", upper) or re.search(r"\bWARN\b", upper):
            return "meta"
        if "SERVING ON" in upper or "ESCUCHANDO" in upper or "MODO ZKTECO" in upper:
            return "ok"
        return None

    def _append_log(self, text: str, tag: str | None = None) -> None:
        self.log_text.configure(state=tk.NORMAL)
        if tag:
            self.log_text.insert(tk.END, text, tag)
        else:
            self.log_text.insert(tk.END, text)
        content = self.log_text.get("1.0", tk.END)
        if len(content) > self.MAX_LOG_CHARS:
            self.log_text.delete("1.0", f"1.0+{len(content) - self.MAX_LOG_CHARS}c")
        if self._auto_scroll.get():
            self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)

    def _clear_logs(self) -> None:
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.delete("1.0", tk.END)
        self.log_text.configure(state=tk.DISABLED)

    def _open_logs_folder(self) -> None:
        logs = ROOT / "logs"
        logs.mkdir(parents=True, exist_ok=True)
        try:
            if sys.platform == "win32":
                os.startfile(str(logs))  # type: ignore[attr-defined]
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(logs)])
            else:
                subprocess.Popen(["xdg-open", str(logs)])
        except Exception as exc:
            messagebox.showerror("No se pudo abrir", str(exc))

    def _on_close(self) -> None:
        if self.process and self.process.poll() is None:
            if not messagebox.askyesno(
                "Salir",
                "El servicio sigue corriendo.\n¿Detenerlo y cerrar el panel?",
            ):
                return
            self.stop_service()
        self.root.destroy()

    @staticmethod
    def _now() -> str:
        return datetime.now().strftime("%H:%M:%S")


def main() -> None:
    root = tk.Tk()
    PorteroDesktopApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
