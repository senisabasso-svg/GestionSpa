#!/usr/bin/env python3
"""
Inicia el servidor TCP del portero + API REST en un solo proceso.
"""
import logging
import sys
import threading

from config import API_HOST, API_PORT, SERVER_HOST, SERVER_PORT, Colors

if sys.platform == 'win32':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


def main():
    from config import AGENT_ENABLED, AGENT_EMISOR_SLUG, AGENT_POLL_SECONDS, GESTION_BASE_URL
    from log_buffer import install as install_log_buffer, install_stdio_tee
    from servidor_portero import DoorAccessServer
    from api_rest import run_api
    from agent_poller import start_agent_poller

    install_log_buffer()
    install_stdio_tee()

    print(f"""
{Colors.HEADER}ApiPorteroSpa - Modo completo{Colors.ENDC}
  Portero TCP: {SERVER_HOST}:{SERVER_PORT}
  API REST:    http://{API_HOST}:{API_PORT}
  Panel web:   http://{API_HOST}:{API_PORT}/panel
  Agente pull: {'ON -> ' + GESTION_BASE_URL + ' /' + AGENT_EMISOR_SLUG + ' cada ' + str(AGENT_POLL_SECONDS) + 's' if AGENT_ENABLED else 'OFF (configurá Gestion URL + slug)'}
""")

    server = DoorAccessServer(SERVER_HOST, SERVER_PORT)
    tcp_thread = threading.Thread(target=server.start, daemon=True)
    tcp_thread.start()
    start_agent_poller()

    try:
        run_api(host=API_HOST, port=API_PORT)
    except KeyboardInterrupt:
        logger.info("Deteniendo...")
        server.stop()
        sys.exit(0)


if __name__ == '__main__':
    main()
