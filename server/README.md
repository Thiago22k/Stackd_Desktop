# Servidor de Stackd (Cloudflare Worker)

Guarda en la nube las claves que no deben ir dentro de la app y reenvía solo las consultas que Stackd necesita:

| Ruta | Qué hace | Secreto que usa |
|---|---|---|
| `GET /steam/<interfaz>/<método>/v?...` | Steam Web API (perfil, juegos, logros) | `STEAM_API_KEY` |
| `POST /igdb/v4/games` | Consultas a IGDB | `TWITCH_CLIENT_ID`, `TWITCH_CLIENT_SECRET` |
| `GET /health` | Indica si los secretos están cargados | — |

Solo se aceptan 3 métodos de Steam y el endpoint `games` de IGDB, con parámetros validados. El resto responde 404.

## Publicarlo (plan gratuito)

1. En el panel de Cloudflare: **Workers & Pages → Create → Create Worker**. Nómbralo `stackd-api` y pulsa **Deploy** (con el código de ejemplo).
2. Pulsa **Edit code**, borra todo, pega el contenido de `worker.js` y pulsa **Deploy**.
3. Ve a **Settings → Variables and Secrets → Add** y crea tres variables de tipo **Secret**:
   - `STEAM_API_KEY`: tu Steam Web API Key.
   - `TWITCH_CLIENT_ID` y `TWITCH_CLIENT_SECRET`: las credenciales de IGDB.
   Guarda con **Deploy**.
4. Copia la URL del Worker (en **Settings → Domains & Routes**), con la forma `https://stackd-api.<tu-subdominio>.workers.dev`.
5. Comprueba que funciona abriendo `<URL>/health`. Debe responder `{"ok":true,"steam":true,"igdb":true}`.

## Conectarlo a la app

- Para publicar la app: pon la URL en `Services/AppConfig.cs` → `DefaultServerUrl`.
- Para probar sin recompilar: añade `"ProxyUrl": "<URL>"` a `%LOCALAPPDATA%\Stackd\secrets.json`.

Con el servidor configurado, Stackd deja de pedir la API key de Steam y de usar el Client Secret local.

## Límites y cuidados

- El plan gratuito permite 100 000 peticiones al día. Una sincronización de Steam hace una petición por juego con horas, así que alcanza para cientos de sincronizaciones diarias.
- Cualquiera que conozca la URL puede usar el servidor. Para limitar abusos: **Security → WAF → Rate limiting rules** (el plan gratuito incluye una regla), por ejemplo 60 peticiones por minuto por IP.
- Si rotas una clave, cámbiala en **Variables and Secrets**. No hay que tocar la app.
