# Mentorías de código

Sitio web responsivo y accesible que promociona un servicio de mentorías de programación en línea.
Proyecto de **Aplicaciones Web, Unidad 1** (Universidad Politécnica Salesiana).

- Demostración: https://anthonypilatasig.github.io/mentorias-programacion/
- Autor: Anthony Pilatasig · https://github.com/AnthonyPilatasig

## Estructura

```
index.html          Documento HTML5 semántico (5 secciones)
css/styles.css      CSS mobile first con variables, Flexbox/Grid y 2 breakpoints
js/main.js          Menú adaptable, filtro de mentorías y validación accesible
img/                Ilustraciones SVG con texto alternativo
evidencias/         Capturas a 360, 768 y 1280 px y pruebas a 360 px
pruebas/            Resultados de las pruebas automáticas
```

## Cómo ejecutarlo

No requiere instalación: abre `index.html` en el navegador.

## Accesibilidad y pruebas

- Enlace «Saltar al contenido», foco visible, orden de tabulación lógico y menú operable con teclado (Enter y Esc).
- Contraste mínimo 4,5:1 en texto; axe-core sin violaciones a 360, 768 y 1280 px.
- Sin desbordamiento horizontal de 320 a 1440 px.
- Sin JavaScript el menú sigue visible (mejora progresiva).

El formulario valida en el cliente pero no envía datos: el contenido es de ejemplo.
