import { api, ErrorApi, conectarHub, fechaHora, escapar, TEXTO_ESTADO_HUB } from './api.js';

const $ = (id) => document.getElementById(id);
const lista = $('reservas');
const actividad = $('actividad');
const estado = { pagina: 1, totalPaginas: 1, reservas: new Map(), hub: null };

// Acciones que permite cada estado (reflejo de ReglasReserva en el servidor).
const ACCIONES = {
  pendiente: [['confirmada', 'Confirmar', 'btn'], ['cancelada', 'Cancelar', 'btn-peligro']],
  confirmada: [['completada', 'Marcar completada', 'btn'], ['cancelada', 'Cancelar', 'btn-peligro']],
  completada: [],
  cancelada: [],
};

function mostrarVista(vista) {
  $('vista-login').hidden = vista !== 'login';
  $('vista-panel').hidden = vista !== 'panel';
}

function avisar(texto) {
  $('aviso').textContent = texto;
  $('aviso').hidden = !texto;
}

function registrar(texto) {
  const li = document.createElement('li');
  li.innerHTML = `<time>${new Date().toLocaleTimeString('es-EC', { hour12: false })}</time>${texto}`;
  actividad.prepend(li);
}

function tarjeta(r, nueva = false) {
  const acciones = ACCIONES[r.estado]
    .map(([destino, texto, clase]) => `<button type="button" class="btn btn-sm ${clase}" data-accion="estado" data-destino="${destino}">${texto}</button>`)
    .join('');
  return `<li class="reserva${nueva ? ' nueva' : ''}" id="reserva-${r.id}" data-id="${r.id}">
    <header>
      <h3>#${r.id} · ${escapar(r.mentoria.titulo)}</h3>
      <span class="estado estado-${r.estado}">${r.estado}</span>
    </header>
    <dl>
      <dt>Cuándo</dt><dd>${fechaHora(r.inicio)} (${r.mentoria.duracionMinutos} min)</dd>
      <dt>Estudiante</dt><dd>${escapar(r.nombre)} · <a href="mailto:${escapar(r.correo)}">${escapar(r.correo)}</a></dd>
      <dt>Nivel</dt><dd>${r.nivel}</dd>
      ${r.mensaje ? `<dt>Objetivo</dt><dd>${escapar(r.mensaje)}</dd>` : ''}
      <dt>Versión</dt><dd>${r.version}</dd>
    </dl>
    <div class="acciones">
      ${acciones}
      <button type="button" class="btn btn-sm btn-secundario" data-accion="eliminar">Eliminar</button>
    </div>
    <div class="adjuntos">
      <strong>Adjuntos</strong>
      <ul data-adjuntos></ul>
      <form data-subir>
        <label class="visually-hidden" for="archivo-${r.id}">Archivo para la reserva ${r.id}</label>
        <input id="archivo-${r.id}" name="archivo" type="file" accept=".pdf,.png,.jpg,.jpeg,.zip,.txt,.md,.java,.cs,.js,.py,.sql,.html,.css" required>
        <button type="submit" class="btn btn-sm btn-secundario">Subir</button>
      </form>
    </div>
  </li>`;
}

async function cargarReservas() {
  const filtro = $('filtro-estado').value;
  const { datos } = await api(`/api/reservas?pagina=${estado.pagina}&tamano=10${filtro ? `&estado=${filtro}` : ''}`);
  estado.totalPaginas = Math.max(datos.totalPaginas, 1);
  estado.reservas = new Map(datos.items.map((r) => [r.id, r]));
  lista.innerHTML = datos.items.map((r) => tarjeta(r)).join('');
  $('conteo').textContent = `${datos.total} reservas${filtro ? ` en estado ${filtro}` : ''}.`;
  $('pag-info').textContent = `Página ${datos.pagina} de ${estado.totalPaginas}`;
  $('pag-anterior').disabled = estado.pagina <= 1;
  $('pag-siguiente').disabled = estado.pagina >= estado.totalPaginas;
  await Promise.all(datos.items.map((r) => cargarAdjuntos(r.id)));
}

async function cargarAdjuntos(id) {
  const ul = document.querySelector(`#reserva-${id} [data-adjuntos]`);
  if (!ul) return;
  const { datos } = await api(`/api/reservas/${id}/adjuntos`);
  ul.innerHTML = datos.length
    ? datos.map((a) => `<li><a href="/api/adjuntos/${a.id}" download>${escapar(a.nombre)}</a> (${Math.ceil(a.tamanoBytes / 1024)} KB)
        <button type="button" class="btn btn-sm btn-peligro" data-accion="borrar-adjunto" data-adjunto="${a.id}">Quitar</button></li>`).join('')
    : '<li class="hint">Sin adjuntos</li>';
}

function reemplazar(r, nueva = false) {
  estado.reservas.set(r.id, r);
  const actual = $(`reserva-${r.id}`);
  if (actual) {
    actual.outerHTML = tarjeta(r, nueva);
    cargarAdjuntos(r.id);
  }
}

/** Errores esperables explicados al usuario; 412 = otro cambio llegó antes. */
async function manejarError(e) {
  if (e instanceof ErrorApi && e.status === 401) return iniciar();
  avisar(e.message);
  if (e instanceof ErrorApi && e.status === 412) await cargarReservas();
}

lista.addEventListener('click', async (ev) => {
  const boton = ev.target.closest('button[data-accion]');
  if (!boton) return;
  const id = Number(boton.closest('.reserva').dataset.id);
  const reserva = estado.reservas.get(id);
  avisar('');
  try {
    if (boton.dataset.accion === 'estado') {
      const { datos } = await api(`/api/reservas/${id}/estado`, { method: 'PATCH', json: { estado: boton.dataset.destino }, etag: `"${reserva.version}"` });
      reemplazar(datos);
    } else if (boton.dataset.accion === 'eliminar') {
      // Confirmación en dos pasos, sin diálogos modales del navegador.
      if (boton.dataset.confirmar !== 'si') {
        boton.dataset.confirmar = 'si';
        boton.textContent = '¿Seguro? Eliminar';
        boton.classList.replace('btn-secundario', 'btn-peligro');
        return;
      }
      await api(`/api/reservas/${id}`, { method: 'DELETE', etag: `"${reserva.version}"` });
      $(`reserva-${id}`)?.remove();
    } else if (boton.dataset.accion === 'borrar-adjunto') {
      await api(`/api/adjuntos/${boton.dataset.adjunto}`, { method: 'DELETE' });
      await cargarAdjuntos(id);
    }
  } catch (e) {
    await manejarError(e);
  }
});

lista.addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const formulario = ev.target.closest('form[data-subir]');
  const id = Number(formulario.closest('.reserva').dataset.id);
  avisar('');
  try {
    await api(`/api/reservas/${id}/adjuntos`, { method: 'POST', form: new FormData(formulario) });
    formulario.reset();
    await cargarAdjuntos(id);
  } catch (e) {
    await manejarError(e);
  }
});

$('form-login').addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const error = $('login-error');
  error.hidden = true;
  try {
    await api('/api/auth/login', { method: 'POST', json: Object.fromEntries(new FormData(ev.target)) });
    ev.target.reset();
    await iniciar();
  } catch (e) {
    error.textContent = e.message;
    error.hidden = false;
    error.focus();
  }
});

$('btn-logout').addEventListener('click', async () => {
  await api('/api/auth/logout', { method: 'POST' });
  await estado.hub?.stop();
  mostrarVista('login');
  $('login-correo').focus();
});

$('filtro-estado').addEventListener('change', () => { estado.pagina = 1; cargarReservas().catch(manejarError); });
$('pag-anterior').addEventListener('click', () => { estado.pagina--; cargarReservas().catch(manejarError); });
$('pag-siguiente').addEventListener('click', () => { estado.pagina++; cargarReservas().catch(manejarError); });

function conectar() {
  // El servidor decide el grupo al conectar, por eso se conecta después de iniciar sesión.
  estado.hub = conectarHub(
    {
      reservaCreada: (r) => {
        registrar(`Nueva reserva <strong>#${r.id}</strong> de ${escapar(r.nombre)} para ${fechaHora(r.inicio)}.`);
        if (estado.pagina === 1 && !$('filtro-estado').value) {
          estado.reservas.set(r.id, r);
          lista.insertAdjacentHTML('afterbegin', tarjeta(r, true));
          cargarAdjuntos(r.id);
        }
      },
      reservaActualizada: (r) => {
        registrar(`Reserva <strong>#${r.id}</strong> ahora está <strong>${r.estado}</strong> (versión ${r.version}).`);
        reemplazar(r, true);
      },
      reservaEliminada: ({ id }) => {
        registrar(`Reserva <strong>#${id}</strong> eliminada.`);
        $(`reserva-${id}`)?.remove();
      },
      adjuntoAgregado: (a) => {
        registrar(`Adjunto <strong>${escapar(a.nombre)}</strong> en la reserva #${a.reservaId}.`);
        cargarAdjuntos(a.reservaId);
      },
      adjuntoEliminado: ({ reservaId }) => cargarAdjuntos(reservaId),
    },
    (e) => {
      $('estado-hub').dataset.estado = e;
      $('estado-hub').textContent = TEXTO_ESTADO_HUB[e];
    },
  );
}

async function iniciar() {
  try {
    const { datos } = await api('/api/auth/me');
    $('saludo').textContent = `Sesión de ${datos.nombre} (${datos.correo}).`;
    mostrarVista('panel');
    await cargarReservas();
    if (!estado.hub || estado.hub.state === 'Disconnected') conectar();
  } catch (e) {
    if (e instanceof ErrorApi && e.status === 401) {
      mostrarVista('login');
    } else {
      mostrarVista('panel');
      avisar(e.message);
    }
  }
}

iniciar();
