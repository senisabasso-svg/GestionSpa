"""
Protocolo ZKTeco iClock PUSH (ADMS) sobre HTTP.
"""
import re
import logging
from datetime import datetime
from urllib.parse import urlparse, parse_qs

logger = logging.getLogger(__name__)

DEFAULT_DEVICE_SN = '7674222960189'

# VerifyMode: 0=any, 1=fingerprint, 2=card, 15=face (comun en ZKTeco)
VERIFY_FACE = 15


def parse_http_request(data: bytes) -> dict | None:
    """Separa metodo, ruta, query, headers y body de una peticion HTTP."""
    if b'\r\n\r\n' not in data:
        if data.startswith(b'GET ') and data.endswith(b'\r\n\r\n'):
            header_part = data.decode('utf-8', errors='ignore')
            body = ''
        else:
            return None
    else:
        header_part, body_bytes = data.split(b'\r\n\r\n', 1)
        body = body_bytes.decode('utf-8', errors='ignore')

    lines = header_part.decode('utf-8', errors='ignore').split('\r\n')
    if not lines:
        return None

    request_line = lines[0]
    parts = request_line.split(' ')
    if len(parts) < 2:
        return None

    method = parts[0]
    path = parts[1]
    parsed = urlparse(path)
    query = {k: v[0] for k, v in parse_qs(parsed.query).items()}

    return {
        'method': method,
        'path': parsed.path,
        'query': query,
        'sn': query.get('SN', ''),
        'table': query.get('table', ''),
        'body': body,
        'first_line': request_line,
    }


def http_response(body: str, connection_close: bool = True) -> bytes:
    conn = 'close' if connection_close else 'keep-alive'
    return (
        f"HTTP/1.1 200 OK\r\n"
        f"Content-Type: text/plain\r\n"
        f"Content-Length: {len(body.encode('utf-8'))}\r\n"
        f"Connection: {conn}\r\n\r\n"
        f"{body}"
    ).encode('utf-8')


def build_userinfo_body(pin: str, name: str, card: str = '', grp: int = 1) -> str:
    """Cuerpo del comando USERINFO (sin prefijo C:ID:)."""
    fields = [
        f"PIN={pin}",
        f"Name={name}",
        "Pri=0",
        "Passwd=",
        f"Card={card}",
        f"Grp={grp}",
        "TZ=0000000100000000",
        f"VerifyMode={VERIFY_FACE}",
    ]
    return "DATA UPDATE USERINFO " + "\t".join(fields)


def build_userinfo_command(cmd_id: int, pin: str, name: str, card: str = '', grp: int = 1) -> str:
    return f"C:{cmd_id}:{build_userinfo_body(pin, name, card, grp)}"


def build_delete_user_body(pin: str) -> str:
    return f"DATA DELETE USERINFO PIN={pin}"


def build_unlock_door_body(door_id: int = 1) -> str:
    """Abre puerta remotamente (comando CONTROL DEVICE ZKTeco)."""
    return "CONTROL DEVICE 01010101"


def build_query_attlog_body() -> str:
    return "DATA QUERY ATTLOG StartTime=2020-01-01 00:00:00\tEndTime=2099-12-31 23:59:59"


def build_getrequest_response(commands: list[str]) -> str:
    if not commands:
        return 'OK'
    return '\n'.join(commands)


def parse_attlog(body: str) -> list[dict]:
    """
    Parsea ATTLOG del portero.
    Formato: PIN \\t Time \\t Status \\t Verify \\t Workcode \\t Reserved
  Status: 0=entrada, 1=salida (habitual ZKTeco)
    """
    records = []
    verify_map = {
        '0': 'password',
        '1': 'fingerprint',
        '2': 'card',
        '15': 'face',
        '16': 'face',
    }

    for line in body.strip().splitlines():
        line = line.strip()
        if not line or line.startswith('ATTLOG'):
            continue
        parts = line.split('\t')
        if len(parts) < 4:
            continue

        pin = parts[0]
        time_str = parts[1]
        status = parts[2]
        verify = parts[3] if len(parts) > 3 else '0'

        records.append({
            'pin': pin,
            'timestamp': time_str,
            'access_type': 'entry' if status == '0' else 'exit',
            'method': verify_map.get(verify, f'verify_{verify}'),
            'status_code': status,
            'verify_code': verify,
        })

    return records


def parse_operlog(body: str) -> list[dict]:
    records = []
    for line in body.strip().splitlines():
        if not line.startswith('OPLOG'):
            continue
        parts = line.split('\t')
        if len(parts) >= 4:
            records.append({
                'op_code': parts[0],
                'admin': parts[1],
                'timestamp': parts[2],
                'operation': parts[3],
            })
    return records


def cdata_ack_body(table: str, body: str) -> str:
    """Respuesta OK:N segun lineas procesadas en cdata."""
    if not body.strip():
        return 'OK'

    lines = [ln for ln in body.strip().splitlines() if ln.strip()]
    if table.upper() == 'ATTLOG':
        count = sum(1 for ln in lines if not ln.startswith('ATTLOG') and '\t' in ln)
    elif table.upper() == 'OPERLOG':
        count = sum(1 for ln in lines if ln.startswith('OPLOG'))
    else:
        count = len(lines)

    return f'OK:{max(count, 1)}' if count else 'OK'
