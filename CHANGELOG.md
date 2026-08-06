# Version

## 1.4.1 - Agosto 2026
- Corregido el módulo Reparar: los botones estaban deshabilitados porque usaban nombres de comando inexistentes (sufijo "Async" innecesario). Ahora todas las opciones están habilitadas.
- Actualizador: ahora consulta el tag más reciente del repositorio git (reficiov2@git.upc.com.mx) y descarga la versión publicada desde el registro de paquetes de git.
- Nuevo script `release.sh` que publica una versión: actualiza la versión, commitea/subue todo el código, etiqueta (tag), compila los binarios y los sube a git para que la app los descargue automáticamente.
- `publish.sh` toma la versión directamente de `Reficio.csproj` (una sola fuente de la verdad).

## 1.3.8 - Agosto 2026
- Corregido comportamiento de edición en ambos módulos (factura y paciente)
- Mensaje "Haga clic en un registro para editarlo" después de cada búsqueda
- Consistencia entre módulos: selectedRow se resetea después de cada búsqueda
- Mensajes de estado al intentar editar sin selección

## 1.3.7 - Agosto 2026
- Simplificado flujo de edición: después de guardar, solo muestra mensaje de éxito
- Usuario debe buscar y seleccionar el registro nuevamente para editar
- Evita problemas de selección con la lista después de actualizar

## 1.3.6 - Agosto 2026
- Corregido problema de selección después de editar registro en paciente
- Mensajes de estado al intentar editar sin selección
- Actualización usando fyne.Do() para thread safety

## 1.3.5 - Agosto 2026
- Corregido error de conversión de fecha en edición de paciente (FECHNACI)
- Formato de fecha compatible con Firebird (YYYY-MM-DD)

## 1.3.4 - Agosto 2026
- Dialogo de ayuda al detectar repositorio privado sin credenciales
- Script Reficio_setup_creds.bat mejorado con instrucciones paso a paso
- Manejo de errores mas amigable para usuarios

## 1.3.3 - Agosto 2026
- Logs ahora son seleccionables y copiables (widget.Entry con MultiLine)
- Versión embebida en el binario via go:embed VERSION (soluciona v0.0.0 en Windows)
- updater.SetEmbeddedVersion() para inyectar versión al iniciar

## 1.3.2 - Agosto 2026
- Updater no requiere git instalado (usa HTTP API exclusivamente)
- Soporta credenciales via ~/.git-credentials o variables GIT_USERNAME/GIT_PASSWORD
- Script Reficio_setup_creds.bat para configurar credenciales en Windows
- Mejor manejo de errores con mensajes descriptivos
- DownloadURL usa HTTPS con autenticación

## 1.3.1 - Agosto 2026
- Updater usa git credential helper para autenticación en repositorios privados
- Fallback a git ls-remote si la API HTTP falla
- Mejor manejo de errores de parseo JSON

## 1.3.0 - Agosto 2026
- Módulo de corrección de paciente usa tabla MPACI (no PACIE)
- Búsqueda de CODI: búsqueda exacta
- Búsqueda de nombre: campo NOMB (búsqueda parcial con LIKE)
- Campos de edición: NOMB, PATE, MATE, NOMBPACI, FECHNACI
- Lista muestra CODI, NOMBPACI y PATE

## 1.2.1 - Agosto 2026
- Corregido error de versión en build para Windows (VERSION file not found)
- Updater usa GitLab HTTP API en lugar de git ls-remote (no requiere git instalado)
- Búsqueda de VERSION file relativa al ejecutable

## 1.2.0 - Agosto 2026
- Módulo de corrección de paciente (tabla PACIE)
- Búsqueda por CODI y nombre del paciente
- Edición modal con campos: CODI, NOMBRE, FECHA_NAC, TELEFONO, EMAIL, DIRECCION
- Botón de buscar actualización manual
- Auto-actualización desde repositorio Git
- Versión mostrada en título de ventana y footer
- Compilación para Windows como aplicación GUI (sin consola)

## 1.0.0 - Agosto 2026
- Interfaz gráfica multiplataforma (Windows/macOS)
- Reparación de bases de datos Firebird (gstat, gfix, gbak)
- Corrección de datos en tabla DINGR
- Búsqueda exacta por CODI
- Edición modal con campos NOMBRECI, USOCFDI, REGIFISC
- Icono embebido en el ejecutable
- Barra de progreso con estados
- Conexión persistente a base de datos

## 1.1.0 - Agosto 2026
- Ventana de edición a mitad de tamaño de ventana principal
- Optimización de consultas SQL
- Mejora en el rendimiento de búsqueda

## 1.1.1 - Agosto 2026
- Corrección en ventana de edición (ShowForm)
