#!/usr/bin/env python3
"""
API REST para que otros backends se conecten al portero.

Autenticacion: header X-API-Key

Endpoints:
  GET  /api/health
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
  POST /api/comandos
"""
import logging
from functools import wraps

from flask import Flask, request, jsonify

from config import API_HOST, API_PORT, API_KEY
from database import Database
from socio_service import (
    crear_socio, actualizar_socio, eliminar_socio,
    abrir_puerta, sincronizar_fichajes, encolar_comando,
)
from webhook_client import notify_socio_queued
from zkteco_protocol import DEFAULT_DEVICE_SN

logger = logging.getLogger(__name__)

app = Flask(__name__)
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
