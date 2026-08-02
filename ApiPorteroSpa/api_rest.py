#!/usr/bin/env python3
"""
API REST para que otros backends se conecten al portero.

Autenticacion: header X-API-Key

Endpoints:
  GET  /panel          (UI logs)
  GET  /api/health
  GET  /api/logs       (X-API-Key)
  GET  /api/stats
  GET  /api/socios
  POST /api/socios
  GET  /api/socios/<pin>
  PUT  /api/socios/<pin>
  DELETE /api/socios/<pin>
  GET  /api/accesos
  GET  /api/dispositivos
  GET  /api/dispositivos/<sn>
  POST /api/dispositivos/<sn>/abrir-puerta
  POST /api/dispositivos/<sn>/sincronizar-fichajes
  POST /api/dispositivos/<sn>/consultar-usuarios
  GET  /api/dispositivos/<sn>/usuarios-equipo
  POST /api/comandos
"""
import logging
import os
import time
from functools import wraps

from flask import Flask, request, jsonify, send_from_directory, redirect, Response

from config import API_HOST, API_PORT, API_KEY
from database import Database
from log_buffer import get_lines as get_log_lines, clear as clear_log_buffer
from socio_service import (
    crear_socio, actualizar_socio, eliminar_socio,
    abrir_puerta, sincronizar_fichajes, consultar_usuarios_equipo, encolar_comando,
)
from webhook_client import notify_socio_queued
from zkteco_protocol import DEFAULT_DEVICE_SN

logger = logging.getLogger(__name__)

_STATIC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "static")

app = Flask(__name__, static_folder=_STATIC_DIR, static_url_path='/static')
db = Database()


def require_api_key(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        key = request.headers.get('X-API-Key', '')
        if key != API_KEY:
            return jsonify({'error': 'API key invalida'}), 401
        return f(*args, **kwargs)
    return decorated


@app.route('/api/health', methods=['GET'])
def health():
    return jsonify({
        'status': 'ok',
        'service': 'ApiPorteroSpa',
        'device_default_sn': DEFAULT_DEVICE_SN,
    })


@app.route('/')
def root():
    return redirect('/panel')


@app.route('/panel')
def panel():
    return send_from_directory(_STATIC_DIR, 'panel.html')


@app.route('/api/logs', methods=['GET'])
@require_api_key
def api_logs():
    limit = request.args.get('limit', 300, type=int)
    since = request.args.get('since', 0, type=int)
    return jsonify(get_log_lines(limit=limit, since=since))


@app.route('/api/logs', methods=['DELETE'])
@require_api_key
def api_logs_clear():
    clear_log_buffer()
    return jsonify({'ok': True})


@app.route('/api/stats', methods=['GET'])
@require_api_key
def stats():
    return jsonify(db.get_stats())


@app.route('/api/socios', methods=['GET'])
@require_api_key
def list_socios():
    limit = request.args.get('limit', 100, type=int)
    status = request.args.get('status')
    return jsonify({'socios': db.get_users(limit=limit, status=status)})


@app.route('/api/socios', methods=['POST'])
@require_api_key
def create_socio():
    data = request.get_json(force=True, silent=True) or {}
    required = ['nombre', 'cedula']
    missing = [f for f in required if not data.get(f)]
    if missing:
        return jsonify({'error': f'Campos requeridos: {missing}'}), 400

    try:
        result = crear_socio(
            db,
            nombre=data['nombre'],
            cedula=str(data['cedula']),
            celular=data.get('celular', ''),
            email=data.get('email', ''),
            device_sn=data.get('device_sn', DEFAULT_DEVICE_SN),
            membership_type=data.get('membership_type', 'socio'),
            access_level=data.get('access_level', 1),
            valid_days=data.get('valid_days', 365),
        )
        notify_socio_queued('created', result)
        return jsonify(result), 201
    except Exception as e:
        logger.error(f"Error creando socio: {e}")
        return jsonify({'error': str(e)}), 500


@app.route('/api/socios/<pin>', methods=['GET'])
@require_api_key
def get_socio(pin):
    user = db.get_user(pin)
    if not user:
        return jsonify({'error': 'Socio no encontrado'}), 404
    return jsonify(user)


@app.route('/api/socios/<pin>', methods=['PUT'])
@require_api_key
def update_socio(pin):
    data = request.get_json(force=True, silent=True) or {}
    try:
        result = actualizar_socio(
            db, pin,
            nombre=data.get('nombre'),
            celular=data.get('celular'),
            email=data.get('email'),
            device_sn=data.get('device_sn', DEFAULT_DEVICE_SN),
            status=data.get('status'),
        )
        notify_socio_queued('updated', result)
        return jsonify(result)
    except ValueError as e:
        return jsonify({'error': str(e)}), 404
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/api/socios/<pin>', methods=['DELETE'])
@require_api_key
def delete_socio(pin):
    data = request.get_json(force=True, silent=True) or {}
    try:
        result = eliminar_socio(db, pin, data.get('device_sn', DEFAULT_DEVICE_SN))
        notify_socio_queued('deleted', result)
        return jsonify(result)
    except ValueError as e:
        return jsonify({'error': str(e)}), 404


@app.route('/api/accesos', methods=['GET'])
@require_api_key
def list_accesos():
    limit = request.args.get('limit', 50, type=int)
    pin = request.args.get('pin')
    since = request.args.get('since')
    logs = db.get_access_logs(limit=limit, pin=pin, since=since)
    return jsonify({'accesos': logs, 'total': len(logs)})


@app.route('/api/dispositivos', methods=['GET'])
@require_api_key
def list_dispositivos():
    terminals = db.get_terminals()
    pending = db.get_stats().get('pending_commands', 0)
    return jsonify({
        'dispositivos': terminals,
        'comandos_pendientes': pending,
    })


@app.route('/api/dispositivos/<sn>', methods=['GET'])
@require_api_key
def get_dispositivo(sn):
    terminal = db.get_terminal(sn)
    if not terminal:
        return jsonify({'error': 'Dispositivo no encontrado'}), 404
    pending = db.get_pending_commands(sn)
    return jsonify({
        'dispositivo': terminal,
        'comandos_pendientes': [dict(c) for c in pending],
    })


@app.route('/api/dispositivos/<sn>/abrir-puerta', methods=['POST'])
@require_api_key
def unlock_door(sn):
    result = abrir_puerta(db, sn)
    return jsonify(result)


@app.route('/api/dispositivos/<sn>/sincronizar-fichajes', methods=['POST'])
@require_api_key
def sync_attlog(sn):
    result = sincronizar_fichajes(db, sn)
    return jsonify(result)


@app.route('/api/dispositivos/<sn>/consultar-usuarios', methods=['POST'])
@require_api_key
def query_device_users(sn):
    """Encola DATA QUERY USERINFO al dispositivo."""
    return jsonify(consultar_usuarios_equipo(db, sn))


@app.route('/api/dispositivos/<sn>/usuarios-equipo', methods=['GET'])
@require_api_key
def list_device_users(sn):
    """Snapshot de usuarios leídos del equipo (tras consultar-usuarios)."""
    meta = db.get_device_userinfo_meta(sn) or {}
    users = db.get_device_users(sn)
    return jsonify({
        'device_sn': sn,
        'last_userinfo_at': meta.get('last_userinfo_at'),
        'count': len(users),
        'usuarios': users,
    })


@app.route('/api/dispositivos/<sn>/exportar-usuarios-equipo', methods=['GET'])
@require_api_key
def export_device_users_csv(sn):
    """
    Encola QUERY USERINFO, acumula TODOS los USER del equipo y devuelve CSV.
    Espera a que el conteo se estabilice (sin altas nuevas por idle_seconds).
    Query: wait_seconds (default 150, max 240), idle_seconds (default 12).
    """
    wait_seconds = request.args.get('wait_seconds', 150, type=int)
    wait_seconds = max(30, min(wait_seconds or 150, 240))
    idle_seconds = request.args.get('idle_seconds', 12, type=int)
    idle_seconds = max(6, min(idle_seconds or 12, 40))

    started = time.time()
    consultar_usuarios_equipo(db, sn)

    deadline = started + wait_seconds
    last_count = 0
    stable_since = None
    saw_data = False

    while time.time() < deadline:
        time.sleep(2)
        count = db.count_device_users(sn)
        if count > 0:
            saw_data = True
        if count != last_count:
            last_count = count
            stable_since = time.time()
            logger.info("Export usuarios equipo SN=%s: acumulados=%s (esperando idle)", sn, count)
            continue
        # Conteo estable el tiempo suficiente → dump completo
        if saw_data and stable_since and (time.time() - stable_since) >= idle_seconds:
            logger.info("Export usuarios equipo SN=%s: estable en %s tras %.0fs idle", sn, count, idle_seconds)
            break

    users = db.get_device_users(sn)
    if not users:
        return jsonify({
            'error': (
                'El equipo no envió usuarios a tiempo. '
                'Verificá heartbeat del portero e intentá de nuevo (puede tardar ~2 min).'
            ),
        }), 504

    lines = ['pin;nombre;privilegio;tarjeta;sincronizado']
    for u in users:
        def esc(v):
            s = str(v if v is not None else '')
            if '"' in s or ';' in s or '\n' in s:
                return '"' + s.replace('"', '""') + '"'
            return s
        lines.append(';'.join([
            esc(u.get('pin')),
            esc(u.get('name')),
            esc(u.get('privilege')),
            esc(u.get('card')),
            esc(u.get('synced_at')),
        ]))
    csv_body = '\ufeff' + '\n'.join(lines) + '\n'
    filename = f"usuarios-equipo-{sn}-{time.strftime('%Y%m%d-%H%M%S')}.csv"
    logger.info("Export CSV SN=%s: %s usuario(s)", sn, len(users))
    return Response(
        csv_body,
        mimetype='text/csv; charset=utf-8',
        headers={'Content-Disposition': f'attachment; filename="{filename}"'},
    )


@app.route('/api/comandos', methods=['POST'])
@require_api_key
def queue_command():
    data = request.get_json(force=True, silent=True) or {}
    if not data.get('command'):
        return jsonify({'error': 'Campo command requerido'}), 400
    sn = data.get('device_sn', DEFAULT_DEVICE_SN)
    result = encolar_comando(db, sn, data['command'])
    return jsonify(result), 201


def run_api(host=API_HOST, port=API_PORT, debug=False):
    logging.basicConfig(level=logging.INFO)
    logger.info(f"API REST en http://{host}:{port}")
    # Waitress = WSGI de producción (sin el warning de Flask dev server).
    # Si no está instalado, cae al servidor de desarrollo de Flask.
    if not debug:
        try:
            from waitress import serve
            logger.info("Usando Waitress (producción)")
            serve(app, host=host, port=port, threads=8)
            return
        except ImportError:
            logger.warning("waitress no instalado; usando servidor de desarrollo de Flask")
    app.run(host=host, port=port, debug=debug, use_reloader=False)


if __name__ == '__main__':
    run_api()
