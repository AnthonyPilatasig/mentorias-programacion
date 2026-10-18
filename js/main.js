/* Mentorías de código — interacciones (JavaScript sin dependencias)
   1) Menú adaptable con teclado  2) Filtro de mentorías
   3) Validación accesible del formulario */
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

  /* ---------- 3) Validación accesible ---------- */
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
    tema: function (f) {
      return f.elements.tema.value ? "" : "Selecciona un tema de mentoría.";
    },
    nivel: function (f) {
      return f.elements.nivel.value ? "" : "Elige tu nivel actual.";
    }
  };

  var ETIQUETAS = { nombre: "Nombre completo", correo: "Correo electrónico", tema: "Tema de la mentoría", nivel: "Tu nivel actual" };

  function campoDe(form, nombre) {
    // Para radios, el primer input sirve de destino del foco.
    return form.querySelector('[name="' + nombre + '"]');
  }

  function mostrarError(form, nombre, mensaje) {
    var msg = document.getElementById(nombre + "-error");
    var campo = campoDe(form, nombre);
    if (!msg || !campo) return;
    msg.textContent = mensaje;
    msg.hidden = !mensaje;
    if (nombre === "nivel") {
      form.querySelectorAll('[name="nivel"]').forEach(function (r) {
        if (mensaje) r.setAttribute("aria-invalid", "true"); else r.removeAttribute("aria-invalid");
      });
    } else if (mensaje) {
      campo.setAttribute("aria-invalid", "true");
    } else {
      campo.removeAttribute("aria-invalid");
    }
  }

  function initFormulario() {
    var form = document.getElementById("form-contacto");
    var resumen = document.getElementById("resumen-errores");
    var lista = document.getElementById("lista-errores");
    var exito = document.getElementById("exito");
    if (!form || !resumen || !lista || !exito) return;

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
        var a = document.createElement("a");
        a.href = "#" + (e.nombre === "nivel" ? "nivel-error" : e.nombre);
        a.textContent = ETIQUETAS[e.nombre] + ": " + e.mensaje;
        a.addEventListener("click", function (ev) {
          ev.preventDefault();
          campoDe(form, e.nombre).focus();
        });
        li.appendChild(a);
        lista.appendChild(li);
      });
      resumen.hidden = errores.length === 0;
    }

    form.addEventListener("submit", function (ev) {
      ev.preventDefault();
      exito.hidden = true;
      var errores = validarTodo();
      pintarResumen(errores);
      if (errores.length) {
        resumen.focus(); // el lector de pantalla lee el resumen
        return;
      }
      // Sin servidor: se simula el envío y se confirma al usuario.
      var nombre = form.elements.nombre.value.trim().split(" ")[0];
      exito.textContent = "Gracias, " + nombre + ". Recibí tu solicitud y te escribiré al correo indicado.";
      exito.hidden = false;
      form.reset();
      exito.focus();
    });

    // Un error ya mostrado se revalida mientras el usuario corrige (input/change).
    // No se usa "blur": ocultar un mensaje al perder el foco desplaza el formulario
    // justo cuando se hace clic en el campo siguiente y el clic puede fallar.
    Object.keys(REGLAS).forEach(function (nombre) {
      form.querySelectorAll('[name="' + nombre + '"]').forEach(function (el) {
        function revalidar() {
          if (el.getAttribute("aria-invalid") === "true") validarCampo(nombre);
        }
        el.addEventListener("input", revalidar);
        el.addEventListener("change", revalidar);
      });
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    initMenu();
    initFiltro();
    initFormulario();
  });
})();
