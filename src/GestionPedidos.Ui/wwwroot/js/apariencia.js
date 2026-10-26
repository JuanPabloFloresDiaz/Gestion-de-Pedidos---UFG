// Apariencia (modo claro/oscuro/sistema y color de acento): se aplica ANTES de pintar para que no haya parpadeo.
// La preferencia se guarda en localStorage del propio navegador/WebView (no es un dato sensible).
window.gpApariencia = {
  claveModo: 'gp.modo',
  claveAcento: 'gp.acento',
  leer: function () {
    try {
      return [localStorage.getItem(this.claveModo) || 'sistema', localStorage.getItem(this.claveAcento) || 'azul'];
    } catch (e) { return ['sistema', 'azul']; }
  },
  aplicar: function (modo, acento) {
    var raiz = document.documentElement;
    if (modo === 'claro' || modo === 'oscuro') raiz.setAttribute('data-modo', modo); else raiz.removeAttribute('data-modo');
    raiz.setAttribute('data-acento', acento || 'azul');
  },
  establecer: function (modo, acento) {
    this.aplicar(modo, acento);
    try { localStorage.setItem(this.claveModo, modo); localStorage.setItem(this.claveAcento, acento); } catch (e) { }
  }
};
(function () { var v = window.gpApariencia.leer(); window.gpApariencia.aplicar(v[0], v[1]); })();
