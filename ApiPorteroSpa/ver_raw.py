#!/usr/bin/env python3
"""Visualiza paquetes crudos capturados del portero"""
import os
import glob
from database import Database
from config import Colors, RAW_LOG_DIR, SESSION_LOG_DIR


def print_header(title):
    print(f"\n{Colors.OKBLUE}{Colors.BOLD}{'=' * 60}{Colors.ENDC}")
    print(f"{Colors.OKBLUE}{Colors.BOLD}  {title}{Colors.ENDC}")
    print(f"{Colors.OKBLUE}{Colors.BOLD}{'=' * 60}{Colors.ENDC}\n")


def main():
    db = Database()
    stats = db.get_stats()

    print(f"""{Colors.HEADER}
CAPTURA CRUDA - TRAFICO DEL PORTERO
{Colors.ENDC}""")
    print(f"Paquetes en BD: {stats.get('total_raw_packets', 0)}")
    print(f"Log consolidado: {RAW_LOG_DIR}/raw_traffic.log")
    print(f"Sesiones: {SESSION_LOG_DIR}/")

    print_header("ULTIMOS PAQUETES (BD)")
    packets = db.get_raw_packets(20)

    if not packets:
        print(f"{Colors.WARNING}No hay paquetes capturados aun.{Colors.ENDC}")
        print("Espera a que el portero conecte con el servidor reiniciado.")
    else:
        for p in packets:
            print(f"""
  [{p['created_at']}] {p['direction'].upper()} | sesion={p['session_id']}
  Origen: {p['remote_ip']}:{p['remote_port']} | {p['byte_length']} bytes
  Protocolo: {p['protocol_hint']}
  Preview: {p['ascii_preview'][:150]}
  UTF-8: {(p['text_utf8'] or '')[:300]}
  HEX (inicio): {(p['hex_data'] or '')[:120]}...
            """)

    print_header("ARCHIVOS DE SESION RECIENTES")
    session_files = sorted(
        glob.glob(os.path.join(SESSION_LOG_DIR, '*.log')),
        key=os.path.getmtime,
        reverse=True
    )[:10]

    if not session_files:
        print(f"{Colors.WARNING}No hay archivos de sesion aun.{Colors.ENDC}")
    else:
        for path in session_files:
            size = os.path.getsize(path)
            print(f"  {os.path.basename(path)} ({size} bytes)")

    print(f"\n{Colors.OKBLUE}{'=' * 60}{Colors.ENDC}\n")


if __name__ == '__main__':
    main()
