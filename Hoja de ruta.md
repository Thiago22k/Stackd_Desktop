# Stackd: resumen y hoja de ruta (para continuar en otro chat)

## Qué es
Aplicación nativa de Windows: biblioteca personal de videojuegos. Registra lo que juegas, horas y logros (Steam), explora y busca con IGDB, tiene listas personalizadas y estadísticas.
Repo: https://github.com/Thiago22k/Stackd_Desktop

## Stack
C# + .NET 8, WinUI 3 (Windows App SDK 1.6, sin empaquetar), MVVM con CommunityToolkit.Mvvm, inyección de dependencias con Microsoft.Extensions.Hosting, EF Core + SQLite (`%LOCALAPPDATA%\Stackd\stackd.db`).

## Estructura (carpetas del repo)
- `Models/`: Game, Collection, PlayStatus, IgdbGame, CollectionItem, PickItems, SteamModels, StatsModels.
- `Data/`: AppDbContext, DbInitializer (EnsureCreated + parches EnsureColumn), AppPaths.
- `Services/`: NavigationService, GameLauncher, IgdbService, SteamService, SteamOpenIdLogin, CollectionService, SecretsService, AppConfig.
- `ViewModels/`: Shell, Home, Library, Explore, Collections, CollectionDetail, Stats.
- `Views/`: Home, Library, Explore, Collections, CollectionDetail, Stats, PlaceholderPage y los diálogos (GameEdit, SteamLink, CollectionEdit, GamePicker, CollectionPicker, credenciales IGDB).
- `Themes/Theme.xaml`: paleta y estilos del design system v2 (fondo oscuro, acento amarillo, violeta). Tipografía Radio Canada con Segoe UI como respaldo.
- `Assets/`: logotipo.png, isotipo.png, no_cover.png.
- `server/worker.js` + `server/README.md`: Cloudflare Worker.

## Lo que ya está hecho
1. Esqueleto: barra lateral, tema, base de datos, Home.
2. Biblioteca manual: añadir juegos eligiendo el .exe (título propuesto desde la carpeta), editar, borrar, estados, horas, notas, búsqueda, filtro por estado, ordenación, "Continue playing" con conteo de sesión, prevención de ejecuciones duplicadas y paso a segundo plano al jugar.
3. IGDB: OAuth con Twitch, Explore (Popular Games, Most Anticipated), buscador, importar a la biblioteca y "Match IGDB" en juegos manuales.
4. Collections: crear, editar, borrar, mosaico de portadas, añadir y quitar juegos, "Add to collection…" desde Library.
5. Steam: iniciar sesión con Steam (OpenID), importar juegos, horas, última vez jugado, portadas y logros, vincular con juegos manuales por nombre, abrir juegos con steam://rungameid, sincronización automática cada 24 h, insignia STEAM y filtro de origen en Library.
6. Servidor en Cloudflare Workers: guarda la API key de Steam y el Client Secret de IGDB. La app lo usa si hay URL configurada en `AppConfig.DefaultServerUrl` o en `ProxyUrl` de secrets.json. Sin servidor, pide la API key de Steam y usa las credenciales de IGDB locales.
7. Stats: horas totales, juegos, completados, logros, barras por estado, más jugados, progreso de logros, datos sueltos y desglose de géneros más jugados.
8. Rendimiento y caché: ImageCache y ResponseCache en disco para portadas y respuestas IGDB, CachedImageConverter y persistencia de géneros en base de datos.

## Configuración
`%LOCALAPPDATA%\Stackd\secrets.json` puede tener: IgdbClientId, IgdbClientSecret, SteamApiKey, SteamId, SteamPersonaName, SteamLastSyncUtc y ProxyUrl. Los secretos reales no se pegan en el chat.

## Decisiones tomadas
- Todo es local: no hay cuentas ni inicio de sesión propio, aunque los diseños tienen pantallas de login y registro.
- Recomendaciones con IA: se dejan para más adelante (requieren una IA de pago y mucho pulido).
- Comparación de requisitos (qué juegos corre mi PC): se deja para después. Los requisitos de Steam vienen como texto libre y hace falta una base de benchmarks.
- Hardware Profile: primero solo detección.

## Pendientes y deuda técnica
- Migraciones de EF Core en lugar de EnsureCreated y EnsureColumn (antes de repartir la app).
- No hay historial de sesiones, así que no hay actividad por semana ni por mes.
- Las sesiones de juegos de Steam abiertos con steam:// no se cuentan en Stackd (las horas llegan al sincronizar).
- En Explore no se puede añadir a una colección sin importar antes el juego.
- Library no tiene filtro por colección.
- La barra lateral colapsable de los diseños no está hecha.
- Falta el icono .ico y un instalador.
- Placeholders: Recommended, Hardware Profile, Settings.

## Hoja de ruta propuesta
**Siguiente (corto plazo)**
1. Settings: cuentas (Steam e IGDB), estado del servidor, sincronización automática y borrar datos locales.
2. Hardware Profile mínimo: CPU, GPU, RAM, almacenamiento y Windows.
3. Migraciones de base de datos.
4. Icono (.ico desde el isotipo) e instalador.

**Después**
7. Barra lateral colapsable.
8. Filtro por colección en Library y añadir a colección desde Explore.
9. Historial de sesiones (tabla GameSession) para estadísticas de actividad.
10. "Because you played X" con juegos similares de IGDB (sin IA de pago).

**Más adelante**
11. Recomendaciones con IA.
12. Comparación de requisitos, solo juegos de Steam, con tres niveles aproximados.
13. Cuentas y sincronización en la nube (usaría las pantallas de login y registro).
14. Otros lanzadores (Epic, GOG, Xbox) y detección de juegos instalados.