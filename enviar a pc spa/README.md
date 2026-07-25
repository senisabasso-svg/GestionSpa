# 🔐 API Servidor Portero Biométrico

**Control de Acceso TCP PUSH para Terminales T&A**

---

## 📋 Contenido del Proyecto

```
.
├── servidor_portero.py    ← SERVIDOR PRINCIPAL (inicia aquí)
├── database.py            ← Gestión de BD SQLite
├── config.py              ← Configuración
├── ver_datos.py           ← Visualiza datos almacenados
├── requirements.txt       ← Dependencias Python
└── README.md              ← Este archivo
```

---

## 🚀 INICIO RÁPIDO (Windows — PC del spa)

### 1️⃣ **Instalar (recomendado)**

Doble clic en **`Instalar.bat`**.

Eso:
- Instala **Python** si no está (vía winget)
- Crea entorno virtual `.venv`
- Descarga dependencias (`requirements.txt`)
- Crea la base **`portero_spa.db`** si no existe
- Deja listo **`Iniciar_Panel.bat`**

### 2️⃣ **Abrir el panel**

Doble clic en **`Iniciar_Panel.bat`**.

El panel inicia/detiene el mismo `run_all.py` (TCP + REST), muestra IP/puertos, configuración, logs redimensionables (arrastrá el separador). No cambia la funcionalidad de la API.

### Alternativa manual

```bash
pip install -r requirements.txt
python init_db.py
python desktop_app.py
```

**Opción B — Consola (TCP + REST)**

```bash
python run_all.py
```

**Opción C — Solo TCP (sin API REST)**

```bash
python servidor_portero.py
```

Deberías ver:

```
╔═══════════════════════════════════════════════════════════╗
║        🔐 SERVIDOR CONTROL DE ACCESO - PORTERO SPA        ║
╚═══════════════════════════════════════════════════════════╝

✅ Servidor iniciado correctamente
📍 Dirección: 0.0.0.0:8081
📊 BD: /ruta/portero_spa.db
📋 Logs: /ruta/logs/

⏳ Esperando conexiones de dispositivos...
(Presiona Ctrl+C para detener)
```

### 3️⃣ **Ver Datos Almacenados**

En otra terminal:

```bash
python3 ver_datos.py
```

---

## ⚙️ CONFIGURACIÓN

Edita `config.py` para cambiar:

```python
# Puerto donde escucha
SERVER_PORT = 8081

# IP a la que se conecta el portero
# 0.0.0.0 = todas las interfaces
SERVER_HOST = '0.0.0.0'

# Ubicación de BD
DB_PATH = './portero_spa.db'

# Directorio de logs
LOG_DIR = './logs'
```

---

## 🔌 CONECTAR EL PORTERO

### Opción 1: Red Local

1. Obtén tu IP local:

**Linux/Mac:**
```bash
ifconfig | grep "inet " | grep -v "127.0.0.1"
```

**Windows:**
```bash
ipconfig
```

2. En el portero:
   - Menú → Ajustes Servidor Cloud
   - Dirección: `tu_ip_local` (ej: 192.168.1.10)
   - Puerto: `8081`
   - Guardar

3. ✅ El portero debería conectar automáticamente

---

### Opción 2: Con ngrok (Exposición Pública)

**Instala ngrok:**
```bash
# Descargar desde: https://ngrok.com/download
# O con Homebrew (Mac):
brew install ngrok/ngrok/ngrok

# O con apt (Linux):
sudo apt install ngrok
```

**Expón tu servidor:**
```bash
ngrok tcp 8081
```

Verás algo como:
```
Forwarding                    tcp://2.tcp.ngrok.io:15234 -> localhost:8081
```

**En el portero:**
- Dirección: `2.tcp.ngrok.io`
- Puerto: `15234`

---

## 📊 ESTRUCTURA DE DATOS RECIBIDOS

### Acceso de Usuario

```json
{
  "terminalId": 1,
  "type": "access",
  "timestamp": "2026-06-29T17:33:06Z",
  "data": {
    "userId": "12345",
    "userName": "Juan Pérez",
    "accessType": "entry",
    "method": "facial_recognition",
    "confidence": 0.98,
    "temperature": 36.5,
    "cardId": "04:A1:23:45:67",
    "maskDetected": false
  }
}
```

### Nuevo Usuario

```json
{
  "terminalId": 1,
  "type": "user_registration",
  "data": {
    "userId": "NEW-001",
    "firstName": "Carlos",
    "lastName": "González",
    "email": "carlos@spa.com",
    "phone": "+34611223344",
    "membershipType": "premium",
    "accessLevel": 3,
    "validFrom": "2026-06-29",
    "validUntil": "2027-06-29",
    "cardId": "04:B2:34:56:78"
  }
}
```

### Heartbeat (Estado)

```json
{
  "terminalId": 1,
  "type": "heartbeat",
  "data": {
    "status": "online",
    "temperature": 22.5,
    "networkSignal": -45,
    "batteryLevel": 100,
    "faceCount": 1256,
    "cardCount": 432
  }
}
```

---

## 📁 BASE DE DATOS

Automáticamente se crean estas tablas:

### `users` - Socios/Usuarios
```sql
user_id          TEXT (único)
first_name       TEXT
last_name        TEXT
email            TEXT
phone            TEXT
membership_type  TEXT
access_level     INT
card_id          TEXT
valid_from       DATE
valid_until      DATE
status           TEXT (active/inactive)
created_at       DATETIME
```

### `access_logs` - Registros de Acceso
```sql
terminal_id      INT
user_id          TEXT
user_name        TEXT
access_type      TEXT (entry/exit)
method           TEXT (facial_recognition/card/pin)
confidence       FLOAT (0.0-1.0)
temperature      FLOAT
card_id          TEXT
mask_detected    BOOL
event_timestamp  DATETIME
created_at       DATETIME
```

### `terminals` - Dispositivos Conectados
```sql
terminal_id      INT (único)
name             TEXT
location         TEXT
ip_address       TEXT
last_heartbeat   DATETIME
status           TEXT (online/offline)
created_at       DATETIME
```

### `alerts` - Alertas
```sql
terminal_id      INT
alert_type       TEXT
severity         TEXT (low/medium/high/critical)
message          TEXT
resolved         BOOL
event_timestamp  DATETIME
created_at       DATETIME
```

---

## 📝 LOGS

Los logs se guardan en `logs/portero_server.log` e incluyen:

```
2026-06-29 17:33:06,123 - INFO - 🔌 Conexión entrante de 192.168.1.12:54321
2026-06-29 17:33:07,456 - INFO - 📨 [TERMINAL 1] Evento: access
2026-06-29 17:33:07,890 - INFO - 📝 Acceso registrado: Juan Pérez
```

---

## 🔧 TROUBLESHOOTING

### ❌ "Address already in use"

El puerto 8081 ya está en uso. Soluciones:

**Linux/Mac:**
```bash
lsof -i :8081
kill -9 <PID>
```

**Windows:**
```bash
netstat -ano | findstr :8081
taskkill /PID <PID> /F
```

O cambia el puerto en `config.py`:
```python
SERVER_PORT = 8082
```

---

### ❌ El portero no conecta

1. ✅ Verifica que el servidor esté corriendo:
   ```bash
   python3 servidor_portero.py
   ```

2. ✅ Comprueba la IP:
   ```bash
   ifconfig  # Linux/Mac
   ipconfig  # Windows
   ```

3. ✅ Verifica firewall:
   - Linux: `sudo ufw allow 8081`
   - Windows: Abre puerto en Windows Defender

4. ✅ Prueba conectividad:
   ```bash
   telnet 192.168.1.X 8081
   # Si conecta, verás un cursor vacío (Ctrl+C para salir)
   ```

---

### ❌ No veo los datos en BD

Verifica con:
```bash
python3 ver_datos.py
```

Si está vacío, revisa los logs:
```bash
tail -f logs/portero_server.log
```

---

## 🧪 TESTING MANUAL

Para simular un dispositivo y enviar datos:

```bash
# Terminal 1: Inicia servidor
python3 servidor_portero.py

# Terminal 2: Envía un evento de prueba
cat > test_event.json << 'EOF'
{"terminalId":1,"type":"access","timestamp":"2026-06-29T17:33:06Z","data":{"userId":"12345","userName":"Test User","accessType":"entry","method":"facial_recognition","confidence":0.95,"temperature":36.5}}
EOF

# Linux/Mac:
nc localhost 8081 < test_event.json

# Windows (requiere nc o PowerShell):
Get-Content test_event.json | nc localhost 8081
```

---

## 📡 INTEGRACIÓN CON NGROK

Para producción con ngrok (plan PRO):

```bash
# Compra plan pro en ngrok.com

# Usa URL estable
ngrok tcp --domain=mi-portero.ngrok.io 8081

# En el portero:
# - Dirección: mi-portero.ngrok.io
# - Puerto: 443 (o el que ngrok asigne)
```

---

## 🔐 SEGURIDAD (Producción)

Para producción, considera:

1. **SSL/TLS:**
   - Usa certificados SSL
   - Modifica `servidor_portero.py` para usar SSL

2. **Autenticación:**
   - Agrega tokens API
   - Valida origen de dispositivos

3. **Firewall:**
   - Restringe IP permitidas
   - Solo acepta del portero

4. **Backup:**
   - Backup diario de `portero_spa.db`
   - Mantén copias en otro servidor

---

## 📞 SOPORTE

**Logs:**
```bash
tail -f logs/portero_server.log
```

**Ver estado actual:**
```bash
python3 ver_datos.py
```

**Reinicia el servidor:**
```bash
# Ctrl+C para detener
# python3 servidor_portero.py para reiniciar
```

---

## 📄 Licencia

Este proyecto es para uso privado en el spa.

---

**¡Servidor listo para usar! 🚀**
