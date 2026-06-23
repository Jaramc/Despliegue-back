# RentalAI — Backend

Plataforma de gestión de rentas cortas con validación de identidad asistida por IA,
chatbot inteligente y dashboard de rendimiento para propietarios.

Construida sobre **.NET 10** como núcleo, con **Laravel 11** para notificaciones y
**Docker Compose** para levantar el entorno completo con un solo comando.

---

## Repositorios del proyecto

| Repo | Descripción |
|------|-------------|
| **rental-ai-backend** *(este repo)* | Microservicios .NET 10, worker Laravel, infraestructura |
| [rental-ai-frontend](https://github.com/TU_USUARIO/rental-ai-frontend) | Aplicación Next.js 14 |

---

## Requisitos previos

| Herramienta | Versión mínima | Verificar |
|-------------|---------------|-----------|
| Docker Desktop / Docker Engine | 24.x | `docker --version` |
| Docker Compose | 2.x (plugin) | `docker compose version` |
| Git | 2.x | `git --version` |
| RAM disponible | 8 GB | — |

> El proyecto **no requiere** tener .NET, PHP ni Node instalados localmente.
> Todo corre dentro de contenedores Docker.

---

## Levantar el proyecto

### 1. Clonar el repositorio

```bash
git clone https://github.com/Jaramc/rental-ai-backend
cd rental-ai-backend
```

### 2. Configurar variables de entorno

```bash
cp .env.example .env
```

Abre `.env` y reemplaza **obligatoriamente** estos tres valores:

```env
OPENAI_API_KEY=sk-proj-TU_CLAVE_REAL_DE_OPENAI
MAIL_USERNAME=TU_USUARIO_MAILTRAP
MAIL_PASSWORD=TU_PASSWORD_MAILTRAP
```

> Para las claves de cifrado (`JWT_SECRET`, `KYC_ENCRYPTION_KEY`) los valores
> de desarrollo ya están pre-generados en `.env.example` para conveniencia.
> **Nunca uses esos valores en producción.**

### 3. Levantar todos los servicios

```bash
docker compose up -d --build
```

La primera vez descarga las imágenes base (~3 min). Las migraciones de base de datos
se ejecutan automáticamente al iniciar cada servicio.

### 4. Verificar que todo está corriendo

```bash
docker compose ps
```

Todos los servicios deben mostrar estado `healthy`.

---

## URLs disponibles tras levantar

| Servicio | URL | Credenciales |
|----------|-----|--------------|
| **API Gateway** | http://localhost:5000 | — |
| **Swagger UI** | http://localhost:5000/swagger | — |
| **RabbitMQ Management** | http://localhost:15672 | ver `RABBITMQ_*` en `.env` |
| **MinIO Console** | http://localhost:9001 | ver `MINIO_*` en `.env` |
| **phpMyAdmin** | http://localhost:5050 | ver `DB_*` en `.env` |
| **Seq (logs)** | http://localhost:5341 | — |
| **Qdrant Dashboard** | http://localhost:6333/dashboard | — |

---

## Comandos útiles

```bash
# Ver logs en tiempo real de un servicio específico
docker compose logs -f auth-service

# Ver logs de todos los servicios
docker compose logs -f

# Detener sin borrar datos
docker compose stop

# Detener y eliminar contenedores (conserva volúmenes de datos)
docker compose down

# Reseteo completo (BORRA todos los datos)
docker compose down -v

# Reconstruir solo un servicio tras cambios
docker compose up -d --build booking-service

# Ejecutar tests dentro de un servicio
docker compose exec auth-service dotnet test
```

---

## Arquitectura

### Decisiones técnicas clave

#### Prevención de double-booking
La disponibilidad se valida en dos capas. Primero, un **distributed lock en Redis**
(`lock:property:{id}:{checkIn}:{checkOut}`, TTL 30s) bloquea el recurso durante
la transacción para prevenir condiciones de carrera ante requests concurrentes.
Segundo, una **constraint UNIQUE en MySQL** sobre `(property_id, check_in, check_out)`
garantiza integridad a nivel de datos incluso si el lock fallara. Como MySQL 8 no
soporta índices únicos parciales, la unicidad se limita a las reservas confirmadas
mediante una columna generada que solo toma valor cuando `status = 'confirmed'`.

#### Estandarización de horarios
Toda reserva confirmada fija automáticamente el check-in a las **14:00** y el
check-out a las **12:00** en la zona horaria del inmueble. Las horas se aplican
en el servidor al confirmar y no son editables por el usuario, de modo que la
política es consistente en todo el sistema.

#### Notificaciones omnicanal
Los servicios .NET publican eventos de dominio en **RabbitMQ** (vía MassTransit) y
el **worker de Laravel** los consume para despachar alertas por **correo** y
**dentro de la aplicación**: confirmación de reserva, veredicto de KYC y
recordatorios de llegada y salida. Desacoplar la mensajería del flujo principal
evita que un fallo de envío bloquee la reserva.

#### KYC seguro
Los documentos de identidad se cifran con **AES-256-GCM** antes de almacenarse
en MinIO, con una clave derivada por usuario usando HKDF. Un job de Hangfire
borra el documento 24 horas después de la validación (sobrescritura de bytes +
eliminación lógica). El texto extraído por IA se almacena hasheado; solo el
veredicto final (aprobado/rechazado) persiste en claro.

#### Chatbot con RAG
El chatbot usa **Retrieval-Augmented Generation**: los inmuebles se indexan como
embeddings en Qdrant usando `text-embedding-3-small`. Cuando el usuario hace una
pregunta, se buscan semánticamente los inmuebles más relevantes y se pasan como
contexto a GPT-4o mini. El chatbot también tiene acceso a herramientas
(function calling) para consultar disponibilidad y el estado de reservas del
usuario autenticado en tiempo real.

#### Autenticación diferida
El catálogo es completamente público (sin JWT). La wishlist se mantiene en una
cookie anónima firmada. El sistema solicita login únicamente al confirmar una
reserva o al guardar favoritos de forma permanente; al hacer login, fusiona
automáticamente los favoritos anónimos con el perfil registrado.

### Mapa de servicios

```
Cliente (Next.js)
    │
    ▼
API Gateway :5000 (YARP)
    │
    ├── /auth/*         → Auth Service
    ├── /properties/*   → Properties Service
    ├── /bookings/*     → Booking Service
    ├── /kyc/*          → KYC Service
    ├── /users/*        → Users Service
    ├── /dashboard/*    → Dashboard Service
    ├── /chat/*         → Chatbot Service
    └── /files/*        → Files Service
             │
             ▼ (eventos vía RabbitMQ)
    Notification Service (Laravel)
```

### Stack completo

| Capa | Tecnología |
|------|-----------|
| Servicios core | .NET 10 / ASP.NET Core |
| ORM | Entity Framework Core 10 |
| Mensajería | RabbitMQ + MassTransit |
| Background jobs | Hangfire |
| Notificaciones | Laravel 11 + Queues |
| Base de datos | MySQL 8.4 |
| Caché / Locks | Redis 7 |
| Almacenamiento | MinIO (S3-compatible) |
| Vector DB | Qdrant |
| IA — OCR / Chat | OpenAI GPT-4o / GPT-4o mini |
| Logging | Serilog → Seq |
| API Gateway | YARP |

---

## Estructura del repositorio

```
rental-ai-backend/
├── src/
│   ├── gateway/                  # YARP API Gateway (.NET 10)
│   ├── services/
│   │   ├── RentalAI.Auth/
│   │   ├── RentalAI.Properties/
│   │   ├── RentalAI.Booking/
│   │   ├── RentalAI.KYC/
│   │   ├── RentalAI.Users/
│   │   ├── RentalAI.Dashboard/
│   │   ├── RentalAI.Chatbot/
│   │   └── RentalAI.Files/
│   ├── workers/
│   │   └── notification-service/ # Laravel 11
│   └── shared/
│       ├── RentalAI.Contracts/   # DTOs y eventos compartidos
│       └── RentalAI.Common/      # Middleware, helpers
├── infra/
│   ├── docker/                   # Dockerfiles
│   └── mysql/                    # Configuración / init de MySQL (opcional)
├── docs/
│   └── architecture/             # ADRs y diagramas
├── .github/
│   └── workflows/                # CI/CD
├── docker-compose.yml
├── .env.example
└── README.md
```

---

## CI/CD

Cada pull request a `develop` o `main` ejecuta automáticamente:
- Build de todos los proyectos .NET
- Suite de tests (unitarios + integración)
- Análisis estático de código

Los merges a `main` construyen y publican las imágenes Docker a GitHub Container Registry.

---

## Contribuir

1. Crear una rama desde `develop`: `git checkout -b feature/nombre-de-la-feature`
2. Hacer commits descriptivos en inglés, en imperativo (`Add booking lock`)
3. Abrir un Pull Request hacia `develop`
4. Los tests deben pasar antes del merge

---

## Licencia

[MIT](LICENSE)