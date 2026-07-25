# Desplegar ApiPorteroSpa en Railway

El ZKTeco habla **TCP** (no HTTPS). En Railway se usa un **TCP Proxy** (host tipo `xxx.proxy.rlwy.net` + puerto), igual que con ngrok pero fijo al servicio.

```
ZKTeco ──TCP──► xxx.proxy.rlwy.net:PUERTO ──► contenedor :8081 (ApiPorteroSpa)
GestionSpa ──HTTPS──► https://api-portero.up.railway.app  (REST / health / opcional)
ApiPorteroSpa ──pull──► GestionSpa (Railway) cada N s
```

## 1. Crear servicio

1. Railway → New → GitHub repo `GestionSpa`
2. Root directory / watch path: `ApiPorteroSpa` (o servicio con Dockerfile en esa carpeta)
3. Builder: Dockerfile (ya hay `railway.toml`)

## 2. Volume (importante)

Sin volume perdés la SQLite en cada deploy.

1. Service → Volumes → Add  
2. Mount path: `/data`  
3. Variable (opcional, ya default): `PORTERO_DATA_DIR=/data`

## 3. Variables

```
PORTERO_API_KEY=spa-dayman-2026-clave
PORTERO_WEBHOOK_SECRET=webhook-dayman-secreto
PORTERO_GESTION_BASE_URL=https://gestionspa-production-dea2.up.railway.app
PORTERO_EMISOR_SLUG=dayman
PORTERO_POLL_SECONDS=5
PORTERO_TCP_PORT=8081
PORTERO_DATA_DIR=/data
```

`PORT` lo pone Railway solo (HTTP/REST).

## 4. HTTP público (REST + panel de logs)

Settings → Networking → Generate domain → puerto **`$PORT`** (ej. 8080), **no** 8081.

Ej: `https://apiporterospa-production.up.railway.app`

| URL | Uso |
|-----|-----|
| `/panel` | Panel web de logs en vivo (pedí la `PORTERO_API_KEY`) |
| `/api/health` | Health JSON (público) |
| `/api/logs` | Últimas líneas (header `X-API-Key`) |

El panel va en el mismo Docker; no hace falta otro servicio.

## 5. TCP Proxy (para el ZKTeco)

1. Settings → Networking → **TCP Proxy** → Add  
2. Application port: **8081** (el de `PORTERO_TCP_PORT`)  
3. Te da algo como:
   - Host: `xxxxx.proxy.rlwy.net`
   - Port: `24808` (ejemplo; el que asigne Railway)

## 6. En el portero físico (Cloud / Servidor)

| Campo | Valor |
|--------|--------|
| Dirección / IP servidor | `xxxxx.proxy.rlwy.net` (el host del TCP Proxy) |
| Puerto | el puerto del TCP Proxy (ej. `24808`) — **no** 8081 interno |

Ethernet del equipo: IP local normal de la red del spa (para salir a internet). DHCP o estática, da igual mientras tenga salida.

## 7. GestionSpa

- Portero habilitado, misma API Key / secret / SN  
- Modo pull: la ApiPortero en Railway consulta sola a GestionSpa (variables de arriba)  
- Opcional: si más adelante usás push, `apiUrl` = URL HTTPS del servicio ApiPortero  

## 8. Probar

1. Logs Railway: `Servidor escuchando en 0.0.0.0:8081` + Waitress en `$PORT`  
2. `curl https://TU-DOMINIO/api/health`  
3. ZKTeco con host/puerto del TCP Proxy → en logs deberían aparecer conexiones  
4. GestionSpa → Probar agente (heartbeat del pull)

## Notas

- El dominio `*.up.railway.app` es **HTTP**; el ZKTeco usa el **TCP Proxy** (`*.proxy.rlwy.net`).  
- Si el equipo no acepta hostname, a veces hace falta la IP resuelta del proxy (menos estable).  
- Redeploys pueden cortar la sesión TCP unos segundos; el equipo suele reconectar solo.  
- La PC del spa con panel sigue siendo válida como alternativa local sin Railway.
