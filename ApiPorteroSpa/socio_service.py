"""
Logica de negocio para socios (compartida entre CLI y API REST).
"""
from datetime import datetime, timedelta, timezone

from database import Database
from zkteco_protocol import (
    build_userinfo_body,
    build_delete_user_body,
    build_unlock_door_body,
    build_query_attlog_body,
    build_query_userinfo_body,
    DEFAULT_DEVICE_SN,
)


def _now():
    return datetime.now(timezone.utc)


def crear_socio(
    db: Database,
    nombre: str,
    cedula: str,
    celular: str = '',
    email: str = '',
    device_sn: str = DEFAULT_DEVICE_SN,
    membership_type: str = 'socio',
    access_level: int = 1,
    valid_days: int = 365,
) -> dict:
    pin = str(cedula).strip()
    name = nombre.strip()
    now = _now()
    valid_from = now.strftime('%Y-%m-%d')
    valid_until = (now + timedelta(days=valid_days)).strftime('%Y-%m-%d')

    db.register_user({
        'user_id': pin,
        'first_name': name,
        'last_name': '',
        'email': email,
        'phone': celular,
        'membership_type': membership_type,
        'access_level': access_level,
        'card_id': pin,
        'valid_from': valid_from,
        'valid_until': valid_until,
        'status': 'active',
    })

    cmd_id = db.queue_device_command(
        device_sn,
        build_userinfo_body(pin=pin, name=name, card=pin),
    )

    return {
        'pin': pin,
        'nombre': name,
        'celular': celular,
        'email': email,
        'device_sn': device_sn,
        'command_id': cmd_id,
        'valid_from': valid_from,
        'valid_until': valid_until,
        'status': 'queued',
        'message': 'Socio guardado y encolado para el portero (~10 seg)',
    }


def actualizar_socio(
    db: Database,
    pin: str,
    nombre: str = None,
    celular: str = None,
    email: str = None,
    device_sn: str = DEFAULT_DEVICE_SN,
    status: str = None,
) -> dict:
    user = db.get_user(pin)
    if not user:
        raise ValueError(f'Socio {pin} no encontrado')

    name = nombre or user['first_name']
    phone = celular if celular is not None else user['phone']
    mail = email if email is not None else user['email']
    user_status = status or user['status']

    db.register_user({
        'user_id': pin,
        'first_name': name,
        'last_name': user.get('last_name', ''),
        'email': mail,
        'phone': phone,
        'membership_type': user.get('membership_type', 'socio'),
        'access_level': user.get('access_level', 1),
        'card_id': pin,
        'valid_from': user.get('valid_from'),
        'valid_until': user.get('valid_until'),
        'status': user_status,
    })

    cmd_id = None
    if user_status == 'active':
        cmd_id = db.queue_device_command(
            device_sn,
            build_userinfo_body(pin=pin, name=name, card=pin),
        )

    return {
        'pin': pin,
        'nombre': name,
        'command_id': cmd_id,
        'status': user_status,
    }


def eliminar_socio(db: Database, pin: str, device_sn: str = DEFAULT_DEVICE_SN) -> dict:
    pin = str(pin).strip()
    cmd_id = db.queue_device_command(device_sn, build_delete_user_body(pin))
    user = db.get_user(pin)
    if user:
        db.set_user_status(pin, 'inactive')

    return {
        'pin': pin,
        'command_id': cmd_id,
        'status': 'deleted_queued',
        'message': 'Baja encolada para el portero',
    }


def abrir_puerta(db: Database, device_sn: str = DEFAULT_DEVICE_SN) -> dict:
    cmd_id = db.queue_device_command(device_sn, build_unlock_door_body())
    return {'device_sn': device_sn, 'command_id': cmd_id, 'action': 'unlock_door'}


def sincronizar_fichajes(db: Database, device_sn: str = DEFAULT_DEVICE_SN) -> dict:
    cmd_id = db.queue_device_command(device_sn, build_query_attlog_body())
    return {'device_sn': device_sn, 'command_id': cmd_id, 'action': 'query_attlog'}


def consultar_usuarios_equipo(db: Database, device_sn: str = DEFAULT_DEVICE_SN) -> dict:
    """Limpia snapshot previo y encola DATA QUERY USERINFO al equipo."""
    db.clear_device_users(device_sn)  # también limpia flag de cancelación
    cmd_id = db.queue_device_command(device_sn, build_query_userinfo_body())
    return {
        'device_sn': device_sn,
        'command_id': cmd_id,
        'action': 'query_userinfo',
        'message': 'Consulta encolada. El equipo enviará los usuarios en el próximo ciclo (~10–90 s).',
    }


def cancelar_consulta_usuarios_equipo(db: Database, device_sn: str = DEFAULT_DEVICE_SN) -> dict:
    """Corta la espera del export y anula QUERY USERINFO pendientes. No afecta sync de socios."""
    return db.cancel_userinfo_query(device_sn)


def encolar_comando(db: Database, device_sn: str, command_body: str) -> dict:
    cmd_id = db.queue_device_command(device_sn, command_body)
    return {'device_sn': device_sn, 'command_id': cmd_id, 'command': command_body}
