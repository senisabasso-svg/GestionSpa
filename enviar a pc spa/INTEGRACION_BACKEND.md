# Integración ApiPorteroSpa con tu Backend

Guía para conectar tu backend (SPA, gym, ERP, etc.) con el servicio de control de acceso del portero biométrico ZKTeco.

---

## 1. Arquitectura

```
┌─────────────────┐         REST / Webhooks          ┌──────────────────┐
│  Tu Backend     │ ◄──────────────────────────────► │  ApiPorteroSpa   │
│  (SPA, API, DB) │    JSON + X-API-Key              │  Puerto 5000     │
└─────────────────┘                                  └────────┬─────────┘
                                                                │ TCP 8081
                                                                │ Protocolo ZKTeco
                                                                ▼
                                                       ┌──────────────────┐
                                                       │ Portero ZKTeco   │
                                                       │ SN: 7674222960189│
                                                       └──────────────────┘
```

**Flujo de datos:**

| Dirección | Mecanismo | Uso |
|-----------|-----------|-----|
| Backend → ApiPortero | REST API | Alta/baja socios, abrir puerta, consultas |
| ApiPortero → Backend | Webhooks HTTP POST | Fichajes en tiempo real, confirmaciones |
| Portero → ApiPortero | TCP push (cada ~10 s) | El equipo inicia la conexión |

> **Importante:** Los comandos al portero (alta, baja, etc.) se **encolan** y el dispositivo los recibe en su próximo `getrequest` (~10 segundos). No es instantáneo.

---

## 2. Puesta en marcha

### 2.1 Instalar y arrancar

```powershell
cd C:\Users\senis\OneDrive\Documentos\ApiPorteroSpa
pip install -r requirements.txt
python run_all.py
```

Servicios activos:

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| API REST | `5000` | Tu backend se conecta aquí |
| Portero TCP | `8081` | Solo el dispositivo físico |

### 2.2 Variables de entorno (recomendado en producción)

```powershell
# Clave que tu backend debe enviar en cada request
$env:PORTERO_API_KEY = "tu-clave-secreta-muy-larga"

# URL de tu backend para recibir eventos (webhooks)
$env:PORTERO_WEBHOOK_URL = "https://tu-backend.com/api/webhooks/portero"

# Opcional: validar webhooks en tu backend
$env:PORTERO_WEBHOOK_SECRET = "secreto-compartido-webhook"
```

### 2.3 Verificar que funciona

```bash
# Sin autenticación
curl http://localhost:5000/api/health

# Con autenticación
curl http://localhost:5000/api/stats \
  -H "X-API-Key: portero-dev-key-change-me"
```

Respuesta esperada de `/api/health`:

```json
{
  "status": "ok",
  "service": "ApiPorteroSpa",
  "device_default_sn": "7674222960189"
}
```

---

## 3. Autenticación

Todas las rutas bajo `/api/*` (excepto `/api/health`) requieren el header:

```
X-API-Key: <tu-clave>
```

| Código | Significado |
|--------|-------------|
| `401` | API key inválida o ausente |
| `404` | Recurso no encontrado |
| `400` | Body inválido o campos faltantes |
| `500` | Error interno |

---

## 4. API REST — Referencia de endpoints

**Base URL:** `http://<host-api-portero>:5000`

**Dispositivo por defecto:** `7674222960189` (se puede override con `device_sn` en el body).

---

### 4.1 Salud y estadísticas

#### `GET /api/health`

Sin autenticación. Verifica que el servicio está vivo.

#### `GET /api/stats`

```json
{
  "total_users": 2,
  "total_accesses": 15,
  "online_terminals": 1,
  "pending_alerts": 0,
  "total_raw_packets": 400,
  "pending_commands": 0
}
```

---

### 4.2 Socios

El **PIN** en el portero = **cédula/documento** del socio (string numérico).

#### `GET /api/socios`

Query params opcionales:

| Param | Tipo | Descripción |
|-------|------|-------------|
| `limit` | int | Máximo de resultados (default: 100) |
| `status` | string | Filtrar: `active`, `inactive` |

```json
{
  "socios": [
    {
      "id": 1,
      "user_id": "51498995",
      "first_name": "magollego",
      "last_name": "",
      "email": "",
      "phone": "092331019",
      "membership_type": "socio",
      "access_level": 1,
      "card_id": "51498995",
      "valid_from": "2026-07-02",
      "valid_until": "2027-07-02",
      "status": "active",
      "created_at": "2026-07-02 17:03:00",
      "updated_at": "2026-07-02 17:03:00"
    }
  ]
}
```

#### `POST /api/socios` — Alta de socio

Encola el usuario en el portero. **Después debe enrolar el rostro en el equipo.**

**Body:**

```json
{
  "nombre": "Juan Perez",
  "cedula": "12345678",
  "celular": "099123456",
  "email": "juan@spa.com",
  "membership_type": "socio",
  "access_level": 1,
  "valid_days": 365,
  "device_sn": "7674222960189"
}
```

| Campo | Requerido | Descripción |
|-------|-----------|-------------|
| `nombre` | Sí | Nombre en el portero |
| `cedula` | Sí | PIN/ID en el dispositivo |
| `celular` | No | Solo se guarda en BD local |
| `email` | No | Solo BD local |
| `device_sn` | No | Serial del portero |
| `valid_days` | No | Días de membresía (default: 365) |

**Respuesta `201`:**

```json
{
  "pin": "12345678",
  "nombre": "Juan Perez",
  "celular": "099123456",
  "email": "juan@spa.com",
  "device_sn": "7674222960189",
  "command_id": 102,
  "valid_from": "2026-07-02",
  "valid_until": "2027-07-02",
  "status": "queued",
  "message": "Socio guardado y encolado para el portero (~10 seg)"
}
```

#### `GET /api/socios/{pin}`

Obtiene un socio por cédula/PIN.

#### `PUT /api/socios/{pin}` — Actualizar

```json
{
  "nombre": "Juan P. Perez",
  "celular": "099999999",
  "email": "nuevo@email.com",
  "status": "active",
  "device_sn": "7674222960189"
}
```

Si `status` es `active`, re-envía el usuario al portero.

**Suspender acceso:** `PUT` con `"status": "inactive"` (solo BD local; para bloquear en el equipo usar `DELETE`).

#### `DELETE /api/socios/{pin}` — Baja en portero

```json
{
  "device_sn": "7674222960189"
}
```

Marca el socio como `inactive` en BD y encola `DATA DELETE USERINFO` en el portero.

---

### 4.3 Fichajes (accesos)

#### `GET /api/accesos`

Query params:

| Param | Tipo | Descripción |
|-------|------|-------------|
| `limit` | int | Default 50 |
| `pin` | string | Filtrar por cédula/PIN |
| `since` | string | Fecha ISO, ej: `2026-07-02 00:00:00` |

```json
{
  "accesos": [
    {
      "id": 1,
      "terminal_id": "7674222960189",
      "user_id": "51498995",
      "user_name": "magollego",
      "access_type": "entry",
      "method": "face",
      "confidence": null,
      "temperature": null,
      "card_id": null,
      "mask_detected": 0,
      "status": "success",
      "event_timestamp": "2026-07-02 18:30:00",
      "created_at": "2026-07-02 18:30:05"
    }
  ],
  "total": 1
}
```

**Valores de `access_type`:** `entry` (entrada), `exit` (salida).

**Valores de `method`:** `face`, `fingerprint`, `card`, `password`, etc.

---

### 4.4 Dispositivos

#### `GET /api/dispositivos`

Lista porteros registrados y comandos pendientes globales.

#### `GET /api/dispositivos/{sn}`

Detalle de un dispositivo + cola de comandos pendientes para ese SN.

#### `POST /api/dispositivos/{sn}/abrir-puerta`

Encola comando para abrir la puerta remotamente.

```json
{
  "device_sn": "7674222960189",
  "command_id": 103,
  "action": "unlock_door"
}
```

#### `POST /api/dispositivos/{sn}/sincronizar-fichajes`

Pide al portero que envíe fichajes almacenados.

```json
{
  "device_sn": "7674222960189",
  "command_id": 104,
  "action": "query_attlog"
}
```

---

### 4.5 Comandos personalizados

#### `POST /api/comandos`

Para comandos ZKTeco avanzados no cubiertos por la API.

```json
{
  "device_sn": "7674222960189",
  "command": "DATA UPDATE USERINFO PIN=999\tName=Test\tPri=0\tPasswd=\tCard=\tGrp=1\tTZ=0000000100000000\tVerifyMode=15"
}
```

> El campo `command` es el cuerpo **sin** el prefijo `C:ID:` (eso lo agrega el sistema).

---

## 5. Webhooks (ApiPortero → tu Backend)

Cuando configurás `PORTERO_WEBHOOK_URL`, ApiPortero hace `POST` a tu backend en eventos importantes.

### 5.1 Configuración en tu backend

1. Creá un endpoint, por ejemplo: `POST /api/webhooks/portero`
2. Validá el header `X-Webhook-Secret` si configuraste `PORTERO_WEBHOOK_SECRET`
3. Procesá el campo `event` y el payload en `data`

### 5.2 Formato del webhook

```json
{
  "event": "access",
  "data": { ... }
}
```

Headers enviados:

```
Content-Type: application/json
X-Webhook-Secret: <secreto>   (si está configurado)
```

### 5.3 Eventos disponibles

| Evento | Cuándo se dispara | Uso en tu backend |
|--------|-------------------|-----------------|
| `access` | Alguien fichó en el portero | Marcar asistencia, validar membresía |
| `socio_created` | Alta encolada vía API | Confirmar sincronización |
| `socio_updated` | Socio actualizado | Actualizar cache local |
| `socio_deleted` | Baja encolada | Desactivar en tu sistema |

#### Ejemplo: `access`

```json
{
  "event": "access",
  "data": {
    "device_sn": "7674222960189",
    "pin": "51498995",
    "nombre": "magollego",
    "access_type": "entry",
    "method": "face",
    "timestamp": "2026-07-02 18:30:00"
  }
}
```

#### Ejemplo: `socio_created`

```json
{
  "event": "socio_created",
  "data": {
    "pin": "12345678",
    "nombre": "Juan Perez",
    "celular": "099123456",
    "device_sn": "7674222960189",
    "command_id": 102,
    "status": "queued"
  }
}
```

### 5.4 Respuesta esperada de tu backend

- Respondé `200` o `204` lo antes posible
- El webhook se envía en un hilo aparte; si tu backend tarda o falla, el fichaje **igual queda guardado** en ApiPortero (podés consultarlo con `GET /api/accesos`)

---

## 6. Flujos de integración recomendados

### 6.1 Alta de socio nuevo (desde tu backend)

```
Tu Backend                          ApiPortero                    Portero
    │                                    │                            │
    │  POST /api/socios                  │                            │
    │───────────────────────────────────►│                            │
    │  201 { status: "queued" }          │                            │
    │◄───────────────────────────────────│                            │
    │                                    │  getrequest (~10s)         │
    │                                    │───────────────────────────►│
    │                                    │  DATA UPDATE USERINFO      │
    │                                    │◄───────────────────────────│
    │  webhook socio_created             │                            │
    │◄───────────────────────────────────│                            │
    │                                    │                            │
    │  (Socio va al equipo y registra rostro manualmente)             │
```

### 6.2 Fichaje en tiempo real

```
Portero              ApiPortero              Tu Backend
   │                      │                       │
   │  POST ATTLOG         │                       │
   │─────────────────────►│                       │
   │  OK:1                │  guarda en BD         │
   │◄─────────────────────│                       │
   │                      │  POST webhook access  │
   │                      │──────────────────────►│
   │                      │                       │ validar membresía
   │                      │                       │ registrar asistencia
```

### 6.3 Membresía vencida — bloquear acceso

```
Tu Backend:
  1. DELETE /api/socios/{cedula}     → elimina del portero
  2. PUT con status inactive en tu BD
```

### 6.4 Polling alternativo (sin webhooks)

Si no podés recibir webhooks, consultá periódicamente:

```
GET /api/accesos?since=2026-07-02T18:00:00&limit=100
```

Cada 30–60 segundos, filtrando por `since` con la última fecha procesada.

---

## 7. Ejemplos de código

### 7.1 Node.js / JavaScript (fetch)

```javascript
const API_URL = 'http://localhost:5000';
const API_KEY = process.env.PORTERO_API_KEY;

async function altaSocio({ nombre, cedula, celular, email }) {
  const res = await fetch(`${API_URL}/api/socios`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-API-Key': API_KEY,
    },
    body: JSON.stringify({ nombre, cedula, celular, email }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

async function obtenerFichajes(desde) {
  const params = new URLSearchParams({ since: desde, limit: '100' });
  const res = await fetch(`${API_URL}/api/accesos?${params}`, {
    headers: { 'X-API-Key': API_KEY },
  });
  return res.json();
}
```

### 7.2 Python (requests)

```python
import os
import requests

API_URL = 'http://localhost:5000'
HEADERS = {'X-API-Key': os.environ['PORTERO_API_KEY']}

def alta_socio(nombre, cedula, celular=''):
    r = requests.post(f'{API_URL}/api/socios', headers=HEADERS, json={
        'nombre': nombre,
        'cedula': str(cedula),
        'celular': celular,
    })
    r.raise_for_status()
    return r.json()

def baja_socio(cedula):
    r = requests.delete(f'{API_URL}/api/socios/{cedula}', headers=HEADERS)
    r.raise_for_status()
    return r.json()
```

### 7.3 Endpoint webhook en tu backend (Express.js)

```javascript
app.post('/api/webhooks/portero', (req, res) => {
  const secret = req.headers['x-webhook-secret'];
  if (secret !== process.env.PORTERO_WEBHOOK_SECRET) {
    return res.status(401).send('Unauthorized');
  }

  const { event, data } = req.body;

  switch (event) {
    case 'access':
      // data.pin, data.nombre, data.access_type, data.method, data.timestamp
      registrarAsistencia(data);
      break;
    case 'socio_created':
      console.log('Socio encolado:', data.pin);
      break;
    case 'socio_deleted':
      desactivarSocioLocal(data.pin);
      break;
  }

  res.sendStatus(200);
});
```

### 7.4 cURL — referencia rápida

```bash
# Alta
curl -X POST http://localhost:5000/api/socios \
  -H "Content-Type: application/json" \
  -H "X-API-Key: portero-dev-key-change-me" \
  -d '{"nombre":"Maria Lopez","cedula":"87654321","celular":"098112233"}'

# Baja
curl -X DELETE http://localhost:5000/api/socios/87654321 \
  -H "X-API-Key: portero-dev-key-change-me"

# Fichajes del día
curl "http://localhost:5000/api/accesos?since=2026-07-02%2000:00:00&limit=50" \
  -H "X-API-Key: portero-dev-key-change-me"

# Abrir puerta
curl -X POST http://localhost:5000/api/dispositivos/7674222960189/abrir-puerta \
  -H "X-API-Key: portero-dev-key-change-me"
```

---

## 8. Despliegue y conectividad del portero

### Misma red local

El portero apunta a la IP de la PC donde corre ApiPortero:

- Dirección: `192.168.x.x`
- Puerto: `8081`

Tu backend puede estar en otro servidor y llamar a `http://<ip-api-portero>:5000`.

### Portero en otra ubicación (túnel)

Si el portero no está en tu red, usá un túnel TCP (ej. bore):

```powershell
.\tools\bore\bore.exe local 8081 --to bore.pub
```

En el portero:

- Dirección: `bore.pub` (o el host del túnel)
- Puerto: el que indique el túnel (ej. `36051`)

La API REST (`5000`) es independiente: tu backend la alcanza por IP pública/VPN de la máquina donde corre `run_all.py`.

---

## 9. Limitaciones y consideraciones

| Tema | Detalle |
|------|---------|
| **Rostro** | El alta crea el usuario; el enrolamiento facial se hace **en el equipo** |
| **Latencia comandos** | ~10 s (intervalo de `getrequest` del portero) |
| **PIN = cédula** | Máximo ~9 dígitos recomendado en ZKTeco |
| **Idempotencia** | `POST /api/socios` con misma cédula hace `INSERT OR REPLACE` en BD local |
| **Biometría** | Equipo FA6000: cara y palma; sin huella |
| **Seguridad** | Cambiar `PORTERO_API_KEY` en producción; usar HTTPS en webhook |

---

## 10. Checklist de integración

- [ ] `run_all.py` corriendo (puertos 5000 y 8081)
- [ ] Portero conectado (ver `GET /api/dispositivos`)
- [ ] `PORTERO_API_KEY` configurada en ambos lados
- [ ] `PORTERO_WEBHOOK_URL` apuntando a tu backend
- [ ] Endpoint webhook implementado y respondiendo 200
- [ ] Probado `POST /api/socios` + verificación en portero (~10 s)
- [ ] Probado fichaje + recepción de webhook `access`
- [ ] Probado `DELETE /api/socios/{pin}` para bajas

---

## 11. Soporte y logs

| Recurso | Ubicación |
|---------|-----------|
| Logs del servidor | `logs/portero_server.log` |
| Tráfico crudo portero | `logs/raw/raw_traffic.log` |
| Sesiones por conexión | `logs/sessions/` |
| Base de datos | `portero_spa.db` (SQLite) |
| Ver datos en consola | `python ver_datos.py` |
| Ver tráfico crudo | `python ver_raw.py` |

---

## 12. Resumen de URLs

| Acción | Método | URL |
|--------|--------|-----|
| Health check | GET | `/api/health` |
| Estadísticas | GET | `/api/stats` |
| Listar socios | GET | `/api/socios` |
| Crear socio | POST | `/api/socios` |
| Ver socio | GET | `/api/socios/{pin}` |
| Actualizar socio | PUT | `/api/socios/{pin}` |
| Eliminar socio | DELETE | `/api/socios/{pin}` |
| Listar fichajes | GET | `/api/accesos` |
| Listar dispositivos | GET | `/api/dispositivos` |
| Ver dispositivo | GET | `/api/dispositivos/{sn}` |
| Abrir puerta | POST | `/api/dispositivos/{sn}/abrir-puerta` |
| Sincronizar fichajes | POST | `/api/dispositivos/{sn}/sincronizar-fichajes` |
| Comando custom | POST | `/api/comandos` |
