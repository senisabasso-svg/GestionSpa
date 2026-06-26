# SPA Thermal Daymán — Sistema de Gestión

Sistema web de gestión para el **SPA Thermal Daymán** en Termas del Daymán, Salto, Uruguay.

## Funcionalidades

- **Socios**: alta, edición, suspensión, cuota mensual en pesos uruguayos (UYU)
- **Clientes ocasionales**: personas que usan servicios sin ser socios
- **Servicios**: masajes, tratamientos termales, fango facial, hidromasajes, paquetes
- **Cargos**: registrar servicios a socios (suma a cuota mensual) o clientes (cobro directo)
- **Cuotas mensuales**: generación automática, vencimiento día 10, registro de pagos
- **Informes**: resumen financiero, cobranza por socio, ingresos diarios, servicios más vendidos
- **Control de ingreso**: pantalla tipo kiosk donde el socio ingresa su número para acceder

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| Frontend | React 19 + TypeScript + Vite |
| Backend | ASP.NET Core 10 (C#) |
| Base de datos | PostgreSQL 16 |

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (para PostgreSQL) o PostgreSQL instalado localmente

## Inicio rápido

### 1. Base de datos

```bash
docker compose up -d
```

Esto levanta PostgreSQL en `localhost:5432` con:
- Usuario: `postgres`
- Contraseña: `postgres`
- Base de datos: `gestionspa`

### 2. Backend (API)

```bash
cd backend/GestionSpa.Api
dotnet run
```

API disponible en: http://localhost:5000  
Swagger: http://localhost:5000/swagger

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

App disponible en: http://localhost:5173

### 4. Control de ingreso (kiosk)

Abrir en una tablet o pantalla en la entrada:

http://localhost:5173/ingreso

## Datos de prueba

El sistema carga automáticamente servicios y socios de ejemplo:

| Nº Socio | Nombre | Estado | Cuota |
|----------|--------|--------|-------|
| 1001 | María González | Activo (cuota pagada) | $3.500 |
| 1002 | Carlos Rodríguez | Activo (cuota pendiente) | $3.500 |
| 1003 | Ana Silva | Activo (cuota pendiente) | $4.200 |
| 1004 | Jorge Pérez | Suspendido | $3.500 |

## Estructura del proyecto

```
GestionSpa/
├── backend/GestionSpa.Api/    # API REST en C#
│   ├── Controllers/           # Endpoints
│   ├── Models/                # Entidades
│   ├── Data/                  # DbContext y seed
│   └── Services/              # Lógica de negocio
├── frontend/                  # App React
│   └── src/
│       ├── pages/             # Pantallas
│       ├── components/        # Layout
│       └── api/               # Cliente HTTP
└── docker-compose.yml         # PostgreSQL
```

## Configuración

Cadena de conexión en `backend/GestionSpa.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=gestionspa;Username=postgres;Password=postgres"
}
```

## Referencia

Basado en el [SPA Thermal Daymán](https://amotermas.uy/spa-thermal-dayman-salto/) — Termas del Daymán, Salto, Uruguay.
