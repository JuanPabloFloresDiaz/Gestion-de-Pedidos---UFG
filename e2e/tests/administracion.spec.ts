import { test, expect, Page } from '@playwright/test';

// Contraseñas: la inicial viene de .env (ADMIN_INITIAL_PASSWORD); la nueva es la que se fija al primer ingreso.
const CLAVE_INICIAL = process.env.E2E_ADMIN_INICIAL ?? 'Admin_Dev_Password_123!';
const CLAVE_ADMIN = process.env.E2E_ADMIN_CLAVE ?? 'Admin_E2E_Segura_2026';
const captura = (page: Page, nombre: string) => page.screenshot({ path: `capturas/${nombre}.png` });

async function entrar(page: Page, usuario: string, clave: string) {
  await page.goto('/ingresar');
  await page.getByLabel('Usuario o correo').fill(usuario);
  await page.getByLabel('Contraseña').fill(clave);
  await page.getByRole('button', { name: 'Ingresar' }).click();
}

test('el administrador inicia sesión, cambia la clave obligatoria y entra a la administración', async ({ page }) => {
  await page.goto('/ingresar');
  await expect(page.getByRole('heading', { name: 'Gestión de Pedidos' })).toBeVisible();
  await captura(page, '01-inicio');

  // Credenciales incorrectas: mensaje genérico y sin entrar
  await entrar(page, 'admin', 'clave-incorrecta');
  await expect(page.getByRole('alert')).toContainText('Usuario o contraseña incorrectos.');
  await captura(page, '02-login-error');

  // Primera vez: la clave inicial obliga a cambiarla. Si ya se cambió en una ejecución anterior, entra con la nueva.
  await entrar(page, 'admin', CLAVE_INICIAL);
  const cambiar = page.getByRole('heading', { name: 'Cambiar contraseña' });
  const bienvenida = page.getByRole('heading', { name: /Bienvenido/ });
  const rechazo = page.getByRole('alert').filter({ hasText: 'incorrectos' });
  await cambiar.or(bienvenida).or(rechazo).waitFor();   // espera de verdad al resultado

  if (await rechazo.isVisible()) await entrar(page, 'admin', CLAVE_ADMIN);
  await cambiar.or(bienvenida).waitFor();
  if (await cambiar.isVisible()) {
    await expect(page.getByRole('alert').first()).toContainText('debe cambiar su contraseña');
    await captura(page, '03-cambio-obligatorio');
    await page.getByLabel('Contraseña actual').fill(CLAVE_INICIAL);
    await page.getByLabel('Contraseña nueva (mínimo 10 caracteres)').fill(CLAVE_ADMIN);
    await page.getByLabel('Repita la contraseña nueva').fill(CLAVE_ADMIN);
    await page.getByRole('button', { name: 'Guardar contraseña' }).click();
  }
  await expect(bienvenida).toBeVisible();
  await captura(page, '04-administracion-bienvenida');
});

test('varios módulos abiertos a la vez (pestañas), alta de usuario con notificación y cierre de sesión', async ({ page }) => {
  await entrar(page, 'admin', CLAVE_ADMIN);
  await expect(page.getByRole('heading', { name: /Bienvenido/ })).toBeVisible();

  // Abrir Usuarios desde el menú
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('menuitem', { name: 'Usuarios' }).click();
  await expect(page.getByRole('heading', { name: 'Usuarios', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'admin', exact: true })).toBeVisible();

  // Alta de un usuario nuevo: valida en pantalla antes de llamar a la API
  const usuario = `e2e${Date.now().toString().slice(-8)}`;
  await page.getByRole('button', { name: '+ Nuevo usuario' }).click();
  await page.getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByText('El usuario debe tener al menos 3 caracteres.')).toBeVisible();
  await page.getByLabel('Nombre de usuario').fill(usuario);
  await page.getByLabel('Nombre completo').fill('Persona de Prueba E2E');
  await page.getByLabel('Correo').fill(`${usuario}@ejemplo.com`);
  await page.getByLabel('Contraseña inicial (temporal, mínimo 10)').fill('ClaveTemporal_2026');
  await captura(page, '05-usuarios-nuevo');
  await page.getByRole('button', { name: 'Guardar' }).click();

  // Notificación por evento (barra de estado + aviso) y el listado se refresca solo
  await expect(page.locator('.aviso')).toContainText('registrado');
  await page.getByLabel('Buscar').first().fill(usuario);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.getByRole('cell', { name: usuario, exact: true })).toBeVisible();
  await captura(page, '06-usuarios-listado');

  // Segunda pestaña: Integrantes; la de Usuarios conserva su estado
  await page.getByRole('button', { name: /Ayuda/ }).click();
  await page.getByRole('menuitem', { name: 'Integrantes del equipo' }).click();
  await expect(page.getByRole('cell', { name: 'Juan Pablo Flores Díaz' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'MJ100326' })).toBeVisible();
  await captura(page, '07-integrantes');
  await page.getByRole('tab').filter({ hasText: 'Usuarios' }).getByRole('button').first().click();
  await expect(page.getByLabel('Buscar').first()).toHaveValue(usuario);   // estado conservado
  await expect(page.getByRole('tab')).toHaveCount(2);

  // Abrir otra vez el mismo módulo NO duplica la pestaña
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('menuitem', { name: 'Usuarios' }).click();
  await expect(page.getByRole('tab')).toHaveCount(2);

  // Dar de baja al usuario nuevo: pide confirmación; cancelar no cambia nada
  const fila = page.getByRole('row', { name: new RegExp(usuario) });
  await fila.getByRole('button', { name: 'Dar de baja', exact: true }).click();
  await expect(page.getByRole('alertdialog')).toContainText('eliminado suave');
  await captura(page, '09-confirmar-baja');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Cancelar' }).click();
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  await expect(fila.getByRole('button', { name: /Activo/ })).toBeVisible();

  // El chip de estado hace lo mismo que el botón. El listado muestra solo activos por defecto: el usuario desaparece
  const verEstado = async (estado: 'Activos' | 'Inactivos') => {
    await page.getByLabel('Estado').selectOption({ label: estado });
    await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  };
  await fila.getByRole('button', { name: /Activo/ }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await expect(page.locator('.aviso').last()).toContainText('dado de baja');
  await expect(fila).toHaveCount(0);

  // Reactivar (la baja es reversible) y volver a dar de baja
  await verEstado('Inactivos');
  await expect(fila.getByRole('button', { name: /Inactivo/ })).toBeVisible();
  await fila.getByRole('button', { name: 'Reactivar', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Reactivar' }).click();
  await expect(page.locator('.aviso').last()).toContainText('reactivado');
  await verEstado('Activos');
  await expect(fila.getByRole('button', { name: /Activo/ })).toBeVisible();
  await fila.getByRole('button', { name: 'Dar de baja', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await verEstado('Inactivos');
  await expect(fila.getByRole('button', { name: /Inactivo/ })).toBeVisible();

  // Eliminación definitiva: hay que escribir el nombre de usuario
  await fila.getByRole('button', { name: 'Eliminar definitivamente' }).click();
  const dialogo = page.getByRole('alertdialog');
  const confirmar = dialogo.getByRole('button', { name: 'Eliminar definitivamente' });
  await expect(confirmar).toBeDisabled();
  await dialogo.getByRole('textbox').fill(usuario);
  await captura(page, '10-confirmar-eliminar');
  await expect(confirmar).toBeEnabled();
  await confirmar.click();
  await expect(page.locator('.aviso').last()).toContainText('eliminado definitivamente');
  await expect(page.getByRole('row', { name: new RegExp(usuario) })).toHaveCount(0);

  // Cierre de sesión
  await page.getByRole('button', { name: /Administrador del sistema/ }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Gestión de Pedidos' })).toBeVisible();
  await expect(page).toHaveURL(/ingresar/);
});

test('un operador solo ve lo que su rol permite', async ({ page, request }) => {
  // El administrador crea al operador por la API (la interfaz de usuarios se probó arriba)
  const api = process.env.API_URL ?? 'http://localhost:4390';
  const sesion = await (await request.post(`${api}/api/v1/auth/login`, { data: { identificador: 'admin', clave: CLAVE_ADMIN } })).json();
  const operador = `op${Date.now().toString().slice(-8)}`;
  const alta = await request.post(`${api}/api/v1/usuarios`, {
    headers: { Authorization: `Bearer ${sesion.accessToken}` },
    data: { nombreUsuario: operador, nombreCompleto: 'Operadora E2E', correo: `${operador}@ejemplo.com`, rolId: 2, claveInicial: 'ClaveTemporal_2026' },
  });
  expect(alta.ok()).toBeTruthy();

  await entrar(page, operador, 'ClaveTemporal_2026');
  await expect(page.getByRole('heading', { name: 'Cambiar contraseña' })).toBeVisible();
  await page.getByLabel('Contraseña actual').fill('ClaveTemporal_2026');
  await page.getByLabel('Contraseña nueva (mínimo 10 caracteres)').fill('ClaveOperador_2026');
  await page.getByLabel('Repita la contraseña nueva').fill('ClaveOperador_2026');
  await page.getByRole('button', { name: 'Guardar contraseña' }).click();

  await expect(page.getByRole('heading', { name: /Bienvenido, Operadora E2E/ })).toBeVisible();
  // Sin permiso usuarios.ver: en Administración solo ve los catálogos geográficos; en Gestión, la clasificación (solo consulta)
  await page.getByRole('button', { name: /Administración/ }).click();
  await expect(page.getByRole('menuitem')).toHaveText(['Ciudades y departamentos']);
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('button', { name: /Gestión/ }).click();
  await page.getByRole('menuitem', { name: 'Categorías y filtros' }).click();
  await expect(page.getByRole('button', { name: /^Ropa \d+ sub\./ })).toBeVisible();
  await expect(page.getByRole('button', { name: '+ Categoría' })).toHaveCount(0);   // sin categorias.crear
  await expect(page.getByRole('button', { name: /Ayuda/ })).toBeVisible();
  await captura(page, '08-operador-menu-reducido');
});

test('roles y permisos, historial, catálogos, pestañas nuevas y apariencia', async ({ page }) => {
  await entrar(page, 'admin', CLAVE_ADMIN);
  await expect(page.getByRole('heading', { name: /Bienvenido/ })).toBeVisible();

  // Los atajos de la bienvenida toman el color de acento (no son grises fijos): cambian al elegir otro color
  const atajo = page.locator('.atajo').first();
  const fondoAzul = await atajo.evaluate((e) => getComputedStyle(e).backgroundColor);
  await page.getByRole('button', { name: /Apariencia/ }).click();
  await page.getByRole('radio', { name: /Verde/ }).click();
  await expect.poll(() => atajo.evaluate((e) => getComputedStyle(e).backgroundColor)).not.toBe(fondoAzul);
  await captura(page, '31-atajos-verde');
  await page.getByRole('radio', { name: /Azul/ }).click();
  await page.keyboard.press('Escape');

  // Tarjetas con ícono, nombre y descripción; los apartados que arman los integrantes están "en progreso"
  await expect(page.locator('.atajo .atajo-icono svg').first()).toBeVisible();
  await expect(page.locator('.atajo-desc').first()).not.toBeEmpty();
  await captura(page, '33-bienvenida-tarjetas');
  await page.locator('.atajo', { hasText: 'Clientes' }).click();
  await expect(page.getByText('Apartado en progreso')).toBeVisible();
  await captura(page, '32-apartado-en-progreso');
  await page.getByRole('button', { name: 'Cerrar Clientes' }).click();
  await expect(page.getByRole('tab')).toHaveCount(0);

  // Apariencia: modo oscuro y color, y se recuerda al recargar
  await page.getByRole('button', { name: /Apariencia/ }).click();
  await page.getByRole('radio', { name: /Oscuro/ }).click();
  await page.getByRole('radio', { name: /Violeta/ }).click();
  await captura(page, '11-apariencia');
  await expect(page.locator('html')).toHaveAttribute('data-modo', 'oscuro');
  await expect(page.locator('html')).toHaveAttribute('data-acento', 'violeta');
  await page.keyboard.press('Escape');

  // Roles y permisos: el administrador no puede perder permisos obligatorios
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('menuitem', { name: 'Roles y permisos' }).click();
  await expect(page.getByRole('heading', { name: 'Roles y permisos', level: 1 })).toBeVisible();
  await expect(page.getByRole('option', { name: /Operador/ })).toBeVisible();
  await page.getByRole('option', { name: /Administrador/ }).click();
  await expect(page.getByRole('checkbox', { name: /roles\.editar/ })).toBeDisabled();
  await page.getByRole('option', { name: /Operador/ }).click();
  await captura(page, '12-roles-permisos');

  // Historial y catálogos
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('menuitem', { name: 'Historial de actividades' }).click();
  await expect(page.getByRole('heading', { name: 'Historial de actividades' })).toBeVisible();
  await expect(page.getByRole('row').nth(1)).toBeVisible();
  await captura(page, '13-historial');
  await page.getByRole('button', { name: /Administración/ }).click();
  await page.getByRole('menuitem', { name: 'Ciudades y departamentos' }).click();
  await expect(page.getByText(/262 ciudades en 14 departamentos/)).toBeVisible();
  await page.getByRole('button', { name: /San Salvador/ }).first().click();
  await captura(page, '14-catalogos');

  // "+" abre un lanzador; el mismo módulo puede abrirse otra vez como pestaña independiente
  const antes = await page.getByRole('tab').count();
  await page.getByRole('button', { name: 'Nueva pestaña' }).click();
  await expect(page.getByRole('heading', { name: 'Nueva pestaña' })).toBeVisible();
  await page.locator('.lanzador').getByRole('button', { name: 'Historial de actividades' }).click();
  await expect(page.getByRole('tab', { name: /Historial de actividades \(2\)/ })).toBeVisible();
  await expect(page.getByRole('tab')).toHaveCount(antes + 1);

  // Recargar vuelve a crear la pestaña activa (descarta lo escrito en sus filtros)
  const activa = page.locator('.documento:not([hidden])');
  await activa.getByLabel('Buscar').fill('texto temporal');
  await page.getByRole('button', { name: 'Recargar la pestaña' }).click();
  await expect(activa.getByLabel('Buscar')).toHaveValue('');
});

test('categorías y filtros: árbol de ejemplo, alta de toda la jerarquía, baja suave y eliminación definitiva', async ({ page }) => {
  await entrar(page, 'admin', CLAVE_ADMIN);
  await expect(page.getByRole('heading', { name: /Bienvenido/ })).toBeVisible();
  await page.getByRole('button', { name: /Gestión/ }).click();
  await page.getByRole('menuitem', { name: 'Categorías y filtros' }).click();
  await expect(page.getByRole('heading', { name: 'Categorías y filtros' })).toBeVisible();

  // Datos de ejemplo: el árbol trae las categorías y, al elegir una subcategoría, sus filtros con opciones
  const arbol = page.getByRole('navigation', { name: 'Categorías y subcategorías' });
  await expect(arbol.getByRole('button', { name: /Tecnología/ })).toBeVisible();
  await arbol.getByRole('button', { name: /^Camisas/ }).click();
  await expect(page.getByRole('heading', { name: 'Camisas' })).toBeVisible();
  await expect(page.locator('.filtro-bloque').filter({ hasText: 'Clima' }).locator('.opcion-chip')).toHaveCount(4);
  await captura(page, '15-categorias-arbol');

  // Alta de la jerarquía completa
  const sufijo = Date.now().toString().slice(-6);
  const cat = `Mascotas ${sufijo}`;
  await page.getByRole('button', { name: '+ Categoría' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('Nombre es obligatorio.');
  await page.getByRole('alertdialog').getByLabel('Nombre').fill(cat);
  await page.getByRole('alertdialog').getByLabel('Descripción (opcional)').fill('Todo para tu mascota');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('.aviso').last()).toContainText('registrada');
  await arbol.getByRole('button', { name: new RegExp(cat) }).click();

  await page.getByRole('button', { name: '+ Subcategoría' }).click();
  await page.getByRole('alertdialog').getByLabel('Nombre').fill('Alimento');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('button', { name: /Alimento/ }).first().click();
  await expect(page.getByRole('heading', { name: 'Alimento' })).toBeVisible();

  await page.getByRole('button', { name: '+ Filtro' }).click();
  await page.getByRole('alertdialog').getByLabel('Nombre').fill('Tipo de mascota');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('button', { name: 'Agregar opción a Tipo de mascota' }).click();
  await page.getByRole('alertdialog').getByLabel('Valor').fill('Perro');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('.opcion-chip').filter({ hasText: 'Perro' })).toBeVisible();

  // Repetir la opción: el motivo se muestra dentro del editor, que sigue abierto
  await page.getByRole('button', { name: 'Agregar opción a Tipo de mascota' }).click();
  await page.getByRole('alertdialog').getByLabel('Valor').fill('perro');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('ya tiene esa opción');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Cancelar' }).click();
  await captura(page, '16-categorias-filtros');

  // Dar de baja una opción (chip ×), verla tachada y reactivarla
  await page.getByRole('button', { name: 'Dar de baja Perro' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await expect(page.locator('.aviso').last()).toContainText('dada de baja');
  await page.getByText('Mostrar inactivas').click();
  await expect(page.locator('.opcion-inactiva').filter({ hasText: 'Perro' })).toBeVisible();
  await page.getByRole('button', { name: 'Reactivar Perro' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Reactivar' }).click();
  await expect(page.locator('.opcion-chip:not(.opcion-inactiva)').filter({ hasText: 'Perro' })).toBeVisible();

  // Una categoría con subcategorías activas no se da de baja: la regla se muestra en pantalla
  await arbol.getByRole('button', { name: new RegExp(cat) }).click();
  await page.locator('.detalle-acciones').getByRole('button', { name: /Activo/ }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await expect(page.getByRole('alert')).toContainText('subcategorías activas');

  // Se baja de abajo hacia arriba y se elimina definitivamente en ese orden (cada nivel exige estar vacío)
  const bajaYElimina = async (nombre: RegExp, textoElimina?: string) => {
    await page.locator('.detalle-acciones').getByRole('button', { name: /Activo/ }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
    await page.locator('.detalle-acciones').getByRole('button', { name: 'Eliminar definitivamente' }).click();
    const d = page.getByRole('alertdialog');
    if (textoElimina) await d.getByRole('textbox').fill(textoElimina);
    await d.getByRole('button', { name: 'Eliminar definitivamente' }).click();
  };
  await arbol.getByRole('button', { name: /Alimento/ }).click();
  await page.getByRole('button', { name: 'Dar de baja Perro' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await page.getByRole('button', { name: 'Eliminar Perro' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Eliminar definitivamente' }).click();
  await expect(page.locator('.aviso').last()).toContainText('eliminada definitivamente');
  await page.locator('.filtro-bloque').filter({ hasText: 'Tipo de mascota' }).getByRole('button', { name: /Activo/ }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Dar de baja' }).click();
  await page.getByRole('button', { name: 'Eliminar', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Eliminar definitivamente' }).click();
  await expect(page.locator('.aviso').last()).toContainText('eliminado definitivamente');
  await bajaYElimina(/Alimento/, 'Alimento');
  await bajaYElimina(new RegExp(cat), cat);
  await expect(arbol.getByRole('button', { name: new RegExp(cat) })).toHaveCount(0);
});
