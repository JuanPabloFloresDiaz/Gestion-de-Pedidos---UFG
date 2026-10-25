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
  await page.goto('/');
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

  // Dar de baja al usuario nuevo
  await page.getByRole('row', { name: new RegExp(usuario) }).getByRole('button', { name: 'Dar de baja' }).click();
  await expect(page.locator('.aviso').last()).toContainText('dado de baja');

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
  await expect(page.getByRole('button', { name: /Administración/ })).toHaveCount(0);   // sin permiso usuarios.ver
  await expect(page.getByRole('button', { name: /Ayuda/ })).toBeVisible();
  await captura(page, '08-operador-menu-reducido');
});
