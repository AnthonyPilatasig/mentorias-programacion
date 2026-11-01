// Cliente mínimo de la API: fetch + ProblemDetails + SignalR. Sin dependencias además de signalr.min.js.

export const ZONA = 'America/Guayaquil';

/** Error de la API con el cuerpo ProblemDetails (title, detail, codigo, errors...). */
export class ErrorApi extends Error {
  constructor(status, problema) {
    super(problema?.detail || problema?.title || `Error ${status}`);
    this.status = status;
    this.problema = problema ?? {};
  }
}

export async function api(ruta, { method = 'GET', json, form, etag } = {}) {
  const headers = {};
  if (json !== undefined) headers['Content-Type'] = 'application/json';
  if (etag) headers['If-Match'] = etag;

  let respuesta;
  try {
    respuesta = await fetch(ruta, {
      method,
      headers,
      body: form ?? (json !== undefined ? JSON.stringify(json) : undefined),
      credentials: 'same-origin',
    });
  } catch {
    throw new ErrorApi(0, { detail: 'No se pudo conectar con el servidor. Revisa tu conexión.' });
  }

  const tipo = respuesta.headers.get('Content-Type') ?? '';
  const datos = respuesta.status !== 204 && tipo.includes('json') ? await respuesta.json() : null;
  if (!respuesta.ok) throw new ErrorApi(respuesta.status, datos);
  return { datos, etag: respuesta.headers.get('ETag') };
}

const fmtFechaHora = new Intl.DateTimeFormat('es-EC', { dateStyle: 'full', timeStyle: 'short', timeZone: ZONA });
const fmtHora = new Intl.DateTimeFormat('es-EC', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: ZONA });
const fmtIso = new Intl.DateTimeFormat('en-CA', { timeZone: ZONA }); // AAAA-MM-DD

export const fechaHora = (iso) => fmtFechaHora.format(new Date(iso));
export const hora = (iso) => fmtHora.format(new Date(iso));
export const hoyEcuador = (dias = 0) => fmtIso.format(new Date(Date.now() + dias * 864e5));

/** Conecta al hub y reporta el estado de la conexión (conectado, reconectando, desconectado). */
export function conectarHub(eventos, alCambiarEstado) {
  const conexion = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/reservas')
    .withAutomaticReconnect()
    .build();
  for (const [nombre, manejador] of Object.entries(eventos)) conexion.on(nombre, manejador);
  conexion.onreconnecting(() => alCambiarEstado('reconectando'));
  conexion.onreconnected(() => alCambiarEstado('conectado'));
  conexion.onclose(() => alCambiarEstado('desconectado'));
  conexion.start().then(() => alCambiarEstado('conectado')).catch(() => alCambiarEstado('desconectado'));
  return conexion;
}

export const TEXTO_ESTADO_HUB = {
  conectado: 'En vivo',
  reconectando: 'Reconectando…',
  desconectado: 'Sin conexión en vivo',
};

export function escapar(texto) {
  const div = document.createElement('div');
  div.textContent = texto ?? '';
  return div.innerHTML;
}
