"""
Configuración del servidor de portero
"""
import os

_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
_ENV_FILE = os.path.join(_BASE_DIR, '.env')


def _load_dotenv(path: str = _ENV_FILE) -> None:
    """Carga .env local (guardado desde el panel de escritorio) sin dependencia extra."""
    if not os.path.isfile(path):
        return
    try:
        with open(path, encoding='utf-8') as f:
            for raw in f:
                line = raw.strip()
                if not line or line.startswith('#') or '=' not in line:
                    continue
                key, _, value = line.partition('=')
                key = key.strip()
                value = value.strip().strip('"').strip("'")
                if key and key not in os.environ:
                    os.environ[key] = value
    except OSError:
        pass


_load_dotenv()

# SERVIDOR TCP (si 8081 da WinError 10013 en Windows, probá 9077 u otro libre)
SERVER_HOST = '0.0.0.0'  # Escucha en todas las interfaces
try:
    SERVER_PORT = int(os.environ.get('PORTERO_TCP_PORT', '8081'))
except ValueError:
    SERVER_PORT = 8081
MAX_CONNECTIONS = 10
BUFFER_SIZE = 4096
SOCKET_TIMEOUT = 300  # 5 minutos

# BASE DE DATOS
DB_PATH = os.path.join(_BASE_DIR, 'portero_spa.db')

# LOGGING
LOG_DIR = os.path.join(_BASE_DIR, 'logs')
RAW_LOG_DIR = os.path.join(LOG_DIR, 'raw')
SESSION_LOG_DIR = os.path.join(LOG_DIR, 'sessions')
LOG_LEVEL = 'INFO'

# API REST (para otros backends)
API_HOST = '0.0.0.0'
API_PORT = 5000
API_KEY = os.environ.get('PORTERO_API_KEY', 'portero-dev-key-change-me')
WEBHOOK_SECRET = os.environ.get('PORTERO_WEBHOOK_SECRET', '')

# Modo pull: esta PC consulta GestionSpa (Railway) — no hace falta túnel hacia acá
GESTION_BASE_URL = os.environ.get('PORTERO_GESTION_BASE_URL', '').rstrip('/')
AGENT_EMISOR_SLUG = os.environ.get('PORTERO_EMISOR_SLUG', '').strip()
try:
    AGENT_POLL_SECONDS = max(5, int(os.environ.get('PORTERO_POLL_SECONDS', '10')))
except ValueError:
    AGENT_POLL_SECONDS = 10
AGENT_ENABLED = bool(GESTION_BASE_URL and AGENT_EMISOR_SLUG)

# Webhook de fichajes (PC → GestionSpa). Si no está, se arma desde base + slug.
WEBHOOK_URL = os.environ.get('PORTERO_WEBHOOK_URL', '').strip()
if not WEBHOOK_URL and AGENT_ENABLED:
    WEBHOOK_URL = f"{GESTION_BASE_URL}/api/webhooks/portero/{AGENT_EMISOR_SLUG}"

# COLORES PARA TERMINAL
class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'
