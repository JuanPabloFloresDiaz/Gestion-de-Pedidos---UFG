// Carrito de la tienda: se guarda en el almacenamiento local del navegador (solo productos y cantidades; ningún dato sensible).
window.gpTienda = {
  clave: 'gp.carrito',
  leer: function () {
    try { return localStorage.getItem(this.clave) || '[]'; } catch (e) { return '[]'; }
  },
  guardar: function (json) {
    try { localStorage.setItem(this.clave, json); } catch (e) { }
  },
  subir: function () { window.scrollTo({ top: 0, behavior: 'smooth' }); }
};
