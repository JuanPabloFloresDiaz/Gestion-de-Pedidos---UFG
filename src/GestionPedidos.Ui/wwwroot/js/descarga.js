// Entrega al usuario un archivo generado en memoria (un reporte). Se carga como módulo, solo cuando se usa:
//   import("./_content/GestionPedidos.Ui/js/descarga.js")
// Funciona igual en el navegador (Blazor Web) y en la ventana de escritorio (WebView2 de MAUI).
export function descargar(nombre, tipoContenido, bytes) {
  const url = URL.createObjectURL(new Blob([bytes], { type: tipoContenido }));
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  enlace.style.display = 'none';
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10000);   // se libera la memoria cuando la descarga ya empezó
}
