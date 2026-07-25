"""
Envio de webhooks al backend externo cuando ocurren eventos.
"""
import logging
import threading

import requests

from config import WEBHOOK_URL, WEBHOOK_SECRET

logger = logging.getLogger(__name__)


def _send_webhook(event: str, payload: dict):
    if not WEBHOOK_URL:
        return

    body = {
        'event': event,
        'data': payload,
    }
    headers = {'Content-Type': 'application/json'}
    if WEBHOOK_SECRET:
        headers['X-Webhook-Secret'] = WEBHOOK_SECRET

    try:
        resp = requests.post(WEBHOOK_URL, json=body, headers=headers, timeout=10)
        logger.info(f"Webhook {event} -> {resp.status_code}")
    except Exception as e:
        logger.error(f"Webhook fallo ({event}): {e}")


def notify_access(access_data: dict):
    """Notifica fichaje al backend externo (async)."""
    threading.Thread(
        target=_send_webhook,
        args=('access', access_data),
        daemon=True,
    ).start()


def notify_device_online(device_sn: str, info: dict = None):
    threading.Thread(
        target=_send_webhook,
        args=('device_online', {'device_sn': device_sn, **(info or {})}),
        daemon=True,
    ).start()


def notify_socio_queued(action: str, socio_data: dict):
    threading.Thread(
        target=_send_webhook,
        args=(f'socio_{action}', socio_data),
        daemon=True,
    ).start()
