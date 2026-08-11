# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/),
y este proyecto se adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [1.4.11] - 2026-08-11

### 🔒 Seguridad
- **Hash de contraseñas BCrypt**: Migración de almacenamiento en texto plano a BCrypt (work factor 12) con migración automática en login
- **Validación SQL**: Prevención de inyección SQL en `CorrectionModule` con whitelist de tablas (`DINGR`, `MPACI`) y regex de identificadores
- **Encriptación de config**: PBKDF2-SHA256 (100k iteraciones) + AES-GCM con salt aleatorio por encriptación; versionado de formato (`RFIO1:`)
- **Rate limiting GitHub API**: Reintentos con backoff exponencial para 429/5xx; manejo de 401 reintentando sin token

### 🐛 Correcciones Críticas
- **ConfigCrypto**: Fixed bug donde `Encrypt` usaba SHA256 directo pero `Decrypt` usaba PBKDF2 — las claves no coincidían y la config no se guardaba
- **FirebirdDbService**: Eliminada conexión persistente (stale); ahora usa pool real con `CreateConnection()` por operación
- **UpdaterService**: HttpClient singleton con lifecycle correcto (evita socket exhaustion); disposal en `ReficioUpdater`
- **ReficioUpdater**: Async/await completo (sin `.GetAwaiter().GetResult()`); soporte single-file y folder publish

### ⚡ Mejoras de Arquitectura
- **Target Framework**: net10.0 → net8.0 (LTS)
- **LogService**: Logging centralizado en `%LocalAppData%\Reficio\Logs` con buffer asíncrono y rotación diaria
- **Build System**: ReficioUpdater embebido como `EmbeddedResource`; single-file publish self-contained (win-x64)
- **GitHub Repo Config**: Externalizado (owner/repo/host) como propiedades estáticas configurables

### 🎨 UI/UX
- Eliminado handler `Closed` duplicado en MainWindow
- SettingsWindow usa evento `RequestClose` tipado en lugar de texto frágil
- EditDialog y SettingsWindow con constructores sin parámetros para XAML loader
- Supresión warnings CS4014 con `_ = Task.Run(...)`

### 📦 Dependencias
- Añadido `BCrypt.Net-Next 4.0.3` para hashing seguro

### 🔧 Internos
- `FbConnectionStringBuilder` con propiedades correctas (`UserID` no `User`)
- `ObjectDisposedException.ThrowIf` para validación de estado
- Configuración migrada automáticamente de texto plano legacy al nuevo formato encriptado

## [1.4.9] - 2026-08-10
- Update sin token para repo público + mensajes 404 accionables

## [1.4.8] - Anterior
- Subprograma ReficioUpdater para actualizaciones independientes

---

### Detalle técnico v1.4.11

| Componente | Cambio |
|------------|--------|
| `AuthService` | BCrypt.Verify/HashPassword/GenerateSalt/PasswordNeedsRehash(12) |
| `CorrectionModule` | Validación regex `^[A-Za-z_][A-Za-z0-9_]*$`; whitelist tablas |
| `ConfigCrypto` | PBKDF2 + AES-GCM; payload: version(1) + salt(16) + nonce(12) + tag(16) + cipher |
| `FirebirdDbService` | Connection pooling real; FbConnectionStringBuilder.UserID |
| `UpdaterService` | HttpClient singleton; retry 429/5xx backoff; 401 retry sin token |
| `ReficioUpdater` | Async/await; HttpClient disposal; zip structure single-file |
| `LogService` | `%LocalAppData%\Reficio\Logs\reficio_yyyyMMdd.log`; flush 5s |
| Build | `EmbeddedResource` updater; single-file self-contained win-x64 |

### Archivos modificados v1.4.11
- `Reficio.csproj`, `ReficioUpdater/ReficioUpdater.csproj` — versión 1.4.11, net8.0
- `Services/AuthService.cs` — BCrypt hash + migración auto
- `Services/ConfigCrypto.cs` — PBKDF2 fix encrypt/decrypt
- `Services/CorrectionModule.cs` — SQL injection prevention
- `Services/FirebirdDbService.cs` — Pooling real
- `Services/FirebirdTools.cs` — Logging integrado
- `Services/LogService.cs` — Nuevo logging centralizado
- `Services/UpdaterService.cs` — HttpClient lifecycle, retry logic
- `Services/ConnectionConfigService.cs` — Save/Load encriptado
- `ReficioUpdater/Program.cs` — Async/await, disposal
- `ViewModels/MainViewModel.cs` — `_ = Task.Run`, logging
- `ViewModels/LoginViewModel.cs` — Migración hash en login
- `ViewModels/SettingsViewModel.cs` — RequestClose event
- `Views/EditDialog.axaml.cs` — Constructor sin parámetros
- `Views/SettingsWindow.axaml.cs` — Constructor sin parámetros
- `MainWindow.axaml.cs` — Removido Closed handler duplicado