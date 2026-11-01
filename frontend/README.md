# Mentorías de código

Sitio web responsivo y accesible que promociona un servicio de mentorías de programación en línea.
Proyecto de **Aplicaciones Web, Unidad 1** (Universidad Politécnica Salesiana).

> **Unidad 2:** el formulario «Reserva tu sesión» ya no simula el envío: consulta los horarios libres en la API
> del [backend](../backend), se actualiza en tiempo real con SignalR y muestra los errores por campo que devuelve el
> servidor. Se agregó `panel.html` para el mentor. Instrucciones en el [README principal](../README.md).

- Demostración: https://anthonypilatasig.github.io/mentorias-programacion/
- Autor: Anthony Pilatasig · https://github.com/AnthonyPilatasig

## Estructura

```
index.html          Documento HTML5 semántico (5 secciones)
css/styles.css      CSS mobile first con variables, Flexbox/Grid y 2 breakpoints
js/main.js          Menú adaptable, filtro de mentorías y reserva con la API (validación accesible)
panel.html          Panel del mentor: sesión, reservas en vivo, cambios de estado y adjuntos (Unidad 2)
css/panel.css       Estilos del panel
js/api.js, panel.js Cliente de la API y lógica del panel (módulos ES)
js/lib/             Cliente de SignalR (@microsoft/signalr 8.0.7)
img/                Ilustraciones SVG con texto alternativo
evidencias/         Capturas a 360, 768 y 1280 px y pruebas a 360 px
pruebas/            Resultados de las pruebas automáticas
```

## Cómo ejecutarlo

Con el backend: `docker compose up --build` en la raíz del repositorio y abre http://localhost:8080.
Sin backend (por ejemplo, en GitHub Pages) el sitio funciona, pero el formulario avisa que el servicio de reservas no está disponible.

## Accesibilidad y pruebas

- Enlace «Saltar al contenido», foco visible, orden de tabulación lógico y menú operable con teclado (Enter y Esc).
- Contraste mínimo 4,5:1 en texto; axe-core sin violaciones a 360, 768 y 1280 px.
- Sin desbordamiento horizontal de 320 a 1440 px.
- Sin JavaScript el menú sigue visible (mejora progresiva).

El formulario valida en el cliente pero no envía datos: el contenido es de ejemplo.
