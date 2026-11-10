# Stackd

Biblioteca personal de videojuegos para Windows (WinUI 3, .NET 8). Registra tu tiempo de juego, sincroniza con Steam, explora y cataloga con IGDB, organiza en colecciones personalizadas y analiza tus estadísticas.

---

## Características Implementadas

- **🎮 Biblioteca y Gestión de Juegos**:
  - Registro manual de juegos mediante selección de `.exe` (detección de título y ruta).
  - Estados de juego: *Playing, Backlog, Completed, Abandoned, Wishlist*.
  - Conteo de tiempo de juego en horas y minutos con registro de última sesión.
  - Lanzamiento directo ("Continue playing") con prevención de instancias duplicadas y paso a segundo plano (liberación de memoria RAM/GPU mientras juegas).
  - Búsqueda en tiempo real, filtros por estado y opciones de ordenación (A-Z, Z-A, más jugados, recientemente jugados).

- **🌐 Integración con IGDB (Twitch API)**:
  - Búsqueda dual y en tiempo real de videojuegos.
  - Secciones en Explore: *Popular Games* y *Most Anticipated*.
  - Importación directa a la biblioteca con portadas en alta resolución (`t_cover_big`), sinopsis, calificaciones y géneros.
  - "Match IGDB" en juegos manuales mediante panel inline integrado.

- **💨 Integración con Steam (OpenID)**:
  - Inicio de sesión oficial con **Steam OpenID 2.0** a través del navegador (sin exponer contraseñas ni exigir API Key al usuario).
  - Importación de catálogo, horas jugadas, fecha de última partida, portadas y progreso de logros.
  - Lanzamiento mediante protocolo oficial `steam://run/<appid>`.
  - Sincronización automática de datos cada 24 horas.

- **📁 Colecciones Personalizadas (Collections)**:
  - Creación, edición y eliminación de colecciones con colores personalizables.
  - Mosaico dinámico de portadas 2x2.
  - Diálogos para añadir/quitar juegos de colecciones y acción rápida "Add to collection..." desde el menú contextual de la biblioteca.

- **📊 Pantalla de Estadísticas (Stats)**:
  - Resumen global: horas totales jugadas, cantidad de juegos, completados y logros conseguidos.
  - Gráfico de distribución de biblioteca por estado.
  - Top de juegos más jugados y progreso detallado de logros por juego.
  - Desglose y métricas de **géneros más jugados**.

- **⚡ Rendimiento y Caché Local**:
  - **Caché en disco de imágenes** (`ImageCache`) con fallback a carátula genérica (`no_cover.png`).
  - **Caché de consultas IGDB** (`ResponseCache`) para navegación instantánea y ahorro de peticiones.
  - `CachedImageConverter` optimizado para carga asíncrona de recursos gráficos en WinUI 3.

- **☁️ Servidor Proxy Opcional (Cloudflare Worker)**:
  - Soporte para servidor intermedio (`server/worker.js`) que resguarda las claves de API (Steam Web API Key y Twitch Client Secret).
  - Configurable desde `AppConfig.DefaultServerUrl` o mediante `ProxyUrl` en `secrets.json`.

- **🎨 Design System v2**:
  - Paleta sobria en modo oscuro con contraste accesible (WCAG AA).
  - Tipografía **Radio Canada** (con respaldo a *Segoe UI Variable Display*).
  - Tokens semánticos, bordes de 1px por capas y estilos de botones planos con hover y pressed nativos.

---

## Requisitos

- Windows 10 (1809+) o Windows 11
- Visual Studio 2022 (17.8+) con las cargas de trabajo:
  - **Desarrollo de escritorio con .NET**
  - **Windows App SDK C# Templates**
- SDK de .NET 8

---

## Cómo Ejecutarlo

1. Clona el repositorio:
   ```bash
   git clone https://github.com/Thiago22k/Stackd_Desktop.git
   ```
2. Abre `Stackd.sln` en Visual Studio 2022.
3. Selecciona la plataforma **x64** (o **ARM64** según tu arquitectura).
4. Presiona **F5** (depuración) o **Ctrl + F5**. Los paquetes NuGet se restaurarán automáticamente.

---

## Almacenamiento y Configuración

- **Base de datos local**: SQLite ubicada en `%LOCALAPPDATA%\Stackd\stackd.db`.
- **Caché de imágenes y API**: almacenado en subcarpetas dentro de `%LOCALAPPDATA%\Stackd\`.
- **Credenciales y Secretos**: se guardan de forma segura en `%LOCALAPPDATA%\Stackd\secrets.json` o `%APPDATA%\Stackd\secrets.json`:
  ```json
  {
    "IgdbClientId": "tu_client_id",
    "IgdbClientSecret": "tu_client_secret",
    "SteamApiKey": "opcional_si_no_usas_worker",
    "SteamId": "tu_steamid64",
    "SteamPersonaName": "nombre_de_usuario",
    "ProxyUrl": "https://tu-worker.workers.dev"
  }
  ```

---

## Estructura del Proyecto

```
Stackd/
├─ Converters/           CachedImageConverter para carga y renderizado optimizado
├─ Data/                 AppDbContext (EF Core + SQLite), DbInitializer, AppPaths
├─ Models/               Game, Collection, CollectionItem, PickItems, SteamModels, StatsModels, IgdbGame
├─ Services/             GameLauncher, IgdbService, SteamService, SteamOpenIdLogin, 
│                        CollectionService, GameMetadataService, ImageCache, ResponseCache, SecretsService
├─ Themes/               Theme.xaml (Design System v2: colores, tipografía, estilos)
├─ ViewModels/           Shell, Home, Library, Explore, Collections, CollectionDetail, Stats
├─ Views/                HomePage, LibraryPage, ExplorePage, CollectionsPage, CollectionDetailPage, StatsPage
│                        Diálogos: GameEditDialog, SteamLinkDialog, CollectionEditDialog, GamePickerDialog, etc.
├─ server/               Cloudflare Worker (worker.js y README.md) para proxy seguro de APIs
└─ Assets/               Logotipos, isotipo e imágenes de respaldo
```

---

## Hoja de Ruta

- [x] **Paso 1: Esqueleto base**: navegación, tema, BD SQLite, Home.
- [x] **Paso 2: Biblioteca manual**: CRUD de juegos, launcher con segundo plano y minimización de recursos.
- [x] **Paso 3: Integración IGDB**: Explore, buscador en tiempo real, vinculación con juegos manuales.
- [x] **Paso 4: Colecciones**: agrupación de juegos, mosaico de portadas, asignación múltiple.
- [x] **Paso 5: Integración Steam**: login con OpenID, sincronización de horas, última partida y logros.
- [x] **Paso 6: Estadísticas**: horas jugadas, estados, logros y géneros más jugados.
- [x] **Paso 7: Caché y optimización**: caché local en disco para imágenes y respuestas IGDB.
- [ ] **Paso 8: Settings (Configuración)**: gestión de cuentas vinculadas, sincronización manual, limpieza de caché.
- [ ] **Paso 9: Hardware Profile**: detección de componentes (CPU, GPU, RAM) y evaluación básica de requisitos.
- [ ] **Paso 10: Recommended**: recomendaciones basadas en juegos similares de la biblioteca.
- [ ] **Paso 11: Empaquetado**: icono `.ico` e instalador para distribución.
