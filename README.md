# Stackd

Biblioteca personal de videojuegos para Windows (WinUI 3, .NET 8).

## Requisitos
- Windows 10 (1809+) o Windows 11
- Visual Studio 2022 17.8+ con la carga de trabajo **Desarrollo de escritorio con .NET** y el componente **Windows App SDK C# Templates**
- SDK de .NET 8

## Cómo ejecutarlo
1. Abre `Stackd.sln` en Visual Studio.
2. Elige la plataforma **x64** (o ARM64 según tu equipo).
3. Presiona **F5**. La primera vez restaurará los paquetes NuGet.

La base de datos se crea sola en `%LOCALAPPDATA%\Stackd\stackd.db`.
En modo Debug se insertan 2 juegos de ejemplo para poder ver la interfaz.

Las credenciales de IGDB(Twitch) se guardan en un archivo `secrets.json` en `%APPDATA%\Stackd\secrets.json`

## Estructura
```
Stackd/
├─ App.xaml(.cs)          Arranque + inyección de dependencias
├─ MainWindow.xaml(.cs)   Shell: barra lateral + marco de contenido
├─ Themes/Theme.xaml      Colores, tipografía y estilos (del diseño de Canva)
├─ Models/                Game, Collection, PlayStatus
├─ Data/                  AppDbContext (EF Core + SQLite), inicializador
├─ Services/              Navegación (aquí irán IGDB, Steam, hardware, IA)
├─ ViewModels/            MVVM con CommunityToolkit.Mvvm
└─ Views/                 HomePage, PlaceholderPage (el resto de páginas)
```

## Hoja de ruta
1. [x] **Esqueleto**: navegación, tema, BD, Home.
2. [x] **Biblioteca manual**: añadir/editar/eliminar juegos, estado, horas, notas, prevención de ejecuciones duplicadas, paso a segundo plano (liberación de GPU/RAM) al jugar y opciones de ordenación.
3. [x] **IGDB**: autenticación OAuth2 con Twitch, credenciales en secrets.json, Explore (Popular Games, Most Anticipated), buscador en tiempo real, importación a la biblioteca y vinculación (Match IGDB) en juegos manuales.
4. Collections (listas personalizadas).
5. Steam: vincular cuenta, importar juegos, horas y logros.
6. Hardware Profile: detección de CPU/GPU/RAM y compatibilidad.
7. Recommended: recomendaciones con IA.
8. Pantallas de login/registro (según el diseño) o modo local.

## Notas
- Los iconos de la barra lateral son glifos de Segoe Fluent Icons; se pueden cambiar por los del diseño.
- El Client Secret de IGDB (Twitch) no debe ir dentro de la app distribuida: en la fase 3 se decide dónde guardarlo.
