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

# Datos persistentes (en Railway montá un Volume en /data)
_DATA_DIR = os.environ.get('PORTERO_DATA_DIR', _BASE_DIR)
os.makedirs(_DATA_DIR, exist_ok=True)

def _env_int(name: str, default: int) -> int:
    raw = os.environ.get(name)
    if raw is None or str(raw).strip() == '':
        return default
    try:
        return int(raw)
    except ValueError:
        return default


# SERVIDOR TCP (Railway TCP Proxy apunta a este puerto interno)
SERVER_HOST = '0.0.0.0'
SERVER_PORT = _env_int('PORTERO_TCP_PORT', 8081)
MAX_CONNECTIONS = 10
BUFFER_SIZE = 4096
SOCKET_TIMEOUT = 300  # 5 minutos

# BASE DE DATOS
DB_PATH = os.path.join(_DATA_DIR, 'portero_spa.db')

# LOGGING
LOG_DIR = os.path.join(_DATA_DIR, 'logs')
RAW_LOG_DIR = os.path.join(LOG_DIR, 'raw')
SESSION_LOG_DIR = os.path.join(LOG_DIR, 'sessions')
LOG_LEVEL = 'INFO'
for _d in (LOG_DIR, RAW_LOG_DIR, SESSION_LOG_DIR):
    os.makedirs(_d, exist_ok=True)

# API REST — Railway inyecta PORT; local default 5000.
# Nunca compartir puerto con el TCP del ZKTeco (si PORT=8081 choca con PORTERO_TCP_PORT).
API_HOST = '0.0.0.0'
_port_railway = os.environ.get('PORT')
_port_explicit = os.environ.get('PORTERO_API_PORT')
if _port_explicit not in (None, ''):
    API_PORT = _env_int('PORTERO_API_PORT', 8080)
elif _port_railway not in (None, ''):
    API_PORT = _env_int('PORT', 8080)
else:
    API_PORT = 5000

if API_PORT == SERVER_PORT:
    # Caso típico en Railway: alguien puso PORT=8081 o el target HTTP quedó en 8081.
    # Priorizamos TCP en PORTERO_TCP_PORT (proxy ZKTeco) y movemos REST a 8080.
    _fallback = _env_int('PORTERO_API_PORT', 8080)
    if _fallback == SERVER_PORT:
        _fallback = 8080 if SERVER_PORT != 8080 else 8082
    print(
        f"[config] Puerto en conflicto: REST y TCP ambos en {SERVER_PORT}. "
        f"REST pasa a {_fallback}. En Railway: HTTP domain -> {_fallback}, "
        f"TCP Proxy -> {SERVER_PORT}. No definas PORT={SERVER_PORT}."
    )
    API_PORT = _fallback
    # Para que healthchecks/proxies que lean PORT vean el puerto real del REST
    os.environ['PORT'] = str(API_PORT)

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
