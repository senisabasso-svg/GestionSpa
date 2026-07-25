"""
Gestión de base de datos SQLite para el portero
"""
import sqlite3
import os
import logging
from datetime import datetime
from config import DB_PATH

logger = logging.getLogger(__name__)

class Database:
    def __init__(self, db_path=DB_PATH):
        self.db_path = db_path
        self.init_db()

    def get_connection(self):
        """Obtiene conexión a la BD"""
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        return conn

    def init_db(self):
        """Inicializa las tablas si no existen"""  
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            # Tabla: Usuarios/Socios
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id VARCHAR(50) UNIQUE NOT NULL,
                    first_name VARCHAR(100),
                    last_name VARCHAR(100),
                    email VARCHAR(100),
                    phone VARCHAR(20),
                    membership_type VARCHAR(50),
                    access_level INTEGER DEFAULT 1,
                    card_id VARCHAR(50),
                    valid_from DATE,
                    valid_until DATE,
                    status VARCHAR(20) DEFAULT 'active',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            # Tabla: Registros de Acceso
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS access_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    terminal_id INTEGER NOT NULL,
                    user_id VARCHAR(50),
                    user_name VARCHAR(100),
                    access_type VARCHAR(20),
                    method VARCHAR(50),
                    confidence REAL,
                    temperature REAL,
                    card_id VARCHAR(50),
                    mask_detected BOOLEAN DEFAULT 0,
                    status VARCHAR(20) DEFAULT 'success',
                    event_timestamp DATETIME NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            # Tabla: Terminales
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS terminals (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    terminal_id INTEGER UNIQUE NOT NULL,
                    name VARCHAR(100),
                    location VARCHAR(200),
                    ip_address VARCHAR(50),
                    last_heartbeat TIMESTAMP,
                    status VARCHAR(20) DEFAULT 'offline',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            # Tabla: Alertas
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS alerts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    terminal_id INTEGER NOT NULL,
                    alert_type VARCHAR(50),
                    severity VARCHAR(20),
                    message TEXT,
                    resolved BOOLEAN DEFAULT 0,
                    event_timestamp DATETIME NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            # Tabla: Trafico crudo (debug / analisis de protocolo)
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS raw_packets (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id VARCHAR(64) NOT NULL,
                    direction VARCHAR(10) NOT NULL,
                    remote_ip VARCHAR(50),
                    remote_port INTEGER,
                    byte_length INTEGER NOT NULL,
                    hex_data TEXT,
                    text_utf8 TEXT,
                    text_latin1 TEXT,
                    ascii_preview TEXT,
                    protocol_hint VARCHAR(50),
                    notes TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            # Cola de comandos para dispositivos ZKTeco (por serial SN)
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS device_commands (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    device_sn VARCHAR(50) NOT NULL,
                    command_id INTEGER NOT NULL,
                    command_text TEXT NOT NULL,
                    status VARCHAR(20) DEFAULT 'pending',
                    sent_at TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')

            conn.commit()
            logger.info(f"✅ Base de datos inicializada: {self.db_path}")

        except Exception as e:
            logger.error(f"❌ Error inicializando BD: {e}")
            raise

        finally:
            conn.close()

    # ===== MÉTODOS DE ACCESO =====

    def log_access(self, event_data):
        """Registra un evento de acceso"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('''
                INSERT INTO access_logs
                (terminal_id, user_id, user_name, access_type, method, confidence,
                 temperature, card_id, mask_detected, event_timestamp)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                event_data.get('terminal_id'),
                event_data.get('user_id'),
                event_data.get('user_name'),
                event_data.get('access_type'),
                event_data.get('method'),
                event_data.get('confidence'),
                event_data.get('temperature'),
                event_data.get('card_id'),
                event_data.get('mask_detected', 0),
                event_data.get('timestamp', datetime.utcnow().isoformat())
            ))

            conn.commit()
            logger.info(f"📝 Acceso registrado: {event_data.get('user_name')}")
            return cursor.lastrowid

        except Exception as e:
            logger.error(f"❌ Error registrando acceso: {e}")
            return None

        finally:
            conn.close()

    def register_user(self, user_data):
        """Registra un nuevo usuario"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('''
                INSERT OR REPLACE INTO users
                (user_id, first_name, last_name, email, phone, membership_type,
                 access_level, card_id, valid_from, valid_until, status)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                user_data.get('user_id'),
                user_data.get('first_name'),
                user_data.get('last_name'),
                user_data.get('email'),
                user_data.get('phone'),
                user_data.get('membership_type'),
                user_data.get('access_level', 1),
                user_data.get('card_id'),
                user_data.get('valid_from'),
                user_data.get('valid_until'),
                user_data.get('status', 'active')
            ))

            conn.commit()
            logger.info(f"👤 Usuario registrado: {user_data.get('first_name')} {user_data.get('last_name')}")
            return cursor.lastrowid

        except Exception as e:
            logger.error(f"❌ Error registrando usuario: {e}")
            return None

        finally:
            conn.close()

    def update_terminal_heartbeat(self, terminal_id, data):
        """Actualiza el heartbeat de una terminal"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            # Primero verifica si la terminal existe
            cursor.execute('SELECT id FROM terminals WHERE terminal_id = ?', (terminal_id,))
            exists = cursor.fetchone()

            if exists:
                cursor.execute('''
                    UPDATE terminals
                    SET last_heartbeat = ?, status = 'online', updated_at = CURRENT_TIMESTAMP
                    WHERE terminal_id = ?
                ''', (data.get('timestamp', datetime.utcnow().isoformat()), terminal_id))
            else:
                cursor.execute('''
                    INSERT INTO terminals (terminal_id, status, last_heartbeat)
                    VALUES (?, 'online', ?)
                ''', (terminal_id, data.get('timestamp', datetime.utcnow().isoformat())))

            conn.commit()

        except Exception as e:
            logger.error(f"❌ Error actualizando heartbeat: {e}")

        finally:
            conn.close()

    def log_alert(self, alert_data):
        """Registra una alerta"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('''
                INSERT INTO alerts
                (terminal_id, alert_type, severity, message, event_timestamp)
                VALUES (?, ?, ?, ?, ?)
            ''', (
                alert_data.get('terminal_id'),
                alert_data.get('alert_type'),
                alert_data.get('severity'),
                alert_data.get('message'),
                alert_data.get('timestamp', datetime.utcnow().isoformat())
            ))

            conn.commit()
            logger.warning(f"⚠️ Alerta: {alert_data.get('alert_type')}")

        except Exception as e:
            logger.error(f"❌ Error registrando alerta: {e}")

        finally:
            conn.close()

    def get_user(self, user_id):
        """Obtiene datos de un usuario"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('SELECT * FROM users WHERE user_id = ?', (user_id,))
            row = cursor.fetchone()
            return dict(row) if row else None

        finally:
            conn.close()

    def get_users(self, limit=100, status=None):
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            if status:
                cursor.execute(
                    'SELECT * FROM users WHERE status = ? ORDER BY created_at DESC LIMIT ?',
                    (status, limit)
                )
            else:
                cursor.execute('SELECT * FROM users ORDER BY created_at DESC LIMIT ?', (limit,))
            return [dict(r) for r in cursor.fetchall()]
        finally:
            conn.close()

    def set_user_status(self, user_id, status):
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            cursor.execute(
                'UPDATE users SET status = ?, updated_at = CURRENT_TIMESTAMP WHERE user_id = ?',
                (status, user_id)
            )
            conn.commit()
            return cursor.rowcount > 0
        finally:
            conn.close()

    def get_access_logs(self, limit=100, pin=None, since=None):
        """Obtiene ultimos registros de acceso"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            query = 'SELECT * FROM access_logs WHERE 1=1'
            params = []
            if pin:
                query += ' AND user_id = ?'
                params.append(pin)
            if since:
                query += ' AND created_at >= ?'
                params.append(since)
            query += ' ORDER BY created_at DESC LIMIT ?'
            params.append(limit)

            cursor.execute(query, params)
            return [dict(r) for r in cursor.fetchall()]

        finally:
            conn.close()

    def get_terminals(self):
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            cursor.execute('SELECT * FROM terminals ORDER BY updated_at DESC')
            return [dict(r) for r in cursor.fetchall()]
        finally:
            conn.close()

    def get_terminal(self, terminal_id):
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            cursor.execute('SELECT * FROM terminals WHERE terminal_id = ?', (terminal_id,))
            row = cursor.fetchone()
            return dict(row) if row else None
        finally:
            conn.close()

    def log_raw_packet(self, packet_data):
        """Guarda un paquete crudo para analisis de protocolo"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('''
                INSERT INTO raw_packets
                (session_id, direction, remote_ip, remote_port, byte_length,
                 hex_data, text_utf8, text_latin1, ascii_preview, protocol_hint, notes)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                packet_data.get('session_id'),
                packet_data.get('direction', 'in'),
                packet_data.get('remote_ip'),
                packet_data.get('remote_port'),
                packet_data.get('byte_length', 0),
                packet_data.get('hex_data'),
                packet_data.get('text_utf8'),
                packet_data.get('text_latin1'),
                packet_data.get('ascii_preview'),
                packet_data.get('protocol_hint'),
                packet_data.get('notes'),
            ))
            conn.commit()
            return cursor.lastrowid

        except Exception as e:
            logger.error(f"Error registrando paquete crudo: {e}")
            return None

        finally:
            conn.close()

    def get_raw_packets(self, limit=50):
        """Obtiene ultimos paquetes crudos"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            cursor.execute('''
                SELECT * FROM raw_packets
                ORDER BY created_at DESC
                LIMIT ?
            ''', (limit,))
            return cursor.fetchall()

        finally:
            conn.close()

    # ===== COMANDOS ZKTECO =====

    def _next_command_id(self):
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            cursor.execute('SELECT COALESCE(MAX(command_id), 100) + 1 FROM device_commands')
            return cursor.fetchone()[0]
        finally:
            conn.close()

    def queue_device_command(self, device_sn: str, command_text: str) -> int:
        """Encola un comando C:ID:... para el dispositivo."""
        conn = self.get_connection()
        cursor = conn.cursor()
        cmd_id = self._next_command_id()
        full_cmd = f"C:{cmd_id}:{command_text}"

        try:
            cursor.execute('''
                INSERT INTO device_commands (device_sn, command_id, command_text, status)
                VALUES (?, ?, ?, 'pending')
            ''', (device_sn, cmd_id, full_cmd))
            conn.commit()
            logger.info(f"Comando encolado SN={device_sn} id={cmd_id}")
            return cmd_id
        finally:
            conn.close()

    def get_pending_commands(self, device_sn: str, limit: int = 5) -> list:
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            cursor.execute('''
                SELECT * FROM device_commands
                WHERE device_sn = ? AND status = 'pending'
                ORDER BY id ASC
                LIMIT ?
            ''', (device_sn, limit))
            return [dict(r) for r in cursor.fetchall()]
        finally:
            conn.close()

    def mark_commands_sent(self, command_ids: list[int]):
        if not command_ids:
            return
        conn = self.get_connection()
        cursor = conn.cursor()
        try:
            placeholders = ','.join('?' * len(command_ids))
            cursor.execute(
                f'''UPDATE device_commands
                    SET status = 'sent', sent_at = CURRENT_TIMESTAMP
                    WHERE id IN ({placeholders})''',
                command_ids
            )
            conn.commit()
        finally:
            conn.close()

    def update_terminal_by_sn(self, device_sn: str, ip_address: str = None):
        """Registra terminal por serial number del equipo."""
        conn = self.get_connection()
        cursor = conn.cursor()
        now = datetime.utcnow().isoformat()
        try:
            cursor.execute('SELECT id FROM terminals WHERE terminal_id = ?', (device_sn,))
            if cursor.fetchone():
                cursor.execute('''
                    UPDATE terminals
                    SET status = 'online', last_heartbeat = ?, updated_at = CURRENT_TIMESTAMP,
                        ip_address = COALESCE(?, ip_address)
                    WHERE terminal_id = ?
                ''', (now, ip_address, device_sn))
            else:
                cursor.execute('''
                    INSERT INTO terminals (terminal_id, name, ip_address, status, last_heartbeat)
                    VALUES (?, ?, ?, 'online', ?)
                ''', (device_sn, f'ZKTeco-{device_sn}', ip_address, now))
            conn.commit()
        finally:
            conn.close()

    def get_stats(self):
        """Obtiene estadísticas generales"""
        conn = self.get_connection()
        cursor = conn.cursor()

        try:
            stats = {}

            cursor.execute('SELECT COUNT(*) FROM users')
            stats['total_users'] = cursor.fetchone()[0]

            cursor.execute('SELECT COUNT(*) FROM access_logs')
            stats['total_accesses'] = cursor.fetchone()[0]

            cursor.execute('SELECT COUNT(*) FROM terminals WHERE status = "online"')
            stats['online_terminals'] = cursor.fetchone()[0]

            cursor.execute('SELECT COUNT(*) FROM alerts WHERE resolved = 0')
            stats['pending_alerts'] = cursor.fetchone()[0]

            cursor.execute('SELECT COUNT(*) FROM raw_packets')
            stats['total_raw_packets'] = cursor.fetchone()[0]

            cursor.execute('SELECT COUNT(*) FROM device_commands WHERE status = "pending"')
            stats['pending_commands'] = cursor.fetchone()[0]

            return stats

        finally:
            conn.close()
