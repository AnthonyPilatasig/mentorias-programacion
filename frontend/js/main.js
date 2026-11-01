/* Mentorías de código — interacciones (JavaScript sin frameworks)
   1) Menú adaptable con teclado  2) Filtro de mentorías
   3) Reserva conectada a la API: horarios en vivo (SignalR), validación accesible
      en el cliente y errores por campo devueltos por el servidor (ProblemDetails). */
(function () {
  "use strict";

  /* ---------- 1) Menú adaptable ---------- */
  function initMenu() {
    var boton = document.querySelector(".nav-toggle");
    var menu = document.getElementById("menu-principal");
    if (!boton || !menu) return;

    function abrir(abierto) {
      menu.classList.toggle("is-open", abierto);
      boton.setAttribute("aria-expanded", String(abierto));
    }

    boton.addEventListener("click", function () {
      abrir(boton.getAttribute("aria-expanded") !== "true");
    });

    // Esc cierra el menú y devuelve el foco al botón.
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && boton.getAttribute("aria-expanded") === "true") {
        abrir(false);
        boton.focus();
      }
    });

    // Al elegir un enlace en móvil, el menú se cierra.
    menu.addEventListener("click", function (e) {
      if (e.target.closest("a")) abrir(false);
    });
  }

  /* ---------- 2) Filtro de mentorías ---------- */
  function initFiltro() {
    var botones = document.querySelectorAll(".chip");
    var tarjetas = document.querySelectorAll("#lista-mentorias .card");
    var estado = document.getElementById("filtro-estado");
    if (!botones.length || !tarjetas.length || !estado) return;

    function aplicar(tema) {
      var visibles = 0;
      tarjetas.forEach(function (tarjeta) {
        var coincide = tema === "todos" || tarjeta.dataset.tema === tema;
        tarjeta.hidden = !coincide;
        if (coincide) visibles++;
      });
      botones.forEach(function (b) {
        b.setAttribute("aria-pressed", String(b.dataset.filter === tema));
      });
      // role="status" anuncia el cambio a lectores de pantalla.
      estado.textContent = "Mostrando " + visibles + " de " + tarjetas.length + " mentorías.";
    }

    botones.forEach(function (b) {
      b.addEventListener("click", function () { aplicar(b.dataset.filter); });
    });
  }

  /* ---------- 3) Reserva con la API ---------- */
  // Mismo origen por defecto (la API sirve este sitio). En GitHub Pages no hay backend.
  var API = typeof window.MENTORIAS_API === "string" ? window.MENTORIAS_API : "";
  var ZONA = "America/Guayaquil";
  var SIN_SERVIDOR = "No se pudo conectar con el servicio de reservas. Si ves la versión estática (GitHub Pages), ejecuta el backend con docker compose up.";
  var fmtIso = new Intl.DateTimeFormat("en-CA", { timeZone: ZONA });
  var fmtHora = new Intl.DateTimeFormat("es-EC", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: ZONA });
  var fmtFecha = new Intl.DateTimeFormat("es-EC", { dateStyle: "full", timeStyle: "short", timeZone: ZONA });

  /** fetch + JSON; un error HTTP se rechaza con el cuerpo ProblemDetails. */
  function pedir(ruta, opciones) {
    return fetch(API + ruta, opciones).then(
      function (r) {
        var tipo = r.headers.get("Content-Type") || "";
        var cuerpo = tipo.indexOf("json") >= 0 ? r.json() : Promise.resolve(null);
        return cuerpo.then(function (datos) {
          if (!r.ok) throw { status: r.status, problema: datos || {} };
          return datos;
        });
      },
      function () { throw { status: 0, problema: { detail: SIN_SERVIDOR } }; }
    );
  }

  function hoyEcuador(dias) { return fmtIso.format(new Date(Date.now() + (dias || 0) * 864e5)); }

  function primerDiaHabil() {
    for (var d = 1; d < 8; d++) {
      var iso = hoyEcuador(d);
      if (new Date(iso + "T12:00:00-05:00").getUTCDay() !== 0) return iso;
    }
  }

  var REGLAS = {
    nombre: function (f) {
      var v = f.elements.nombre.value.trim();
      if (!v) return "Escribe tu nombre completo.";
      if (v.length < 3) return "El nombre debe tener al menos 3 caracteres.";
      return "";
    },
    correo: function (f) {
      var v = f.elements.correo.value.trim();
      if (!v) return "Escribe tu correo electrónico.";
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) return "Escribe un correo válido, por ejemplo nombre@dominio.com.";
      return "";
    },
    mentoriaId: function (f) {
      return f.elements.mentoriaId.value ? "" : "Selecciona una mentoría.";
    },
    fecha: function (f) {
      return f.elements.fecha.value ? "" : "Elige el día de la sesión.";
    },
    inicio: function (f) {
      return f.querySelector('[name="inicio"]:checked') ? "" : "Elige un horario disponible.";
    },
    nivel: function (f) {
      return f.elements.nivel.value ? "" : "Elige tu nivel actual.";
    }
  };

  var ETIQUETAS = {
    nombre: "Nombre completo", correo: "Correo electrónico", mentoriaId: "Mentoría", fecha: "Día",
    inicio: "Horario", nivel: "Tu nivel actual", mensaje: "¿Qué quieres lograr?"
  };

  var GRUPOS = { nivel: true, inicio: true }; // radios: el error se marca en todo el grupo

  function campoDe(form, nombre) {
    // Para radios, el primer input habilitado sirve de destino del foco.
    if (nombre === "inicio") return form.querySelector('[name="inicio"]:not(:disabled)') || form.elements.fecha;
    return form.querySelector('[name="' + nombre + '"]');
  }

  function mostrarError(form, nombre, mensaje) {
    var msg = document.getElementById(nombre + "-error");
    var campo = campoDe(form, nombre);
    if (!msg || !campo) return;
    msg.textContent = mensaje;
    msg.hidden = !mensaje;
    var destinos = GRUPOS[nombre] ? form.querySelectorAll('[name="' + nombre + '"]') : [campo];
    Array.prototype.forEach.call(destinos, function (el) {
      if (mensaje) el.setAttribute("aria-invalid", "true"); else el.removeAttribute("aria-invalid");
    });
  }

  function initFormulario() {
    var form = document.getElementById("form-contacto");
    var resumen = document.getElementById("resumen-errores");
    var lista = document.getElementById("lista-errores");
    var exito = document.getElementById("exito");
    var horarios = document.getElementById("horarios");
    var estadoHorarios = document.getElementById("horarios-estado");
    if (!form || !resumen || !lista || !exito || !horarios || !estadoHorarios) return;
    var fecha = form.elements.fecha;
    var mentoria = form.elements.mentoriaId;

    function validarCampo(nombre) {
      var error = REGLAS[nombre](form);
      mostrarError(form, nombre, error);
      return error;
    }

    function validarTodo() {
      var errores = [];
      Object.keys(REGLAS).forEach(function (nombre) {
        var error = validarCampo(nombre);
        if (error) errores.push({ nombre: nombre, mensaje: error });
      });
      return errores;
    }

    function pintarResumen(errores) {
      lista.textContent = "";
      errores.forEach(function (e) {
        var li = document.createElement("li");
        if (!e.nombre) {
          li.textContent = e.mensaje; // error general, sin campo asociado
        } else {
          var a = document.createElement("a");
          a.href = "#" + e.nombre + "-error";
          a.textContent = (ETIQUETAS[e.nombre] || e.nombre) + ": " + e.mensaje;
          a.addEventListener("click", function (ev) {
            ev.preventDefault();
            campoDe(form, e.nombre).focus();
          });
          li.appendChild(a);
        }
        lista.appendChild(li);
      });
      resumen.hidden = errores.length === 0;
      if (errores.length) resumen.focus(); // el lector de pantalla lee el resumen
    }

    /** Errores del servidor: 400 trae "errors" por campo; 409 se asocia al horario. */
    function mostrarErroresServidor(e) {
      var errores = [];
      var porCampo = e.problema.errors || (e.problema.codigo === "horario_ocupado" ? { inicio: [e.problema.detail] } : null);
      if (porCampo) {
        Object.keys(porCampo).forEach(function (nombre) {
          mostrarError(form, nombre, porCampo[nombre][0]);
          errores.push({ nombre: document.getElementById(nombre + "-error") ? nombre : null, mensaje: porCampo[nombre][0] });
        });
      } else {
        errores.push({ nombre: null, mensaje: e.problema.detail || "No se pudo registrar la reserva." });
      }
      pintarResumen(errores);
    }

    function pintarHorarios(datos, elegido, anunciar) {
      var antes = {};
      horarios.querySelectorAll("input:not(:disabled)").forEach(function (i) { antes[i.value] = true; });
      horarios.textContent = "";
      var libres = 0;
      datos.forEach(function (h) {
        var label = document.createElement("label");
        label.className = "slot" + (anunciar && antes[h.inicio] && !h.disponible ? " recien" : "");
        var input = document.createElement("input");
        input.type = "radio";
        input.name = "inicio";
        input.value = h.inicio;
        input.disabled = !h.disponible;
        input.checked = h.disponible && h.inicio === elegido;
        var texto = document.createElement("span");
        texto.textContent = fmtHora.format(new Date(h.inicio));
        if (h.disponible) {
          libres++;
        } else {
          var oculto = document.createElement("span");
          oculto.className = "visually-hidden";
          oculto.textContent = " (ocupado)";
          texto.appendChild(oculto);
        }
        label.appendChild(input);
        label.appendChild(texto);
        horarios.appendChild(label);
      });
      var perdido = elegido && !datos.some(function (h) { return h.inicio === elegido && h.disponible; });
      estadoHorarios.textContent =
        datos.length === 0 ? "Los domingos no hay sesiones. Elige otro día."
        : perdido && anunciar ? "El horario que elegiste acaba de ser reservado por otra persona. Elige otro."
        : anunciar ? "Los horarios se actualizaron: " + libres + " libres."
        : libres + " horarios libres. Se actualizan en tiempo real.";
    }

    function cargarHorarios(anunciar) {
      var marcado = form.querySelector('[name="inicio"]:checked');
      var elegido = marcado ? marcado.value : null;
      if (!mentoria.value || !fecha.value) {
        horarios.textContent = "";
        estadoHorarios.textContent = "Elige una mentoría y un día para ver los horarios libres.";
        return Promise.resolve();
      }
      return pedir("/api/horarios?fecha=" + fecha.value + "&mentoriaId=" + mentoria.value).then(
        function (datos) { pintarHorarios(datos.horarios, elegido, anunciar); },
        function (e) {
          horarios.textContent = "";
          var errores = e.problema.errors;
          estadoHorarios.textContent = (errores && errores.fecha && errores.fecha[0]) || e.problema.detail;
        }
      );
    }

    function conectarTiempoReal() {
      if (!window.signalR) return;
      var conexion = new window.signalR.HubConnectionBuilder()
        .withUrl(API + "/hubs/reservas")
        .withAutomaticReconnect()
        .build();
      conexion.on("horariosActualizados", function (e) {
        if (e.fechas.indexOf(fecha.value) >= 0) cargarHorarios(true);
      });
      conexion.start().catch(function () { /* sin backend: el formulario lo indica al enviar */ });
    }

    form.addEventListener("submit", function (ev) {
      ev.preventDefault();
      exito.hidden = true;
      var errores = validarTodo();
      pintarResumen(errores);
      if (errores.length) return;

      var boton = form.querySelector('button[type="submit"]');
      boton.disabled = true;
      pedir("/api/reservas", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          mentoriaId: Number(mentoria.value),
          nombre: form.elements.nombre.value,
          correo: form.elements.correo.value,
          nivel: form.elements.nivel.value,
          mensaje: form.elements.mensaje.value,
          inicio: form.querySelector('[name="inicio"]:checked').value
        })
      }).then(
        function (r) {
          exito.textContent = "Gracias, " + r.nombre.split(" ")[0] + ". Tu reserva #" + r.id + " de " + r.mentoria.titulo +
            " para el " + fmtFecha.format(new Date(r.inicio)) + " quedó registrada. Te escribiré cuando la confirme.";
          exito.hidden = false;
          var dia = fecha.value;
          var tema = mentoria.value;
          form.reset();
          fecha.value = dia;
          mentoria.value = tema;
          exito.focus();
          return cargarHorarios(false);
        },
        function (e) {
          mostrarErroresServidor(e);
          if (e.status === 409) return cargarHorarios(true);
        }
      ).then(function () { boton.disabled = false; });
    });

    // Un error ya mostrado se revalida mientras el usuario corrige (input/change).
    // No se usa "blur": ocultar un mensaje al perder el foco desplaza el formulario
    // justo cuando se hace clic en el campo siguiente y el clic puede fallar.
    function revalidar(ev) {
      var nombre = ev.target.name;
      if (REGLAS[nombre] && ev.target.getAttribute("aria-invalid") === "true") validarCampo(nombre);
    }
    form.addEventListener("input", revalidar);
    form.addEventListener("change", revalidar);

    mentoria.addEventListener("change", function () { cargarHorarios(false); });
    fecha.addEventListener("change", function () { cargarHorarios(false); });
    fecha.min = hoyEcuador(0);
    fecha.max = hoyEcuador(60);
    fecha.value = primerDiaHabil();
    conectarTiempoReal();
  }

  document.addEventListener("DOMContentLoaded", function () {
    initMenu();
    initFiltro();
    initFormulario();
  });
})();
