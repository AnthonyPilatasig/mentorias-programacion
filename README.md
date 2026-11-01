# Mentorías de código

[![Backend](https://github.com/AnthonyPilatasig/mentorias-programacion/actions/workflows/backend.yml/badge.svg)](https://github.com/AnthonyPilatasig/mentorias-programacion/actions/workflows/backend.yml)
[![Frontend](https://github.com/AnthonyPilatasig/mentorias-programacion/actions/workflows/frontend.yml/badge.svg)](https://github.com/AnthonyPilatasig/mentorias-programacion/actions/workflows/frontend.yml)

Servicio de mentorías de programación en línea. **Aplicaciones Web**, Universidad Politécnica Salesiana.
Autor: Anthony David Pilatasig Macas.

| Carpeta | Unidad | Qué contiene |
|---|---|---|
| [`frontend/`](frontend) | 1 y 2 | Sitio responsivo y accesible (HTML, CSS y JavaScript sin frameworks). Desde la Unidad 2, el formulario reserva contra la API, muestra los horarios libres en tiempo real e incluye el panel del mentor. |
| [`backend/`](backend) | 2 | API REST en ASP.NET Core 10 + EF Core + MySQL 8.4: CRUD de reservas, sesiones, adjuntos y SignalR. |

| | |
|---|---|
| Persistencia | MySQL en Docker, migraciones de EF Core, consultas parametrizadas |
| Sesiones | Cookie HttpOnly + SameSite=Strict, sesión guardada en el servidor (MySQL) |
| Archivos | Adjuntos por reserva con lista blanca y verificación de firma (bytes mágicos) |
| Tiempo real | Hub SignalR `/hubs/reservas` con eventos para el mentor y para el público |
| Pruebas | 90 pruebas xUnit (unitarias + integración con MySQL real vía Testcontainers) y colección Postman de 34 aserciones |
| Documentación | OpenAPI 3.1 en `/openapi/v1.json` y referencia interactiva en `/docs` |

## Estructura del repositorio

```
mentorias-programacion/
├── frontend/              Sitio web (Unidad 1, conectado a la API en la Unidad 2)
│   ├── index.html         Página principal con el formulario «Reserva tu sesión»
│   ├── panel.html         Panel del mentor (inicio de sesión, reservas en vivo, adjuntos)
│   ├── css/  js/  img/
│   └── evidencias/        Capturas de la Unidad 1
├── backend/               API REST (Unidad 2)
│   ├── src/Mentorias.Api/ Código de la API
│   ├── tests/             90 pruebas xUnit
│   ├── postman/           Colección de Postman y archivos de prueba
│   ├── requests/          Peticiones .http para VS Code / Visual Studio
│   ├── evidencias/        Capturas y resultados de pruebas de la Unidad 2
│   └── Dockerfile
├── docker-compose.yml     Levanta MySQL + API con un solo comando
└── .env.example           Credenciales por defecto (opcional cambiarlas)
```

## Cómo funciona

```
Navegador ──► http://localhost:8080 ──► API (ASP.NET Core) ──► MySQL
               │                          │
               ├─ /, /panel.html          └─ entrega también las páginas de frontend/
               ├─ /api/...  (datos JSON)
               └─ /hubs/reservas (tiempo real)
```

No hay dos servidores separados: **la API entrega las páginas del frontend y responde a sus peticiones desde la
misma dirección** (`localhost:8080`). Así el navegador trata todo como un solo sitio: la cookie de sesión del mentor
funciona (por seguridad no se envía a otros dominios) y no hace falta configurar permisos entre dominios (CORS).

## Ejecutar

### Opción 1: con Docker (recomendada)

Requisito: [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado y **abierto**.

```bash
git clone https://github.com/AnthonyPilatasig/mentorias-programacion.git
cd mentorias-programacion
docker compose up --build
```

La primera vez tarda unos minutos (descarga MySQL y compila la API). Cuando la consola muestre
`Now listening on: http://[::]:8080`, abre:

| URL | Qué es |
|---|---|
| http://localhost:8080 | Sitio de mentorías; la sección «Reserva tu sesión» guarda en MySQL |
| http://localhost:8080/panel.html | Panel del mentor (`mentor@mentorias.dev` / `Mentor#2026`) |
| http://localhost:8080/docs | Documentación interactiva de la API |
| http://localhost:8080/health | Estado de la API y de MySQL |

Para detener todo: `Ctrl + C` y luego `docker compose down` (agrega `-v` para borrar también los datos de MySQL).

Las credenciales se pueden cambiar copiando `.env.example` a `.env`. MySQL queda expuesto en el puerto **3307**
del equipo (no el 3306) para no chocar con un MySQL instalado localmente.

### Opción 2: API con `dotnet run` (para programar)

Requisitos: Docker Desktop (solo para MySQL) y [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
docker compose up -d db                          # solo MySQL
dotnet run --project backend/src/Mentorias.Api   # API en http://localhost:5212
```

En modo desarrollo la API lee las páginas directamente de `frontend/`, así que los cambios en HTML, CSS o JS se
ven al recargar el navegador, sin reconstruir nada.

### Opción 3: solo el frontend

Abrir `frontend/index.html` o la versión publicada en
[GitHub Pages](https://anthonypilatasig.github.io/mentorias-programacion/). El sitio, el menú y el filtro funcionan,
pero como no hay API detrás, el formulario avisa que el servicio de reservas no está disponible.

## Pruebas

```bash
cd backend
dotnet test          # 90 pruebas; las de integración levantan su propio MySQL con Docker
npx newman run postman/Mentorias.postman_collection.json -e postman/local.postman_environment.json

cd ../frontend
npm ci && npm test   # node --check y html-validate
```

La colección de Postman crea y luego borra sus propios datos, así que se puede repetir. Si se ejecuta varias
veces dentro del mismo minuto, `POST /api/reservas` responde **429**: es el limitador de peticiones (10 por minuto
por IP) funcionando. `backend/requests/mentorias.http` tiene las mismas peticiones para VS Code o Visual Studio.

## Arquitectura del backend

```
backend/src/Mentorias.Api
├── Domain/          Entidades y reglas PURAS: Agenda (horarios), ReglasReserva (estados),
│                    ReglasArchivo (tipos y firmas), ReglasLogin (bloqueo). Sin E/S.
├── Contracts/       DTOs de petición y respuesta (records inmutables).
├── Application/     Casos de uso (ReservaService, AdjuntoService, AuthService, CatalogoService)
│                    + funciones puras Validadores y Mapeos + catálogo de Errores.
├── Infrastructure/  EF Core/MySQL, migraciones, almacén de archivos y de sesiones.
├── Realtime/        Hub de SignalR y notificador.
├── Endpoints/       Rutas HTTP y traducción de resultados a códigos HTTP / ProblemDetails.
└── Configuracion/   Registro de servicios y orden del pipeline.
```

Cada petición sigue el mismo flujo: **endpoint → servicio → función pura de validación → E/S (MySQL) → mapeo
puro a DTO → notificación**. Los servicios no lanzan excepciones para casos esperados: devuelven
`Resultado<T>` (valor o `Error`) y un único punto (`ProblemasHttp`) lo convierte en respuesta HTTP.

## Endpoints

| Método | Ruta | Acceso | Respuestas |
|---|---|---|---|
| GET | `/api/mentorias?categoria=` | público | 200 |
| GET | `/api/mentorias/{id}` | público | 200, 404 |
| GET | `/api/horarios?fecha=&mentoriaId=` | público | 200, 400 |
| POST | `/api/reservas` | público (10/min por IP) | 201 + `Location` + `ETag`, 400, 409, 429 |
| GET | `/api/reservas?estado=&desde=&hasta=&correo=&pagina=&tamano=` | mentor | 200, 400, 401 |
| GET | `/api/reservas/{id}` | mentor | 200 + `ETag`, 404 |
| PUT | `/api/reservas/{id}` | mentor, `If-Match` | 200, 400, 404, 409, 412, 428 |
| PATCH | `/api/reservas/{id}/estado` | mentor, `If-Match` | 200, 400, 404, 409, 412, 428 |
| DELETE | `/api/reservas/{id}` | mentor, `If-Match` opcional | 204, 404, 412 |
| POST | `/api/reservas/{id}/adjuntos` | mentor (multipart, 2 MB) | 201, 400, 404, 409, 413, 415 |
| GET | `/api/reservas/{id}/adjuntos` | mentor | 200, 404 |
| GET | `/api/adjuntos/{id}` | mentor | 200 (descarga), 404 |
| DELETE | `/api/adjuntos/{id}` | mentor | 204, 404 |
| POST | `/api/auth/login` | público (10/min por IP) | 200 + cookie, 400, 401, 429 |
| POST | `/api/auth/logout` | público | 204 |
| GET | `/api/auth/me` | mentor | 200, 401 |

### Ejemplo: crear una reserva

```http
POST /api/reservas
Content-Type: application/json

{ "mentoriaId": 3, "nombre": "María José Andrade", "correo": "maria.andrade@correo.com",
  "nivel": "intermedio", "mensaje": "Herencia e interfaces", "inicio": "2026-11-10T10:00:00-05:00" }
```

```http
HTTP/1.1 201 Created
Location: /api/reservas/7
ETag: "1"

{ "id": 7, "mentoria": { "id": 3, "titulo": "Programación orientada a objetos", "duracionMinutos": 90 },
  "nombre": "María José Andrade", "correo": "maria.andrade@correo.com", "nivel": "intermedio",
  "mensaje": "Herencia e interfaces", "inicio": "2026-11-10T10:00:00-05:00", "fin": "2026-11-10T11:30:00-05:00",
  "estado": "pendiente", "version": 1, "creadaUtc": "...", "actualizadaUtc": "..." }
```

### Formato uniforme de errores (RFC 9457)

Todas las respuestas de error, incluidas las que genera el framework (401, 404, 405, JSON mal formado,
cuerpo demasiado grande), usan `application/problem+json` con un `codigo` estable para el cliente:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflicto con el estado actual",
  "status": 409,
  "detail": "El horario elegido se cruza con otra reserva. Consulta GET /api/horarios para ver los disponibles.",
  "instance": "/api/reservas",
  "codigo": "horario_ocupado",
  "traceId": "00-aa15858ceea79138..."
}
```

Las validaciones (400) añaden `errors` con un mensaje por campo; el formulario del frontend los muestra junto a
cada control y en el resumen de errores accesible de la Unidad 1.

## Reglas de negocio y manejo de conflictos

- Sesiones de lunes a sábado entre 08:00 y 20:00 (hora de Ecuador, UTC-5), inicio en punto o a la media hora,
  con al menos 2 horas de anticipación y hasta 60 días adelante. La duración depende de la mentoría (60 o 90 min).
- **Doble reserva:** cada reserva ocupa bloques de 30 min en la tabla `BloquesAgenda`, cuya clave primaria es el
  instante de inicio. Aunque dos peticiones lleguen al mismo tiempo, MySQL rechaza la segunda (error 1062) y la API
  responde 409. Lo comprueba una prueba que lanza 10 reservas simultáneas al mismo horario: solo una gana.
- **Ediciones concurrentes:** `Version` es token de concurrencia y se expone como `ETag`. `PUT` y `PATCH` exigen
  `If-Match` (428 si falta, 412 si la versión no coincide).
- **Estados:** pendiente → confirmada | cancelada; confirmada → completada | cancelada. Cancelar libera el horario.
- Consultas siempre parametrizadas mediante EF Core (sin SQL concatenado).

## Sesiones, archivos y tiempo real

- **Sesión del mentor:** contraseña con hash PBKDF2 (`PasswordHasher`), cookie `HttpOnly` y `SameSite=Strict`,
  expiración deslizante de 2 h. El ticket se guarda en la tabla `Sesiones`: cerrar sesión lo borra y una cookie
  copiada deja de funcionar. Tras 5 intentos fallidos el correo queda bloqueado 15 minutos (429 + `Retry-After`).
- **Adjuntos:** lista blanca de extensiones (pdf, png, jpg, zip y archivos de código como texto), verificación
  de la firma real del contenido, máximo 2 MB y 5 por reserva, nombre saneado, guardado fuera de la carpeta
  pública con un GUID como nombre, hash SHA-256 y descarga siempre como `attachment` con `nosniff`.
- **SignalR** (`/hubs/reservas`):

| Evento | Destinatarios | Datos |
|---|---|---|
| `horariosActualizados` | todos | `{ fechas: ["2026-11-10"] }` (sin datos personales) |
| `reservaCreada`, `reservaActualizada` | mentor autenticado | la reserva completa |
| `reservaEliminada` | mentor | `{ id }` |
| `adjuntoAgregado`, `adjuntoEliminado` | mentor | adjunto / `{ reservaId, id }` |

## Evidencias

- [`frontend/evidencias/`](frontend/evidencias): capturas de la Unidad 1 (360, 768 y 1280 px).
- [`backend/evidencias/`](backend/evidencias): capturas de la Unidad 2 (reserva, errores del servidor,
  actualización en vivo entre dos navegadores, panel del mentor, móvil, documentación) y la salida completa de
  `dotnet test` y de Newman.
