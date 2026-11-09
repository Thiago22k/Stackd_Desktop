// Stackd API — servidor intermedio (Cloudflare Worker).
//
// Guarda las claves que no deben ir dentro de la app (API key de Steam, Client Secret de IGDB/Twitch)
// y reenvía únicamente las consultas que Stackd necesita.
//
// Variables secretas (Settings → Variables and Secrets):
//   STEAM_API_KEY, TWITCH_CLIENT_ID, TWITCH_CLIENT_SECRET

const STEAM_BASE = 'https://api.steampowered.com';

// Métodos de Steam permitidos y los parámetros que se aceptan en cada uno.
const STEAM_ALLOWED = {
  'ISteamUser/GetPlayerSummaries/v2': { required: ['steamids'], optional: [] },
  'IPlayerService/GetOwnedGames/v1': {
    required: ['steamid'],
    optional: ['include_appinfo', 'include_played_free_games', 'format'],
  },
  'ISteamUserStats/GetPlayerAchievements/v1': { required: ['steamid', 'appid'], optional: [] },
};

const PARAM_PATTERNS = {
  steamid: /^\d{17}$/,
  steamids: /^\d{17}$/,
  appid: /^\d{1,10}$/,
  include_appinfo: /^[01]$/,
  include_played_free_games: /^[01]$/,
  format: /^json$/,
};

// Endpoints de IGDB permitidos.
const IGDB_ALLOWED = new Set(['games']);
const IGDB_MAX_BODY = 4000;

let twitchToken = null;
let twitchTokenExpiresAt = 0;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    try {
      if (url.pathname === '/health') {
        return json({
          ok: true,
          steam: Boolean(env.STEAM_API_KEY),
          igdb: Boolean(env.TWITCH_CLIENT_ID && env.TWITCH_CLIENT_SECRET),
        });
      }

      if (url.pathname.startsWith('/steam/')) return await handleSteam(request, url, env);
      if (url.pathname.startsWith('/igdb/v4/')) return await handleIgdb(request, url, env);

      return json({ error: 'Not found' }, 404);
    } catch (err) {
      return json({ error: 'Upstream error' }, 502);
    }
  },
};

// ---------------------------------------------------------------- Steam

async function handleSteam(request, url, env) {
  if (request.method !== 'GET') return json({ error: 'Method not allowed' }, 405);
  if (!env.STEAM_API_KEY) return json({ error: 'Server is not configured' }, 500);

  const path = url.pathname.slice('/steam/'.length).replace(/\/+$/, '');
  const rule = STEAM_ALLOWED[path];
  if (!rule) return json({ error: 'Not found' }, 404);

  const upstream = new URL(`${STEAM_BASE}/${path}/`);

  for (const name of [...rule.required, ...rule.optional]) {
    const value = url.searchParams.get(name);
    if (value === null) {
      if (rule.required.includes(name)) return json({ error: `Missing ${name}` }, 400);
      continue;
    }
    if (!PARAM_PATTERNS[name].test(value)) return json({ error: `Invalid ${name}` }, 400);
    upstream.searchParams.set(name, value);
  }

  upstream.searchParams.set('key', env.STEAM_API_KEY);

  const response = await fetch(upstream.toString(), {
    // Cachea unos minutos para repartir menos peticiones a Steam si el usuario sincroniza varias veces seguidas.
    cf: { cacheTtl: 120, cacheEverything: true },
  });

  return new Response(response.body, {
    status: response.status,
    headers: { 'content-type': 'application/json; charset=utf-8' },
  });
}

// ----------------------------------------------------------------- IGDB

async function handleIgdb(request, url, env) {
  if (request.method !== 'POST') return json({ error: 'Method not allowed' }, 405);
  if (!env.TWITCH_CLIENT_ID || !env.TWITCH_CLIENT_SECRET) {
    return json({ error: 'Server is not configured' }, 500);
  }

  const endpoint = url.pathname.slice('/igdb/v4/'.length).replace(/\/+$/, '');
  if (!IGDB_ALLOWED.has(endpoint)) return json({ error: 'Not found' }, 404);

  const body = await request.text();
  if (body.length > IGDB_MAX_BODY) return json({ error: 'Query too long' }, 413);

  let response = await callIgdb(endpoint, body, env, false);
  if (response.status === 401) {
    // El token pudo caducar o revocarse: se renueva una vez.
    response = await callIgdb(endpoint, body, env, true);
  }

  return new Response(response.body, {
    status: response.status,
    headers: { 'content-type': 'application/json; charset=utf-8' },
  });
}

async function callIgdb(endpoint, body, env, forceNewToken) {
  const token = await getTwitchToken(env, forceNewToken);
  return fetch(`https://api.igdb.com/v4/${endpoint}`, {
    method: 'POST',
    headers: {
      'Client-ID': env.TWITCH_CLIENT_ID,
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'text/plain',
    },
    body,
  });
}

async function getTwitchToken(env, forceNew) {
  const now = Date.now();
  if (!forceNew && twitchToken && now < twitchTokenExpiresAt) return twitchToken;

  const params = new URLSearchParams({
    client_id: env.TWITCH_CLIENT_ID,
    client_secret: env.TWITCH_CLIENT_SECRET,
    grant_type: 'client_credentials',
  });

  const response = await fetch(`https://id.twitch.tv/oauth2/token?${params}`, { method: 'POST' });
  if (!response.ok) throw new Error('Twitch token request failed');

  const data = await response.json();
  twitchToken = data.access_token;
  twitchTokenExpiresAt = now + Math.max((data.expires_in ?? 3600) - 300, 60) * 1000;
  return twitchToken;
}

// ---------------------------------------------------------------- utils

function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8' },
  });
}
