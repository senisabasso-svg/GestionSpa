#!/usr/bin/env python3
"""
Servidor TCP para Control de Acceso - Portero Biométrico SPA
Protocolo: PUSH (conexión persistente) + captura cruda ADMS/ZKTeco
Escucha: 0.0.0.0:8081
"""

import socket
import json
import threading
import logging
import sys
import os
import uuid
from datetime import datetime
from database import Database
from config import (
    SERVER_HOST, SERVER_PORT, MAX_CONNECTIONS, BUFFER_SIZE,
    Colors, LOG_DIR, RAW_LOG_DIR, SESSION_LOG_DIR
)
from zkteco_protocol import (
    parse_http_request, http_response, build_getrequest_response,
    parse_attlog, parse_operlog, parse_userinfo, looks_like_user_line, cdata_ack_body, DEFAULT_DEVICE_SN,
)

# ===== UTF-8 en consola Windows =====
if sys.platform == 'win32':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

# ===== CONFIGURACIÓN DE LOGGING =====
for d in (LOG_DIR, RAW_LOG_DIR, SESSION_LOG_DIR):
    os.makedirs(d, exist_ok=True)

logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(os.path.join(LOG_DIR, 'portero_server.log'), encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

raw_logger = logging.getLogger('raw_traffic')
raw_logger.setLevel(logging.DEBUG)
raw_handler = logging.FileHandler(os.path.join(RAW_LOG_DIR, 'raw_traffic.log'), encoding='utf-8')
raw_handler.setFormatter(logging.Formatter('%(asctime)s - %(message)s'))
raw_logger.addHandler(raw_handler)
raw_logger.propagate = False


def hex_dump(data: bytes, width=16) -> str:
    lines = []
    for i in range(0, len(data), width):
        chunk = data[i:i + width]
        hex_part = ' '.join(f'{b:02x}' for b in chunk)
        ascii_part = ''.join(chr(b) if 32 <= b < 127 else '.' for b in chunk)
        lines.append(f'{i:08x}  {hex_part:<{width * 3}}  {ascii_part}')
    return '\n'.join(lines)


def try_decode(data: bytes, encoding: str) -> str:
    try:
        return data.decode(encoding)
    except Exception:
        return ''


def detect_protocol_hint(data: bytes) -> str:
    if not data:
        return 'empty'
    if data.startswith(b'GET ') or data.startswith(b'POST ') or data.startswith(b'PUT '):
        return 'http_request'
    if data.startswith(b'HTTP/'):
        return 'http_response'
    if data.startswith(b'{') or data.startswith(b'['):
        return 'json_maybe'
    if b'\x50\x4f\x53\x54' in data[:20] or b'iclock' in data or b'/cdata' in data:
        return 'zkteco_adms'
    if b'\x00' in data[:50]:
        return 'binary'
    try:
        json.loads(data.decode('utf-8'))
        return 'json'
    except Exception:
        pass
    return 'unknown'


def analyze_packet(data: bytes) -> dict:
    utf8 = try_decode(data, 'utf-8')
    latin1 = try_decode(data, 'latin-1')
    gbk = try_decode(data, 'gbk')
    ascii_preview = ''.join(chr(b) if 32 <= b < 127 else '.' for b in data)

    return {
        'byte_length': len(data),
        'hex': data.hex(),
        'hex_dump': hex_dump(data),
        'repr': repr(data),
        'utf8': utf8,
        'latin1': latin1,
        'gbk': gbk,
        'ascii_preview': ascii_preview,
        'protocol_hint': detect_protocol_hint(data),
    }


def build_zkteco_response(data: bytes, db: Database) -> tuple[bytes | None, str]:
    """
    Procesa peticion HTTP ZKTeco y devuelve (respuesta_bytes, nota_log).
    """
    req = parse_http_request(data)
    if not req:
        return None, ''

    path = req['path']
    sn = req['sn'] or DEFAULT_DEVICE_SN
    logger.info(f"ZKTeco {req['method']} {path} SN={sn} table={req.get('table', '')}")

    # Ping
    if '/iclock/ping' in path:
        return http_response('OK'), 'ZKTeco ping OK'

    # Dispositivo pide comandos pendientes
    if '/iclock/getrequest' in path:
        pending = db.get_pending_commands(sn)
        if pending:
            commands = [row['command_text'] for row in pending]
            db.mark_commands_sent([row['id'] for row in pending])
            body = build_getrequest_response(commands)
            logger.info(f"Enviando {len(commands)} comando(s) a SN={sn}: {body[:200]}")
            print(f"""
{Colors.OKGREEN}COMANDOS ENVIADOS AL PORTERO (SN={sn})
{body[:500]}
{Colors.ENDC}""")
            return http_response(body), f'getrequest -> {len(commands)} comando(s)'

        return http_response('OK'), 'getrequest sin comandos'

    # Dispositivo envia datos
    if '/iclock/cdata' in path and req['method'] == 'POST':
        table = req.get('table', '').upper()
        body = req.get('body', '')
        db.update_terminal_by_sn(sn)

        if table == 'ATTLOG':
            for rec in parse_attlog(body):
                user = db.get_user(rec['pin'])
                user_name = user['first_name'] if user else rec['pin']
                access_event = {
                    'terminal_id': sn,
                    'user_id': rec['pin'],
                    'user_name': user_name,
                    'access_type': rec['access_type'],
                    'method': rec['method'],
                    'confidence': None,
                    'temperature': None,
                    'card_id': None,
                    'mask_detected': False,
                    'timestamp': rec['timestamp'],
                }
                db.log_access(access_event)

                from webhook_client import notify_access
                notify_access({
                    'device_sn': sn,
                    'pin': rec['pin'],
                    'nombre': user_name,
                    'access_type': rec['access_type'],
                    'method': rec['method'],
                    'timestamp': rec['timestamp'],
                })

                print(f"""
{Colors.OKGREEN}FICHAJE RECIBIDO
  PIN:    {rec['pin']}
  Nombre: {user_name}
  Hora:   {rec['timestamp']}
  Tipo:   {rec['access_type']}
  Metodo: {rec['method']}
{Colors.ENDC}""")

        elif table == 'OPERLOG':
            ops = parse_operlog(body)
            if ops:
                logger.info(f"OPERLOG SN={sn}: {len(ops)} operaciones")

        elif table == 'USERINFO':
            users = parse_userinfo(body)
            if users:
                count = db.upsert_device_users(sn, users)
                logger.info(f"USERINFO SN={sn}: lote +{len(users)} → total {count}")
                print(f"""
{Colors.OKGREEN}USUARIOS RECIBIDOS DEL PORTERO (SN={sn})
  Lote: {len(users)} · Total acumulado: {count}
  Ejemplo: {users[0].get('pin')} — {users[0].get('name')}
{Colors.ENDC}""")
            else:
                logger.warning(f"USERINFO SN={sn}: body sin usuarios parseables ({len(body)} chars)")

        elif table == 'OPTIONS':
            logger.info(f"Options recibidas SN={sn} ({len(body)} chars)")

        ack = cdata_ack_body(table, body)
        return http_response(ack), f'cdata {table} -> {ack}'

    # GET cdata inicial (handshake)
    if '/iclock/cdata' in path and req['method'] == 'GET':
        return http_response('OK'), 'cdata GET OK'

    # Cualquier otra peticion HTTP del dispositivo
    return http_response('OK'), 'HTTP generico OK'


class RawSessionLogger:
    """Log de sesion por conexion: archivo + BD + consola."""

    def __init__(self, db: Database, session_id: str, addr):
        self.db = db
        self.session_id = session_id
        self.addr = addr
        self.session_file = os.path.join(
            SESSION_LOG_DIR,
            f"{session_id}_{addr[0]}_{addr[1]}.log"
        )
        self._lock = threading.Lock()
        self._write_header()

    def _write_header(self):
        header = (
            f"{'=' * 70}\n"
            f"SESION: {self.session_id}\n"
            f"ORIGEN: {self.addr[0]}:{self.addr[1]}\n"
            f"INICIO: {datetime.utcnow().isoformat()}Z\n"
            f"{'=' * 70}\n"
        )
        with self._lock:
            with open(self.session_file, 'a', encoding='utf-8') as f:
                f.write(header)

    def log_packet(self, data: bytes, direction: str = 'in', notes: str = ''):
        analysis = analyze_packet(data)
        timestamp = datetime.utcnow().isoformat() + 'Z'

        block = (
            f"\n[{timestamp}] {direction.upper()} ({analysis['byte_length']} bytes)"
            f" protocol={analysis['protocol_hint']}\n"
        )
        if notes:
            block += f"NOTAS: {notes}\n"
        block += f"HEX:\n{analysis['hex_dump']}\n"
        block += f"UTF-8: {analysis['utf8'][:2000]}\n"
        block += f"LATIN-1: {analysis['latin1'][:2000]}\n"
        if analysis['gbk']:
            block += f"GBK: {analysis['gbk'][:2000]}\n"
        block += f"REPR: {analysis['repr'][:2000]}\n"
        block += '-' * 70 + '\n'

        with self._lock:
            with open(self.session_file, 'a', encoding='utf-8') as f:
                f.write(block)

        raw_logger.info(
            f"[{self.session_id}] {direction} {self.addr[0]}:{self.addr[1]} "
            f"{analysis['byte_length']}b {analysis['protocol_hint']} | "
            f"{analysis['ascii_preview'][:120]}"
        )

        self.db.log_raw_packet({
            'session_id': self.session_id,
            'direction': direction,
            'remote_ip': self.addr[0],
            'remote_port': self.addr[1],
            'byte_length': analysis['byte_length'],
            'hex_data': analysis['hex'],
            'text_utf8': analysis['utf8'][:4000],
            'text_latin1': analysis['latin1'][:4000],
            'ascii_preview': analysis['ascii_preview'][:500],
            'protocol_hint': analysis['protocol_hint'],
            'notes': notes,
        })

        print(f"""
{Colors.OKCYAN}--- PAQUETE {direction.upper()} ({analysis['byte_length']} bytes) ---
  Sesion:    {self.session_id}
  Protocolo: {analysis['protocol_hint']}
  Preview:   {analysis['ascii_preview'][:200]}
  Archivo:   {self.session_file}
{Colors.ENDC}""")


class DoorAccessServer:
    """Servidor TCP para recibir eventos del portero biométrico"""

    def __init__(self, host=SERVER_HOST, port=SERVER_PORT):
        self.host = host
        self.port = port
        self.server_socket = None
        self.active_connections = {}
        self.db = Database()
        self.running = False

    def start(self):
        """Inicia el servidor TCP"""
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

        try:
            self.server_socket.bind((self.host, self.port))
            self.server_socket.listen(MAX_CONNECTIONS)
            self.running = True

            print(f"""
{Colors.HEADER}╔═══════════════════════════════════════════════════════════╗{Colors.ENDC}
{Colors.HEADER}║        SERVIDOR CONTROL DE ACCESO - PORTERO SPA           ║{Colors.ENDC}
{Colors.HEADER}╚═══════════════════════════════════════════════════════════╝{Colors.ENDC}

{Colors.OKGREEN}Servidor iniciado correctamente{Colors.ENDC}
{Colors.OKBLUE}Direccion: {self.host}:{self.port}{Colors.ENDC}
{Colors.OKBLUE}BD: {self.db.db_path}{Colors.ENDC}
{Colors.OKBLUE}Logs: {LOG_DIR}/{Colors.ENDC}
{Colors.OKBLUE}Raw:  {RAW_LOG_DIR}/{Colors.ENDC}
{Colors.OKBLUE}Sesiones: {SESSION_LOG_DIR}/{Colors.ENDC}

{Colors.WARNING}MODO ZKTECO ACTIVO - cola de comandos + captura cruda{Colors.ENDC}
{Colors.WARNING}Esperando conexiones de dispositivos...{Colors.ENDC}
{Colors.BOLD}(Presiona Ctrl+C para detener){Colors.ENDC}
            """)

            logger.info(f"Servidor escuchando en {self.host}:{self.port} [MODO CAPTURA TOTAL]")

            while self.running:
                try:
                    client_socket, addr = self.server_socket.accept()
                    logger.info(f"Conexion entrante de {addr[0]}:{addr[1]}")

                    thread = threading.Thread(
                        target=self.handle_client,
                        args=(client_socket, addr),
                        daemon=True
                    )
                    thread.start()

                except KeyboardInterrupt:
                    self.stop()

        except OSError as e:
            logger.error(f"Error al iniciar servidor: {e}")
            winerr = getattr(e, 'winerror', None)
            if e.errno in (48, 98) or winerr in (10013, 10048):
                print(f"\n{Colors.FAIL}Error: No se pudo abrir el puerto TCP {self.port}{Colors.ENDC}")
                print(f"{Colors.WARNING}Causas comunes en Windows:{Colors.ENDC}")
                print(f"  - Puerto reservado por Hyper-V / excluidos")
                print(f"  - Otro programa usando el puerto")
                print(f"  - Falta permiso (proba como Administrador)")
                print(f"{Colors.WARNING}Solucion rapida: en el panel cambia Puerto TCP a 9077,{Colors.ENDC}")
                print(f"{Colors.WARNING}guarda, reinicia el servicio y poné 9077 en el Cloud del ZKTeco.{Colors.ENDC}")
                print(f"{Colors.WARNING}Diagnostico: netstat -ano | findstr :{self.port}{Colors.ENDC}")
                print(f"{Colors.WARNING}Rangos reservados: netsh interface ipv4 show excludedportrange protocol=tcp{Colors.ENDC}")
            # No mates todo el proceso: la REST + agente pull pueden seguir.
            # (run_all arranca TCP en hilo daemon; si hacemos sys.exit acá corta todo)
            return

    def handle_client(self, client_socket, addr):
        """Maneja la conexion de un dispositivo terminal"""
        terminal_id = None
        session_id = datetime.utcnow().strftime('%Y%m%d_%H%M%S') + '_' + uuid.uuid4().hex[:8]
        session = RawSessionLogger(self.db, session_id, addr)
        buffer = b''
        device_sn = DEFAULT_DEVICE_SN
        session_user_count = 0

        def _ingest_users(users: list[dict]) -> None:
            nonlocal session_user_count
            if not users:
                return
            total = self.db.upsert_device_users(device_sn, users)
            session_user_count += len(users)
            logger.info(
                "USER dump SN=%s: +%s (sesión +%s) total BD=%s ej=%s %s",
                device_sn, len(users), session_user_count, total,
                users[0].get('pin'), users[0].get('name'),
            )

        try:
            client_socket.settimeout(300)

            print(f"""
{Colors.OKGREEN}NUEVA CONEXION
  IP:      {addr[0]}:{addr[1]}
  Sesion:  {session_id}
  Log:     {session.session_file}
{Colors.ENDC}""")

            while self.running:
                try:
                    data = client_socket.recv(BUFFER_SIZE)

                    if not data:
                        logger.warning(f"Conexion cerrada por {addr[0]}:{addr[1]}")
                        session.log_packet(b'', 'in', notes='conexion cerrada por cliente')
                        break

                    session.log_packet(data, 'in')
                    buffer += data

                    # --- Protocolo HTTP ZKTeco / ADMS ---
                    zk_response, zk_note = build_zkteco_response(buffer, self.db)
                    if zk_response:
                        client_socket.send(zk_response)
                        session.log_packet(zk_response, 'out', notes=zk_note)
                        buffer = b''
                        continue

                    # --- Líneas USER PIN=... (dump sin HTTP; NO enviar ACK o el equipo corta el dump) ---
                    text_buffer = buffer.decode('utf-8', errors='ignore')
                    if looks_like_user_line(text_buffer) and '\n' not in text_buffer:
                        users = parse_userinfo(text_buffer)
                        if users:
                            _ingest_users(users)
                            buffer = b''
                            continue

                    while '\n' in text_buffer:
                        message_str, text_buffer = text_buffer.split('\n', 1)
                        if not message_str.strip():
                            continue

                        if looks_like_user_line(message_str):
                            users = parse_userinfo(message_str)
                            if users:
                                _ingest_users(users)
                                continue

                        try:
                            message = json.loads(message_str)
                            terminal_id = message.get('terminalId')

                            if terminal_id:
                                self.active_connections[terminal_id] = {
                                    'ip': addr[0],
                                    'port': addr[1],
                                    'last_seen': datetime.utcnow().isoformat(),
                                    'session_id': session_id,
                                }

                            self.process_event(message, addr)

                            ack_response = {
                                "terminalId": terminal_id,
                                "messageId": message.get('messageId', 'unknown'),
                                "status": "received",
                                "acknowledgment": "ok",
                                "timestamp": datetime.utcnow().isoformat() + "Z"
                            }
                            ack_bytes = (json.dumps(ack_response) + '\n').encode('utf-8')
                            client_socket.send(ack_bytes)
                            session.log_packet(ack_bytes, 'out', notes='ACK JSON')

                        except json.JSONDecodeError as e:
                            session.log_packet(
                                message_str.encode('utf-8', errors='replace'),
                                'in',
                                notes=f'linea no-JSON: {e}'
                            )
                            logger.warning(
                                "Linea no reconocida de %s: %s | data=%r",
                                addr[0], e, message_str[:200],
                            )

                    buffer = text_buffer.encode('utf-8', errors='ignore')

                    # Si el buffer crece mucho sin parsear, volcar y limpiar
                    if len(buffer) > 65536:
                        session.log_packet(buffer, 'in', notes='buffer overflow - volcado completo')
                        buffer = b''

                except socket.timeout:
                    logger.warning(f"Timeout de {addr[0]} - cerrando conexion")
                    break

        except Exception as e:
            logger.error(f"Error con cliente {addr[0]}: {e}", exc_info=True)

        finally:
            if session_user_count:
                total = self.db.count_device_users(device_sn)
                logger.info(
                    "Fin conexión USER dump SN=%s: recibidos en sesión=%s · total BD=%s",
                    device_sn, session_user_count, total,
                )
                print(f"""
{Colors.OKGREEN}USUARIOS DEL PORTERO (conexión cerrada)
  SN: {device_sn}
  En esta conexión: {session_user_count}
  Total acumulado: {total}
{Colors.ENDC}""")

            if terminal_id and terminal_id in self.active_connections:
                del self.active_connections[terminal_id]
                logger.info(f"Terminal {terminal_id} desconectada")

            with open(session.session_file, 'a', encoding='utf-8') as f:
                f.write(f"\nFIN SESION: {datetime.utcnow().isoformat()}Z\n")

            client_socket.close()

    def process_event(self, message, addr):
        """Procesa el evento recibido del dispositivo"""
        event_type = message.get('type')
        data = message.get('data', {})
        terminal_id = message.get('terminalId')

        logger.info(f"[TERMINAL {terminal_id}] Evento: {event_type}")
        raw_logger.info(f"JSON EVENTO: {json.dumps(message, ensure_ascii=False)}")

        if event_type == 'access':
            self.handle_access_event(terminal_id, data)
        elif event_type == 'user_registration':
            self.handle_user_registration(terminal_id, data)
        elif event_type == 'heartbeat':
            self.handle_heartbeat(terminal_id, data)
        elif event_type == 'alert':
            self.handle_alert(terminal_id, data)
        else:
            logger.warning(f"Evento desconocido: {event_type} | msg={message}")

    def handle_access_event(self, terminal_id, data):
        """Registra un evento de acceso"""
        print(f"""
{Colors.OKGREEN}ACCESO PERMITIDO
  Terminal:  {terminal_id}
  Usuario:   {data.get('userName', 'N/A')}
  ID:        {data.get('userId', 'N/A')}
  Tipo:      {data.get('accessType', 'N/A')}
  Metodo:    {data.get('method', 'N/A')}
  Confianza: {data.get('confidence', 0)*100:.1f}%
  Temp:      {data.get('temperature', 'N/A')} C
{Colors.ENDC}""")

        self.db.log_access({
            'terminal_id': terminal_id,
            'user_id': data.get('userId'),
            'user_name': data.get('userName'),
            'access_type': data.get('accessType'),
            'method': data.get('method'),
            'confidence': data.get('confidence'),
            'temperature': data.get('temperature'),
            'card_id': data.get('cardId'),
            'mask_detected': data.get('maskDetected', False),
            'timestamp': data.get('timestamp')
        })

    def handle_user_registration(self, terminal_id, data):
        """Registra un nuevo usuario"""
        print(f"""
{Colors.OKGREEN}NUEVO USUARIO REGISTRADO
  Terminal: {terminal_id}
  Nombre:   {data.get('firstName', '')} {data.get('lastName', '')}
  Email:    {data.get('email', 'N/A')}
{Colors.ENDC}""")

        self.db.register_user({
            'user_id': data.get('userId'),
            'first_name': data.get('firstName'),
            'last_name': data.get('lastName'),
            'email': data.get('email'),
            'phone': data.get('phone'),
            'membership_type': data.get('membershipType'),
            'access_level': data.get('accessLevel'),
            'card_id': data.get('cardId'),
            'valid_from': data.get('validFrom'),
            'valid_until': data.get('validUntil'),
            'status': 'active'
        })

    def handle_heartbeat(self, terminal_id, data):
        """Registra el heartbeat de la terminal"""
        logger.info(f"Heartbeat Terminal {terminal_id}")
        self.db.update_terminal_heartbeat(terminal_id, {
            'timestamp': data.get('timestamp', datetime.utcnow().isoformat())
        })

    def handle_alert(self, terminal_id, data):
        """Maneja una alerta del dispositivo"""
        severity = data.get('severity', 'medium')
        print(f"""
{Colors.WARNING}ALERTA DEL DISPOSITIVO
  Terminal:  {terminal_id}
  Tipo:      {data.get('alertType', 'N/A')}
  Severidad: {severity}
  Mensaje:   {data.get('message', 'N/A')}
{Colors.ENDC}""")

        self.db.log_alert({
            'terminal_id': terminal_id,
            'alert_type': data.get('alertType'),
            'severity': severity,
            'message': data.get('message'),
            'timestamp': data.get('timestamp')
        })

    def get_stats(self):
        """Retorna estadísticas del servidor"""
        return {
            'active_terminals': len(self.active_connections),
            'database_stats': self.db.get_stats()
        }

    def stop(self):
        """Detiene el servidor"""
        self.running = False
        if self.server_socket:
            self.server_socket.close()

        print(f"""
{Colors.WARNING}{'=' * 60}{Colors.ENDC}
{Colors.FAIL}Servidor detenido{Colors.ENDC}
{Colors.WARNING}{'=' * 60}{Colors.ENDC}
        """)
        logger.info("Servidor detenido")


def main():
    """Punto de entrada principal"""
    try:
        server = DoorAccessServer(SERVER_HOST, SERVER_PORT)
        server.start()

    except KeyboardInterrupt:
        logger.info("Interrupcion del usuario")
        if 'server' in locals():
            server.stop()
        sys.exit(0)

    except Exception as e:
        logger.critical(f"Error critico: {e}", exc_info=True)
        sys.exit(1)


if __name__ == '__main__':
    main()
