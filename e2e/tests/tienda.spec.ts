import { test, expect, Page } from '@playwright/test';

const captura = (page: Page, nombre: string) => page.screenshot({ path: `capturas/${nombre}.png`, fullPage: false });

test('portada de la tienda: promoción, categorías y recién llegados', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Encuentra lo que buscas');
  await expect(page.getByRole('link', { name: /Tecnología/ }).first()).toBeVisible();
  await expect(page.locator('.tarjeta-producto').first()).toBeVisible();
  // Las fotos vienen del almacén de imágenes (Garage) a través del sitio y se cargan de verdad
  const foto = page.locator('.tarjeta-producto img').first();
  await expect(foto).toBeVisible();
  await expect.poll(() => foto.evaluate((i: HTMLImageElement) => i.complete && i.naturalWidth > 0 && getComputedStyle(i).opacity === '1')).toBe(true);   // cargada y ya sin el desvanecido
  await expect(page.locator('.categoria-imagen img').first()).toBeVisible();
  // Sin emojis: los íconos son SVG
  expect(await page.locator('body').innerText()).not.toMatch(/\p{Extended_Pictographic}/u);
  await expect(page.locator('svg.icono').first()).toBeVisible();
  await captura(page, '20-tienda-portada');
});

test('catálogo: filtros por categoría, subcategoría, opciones, precio y orden; los filtros viven en la dirección', async ({ page }) => {
  await page.goto('/tienda');
  await expect(page.locator('.tarjeta-producto').first()).toBeVisible();
  await page.waitForTimeout(700);   // deja terminar el desvanecido de las fotos para la captura
  await captura(page, '21-tienda-catalogo');

  // Categoría → subcategoría → opciones (Y entre filtros, O dentro de un filtro)
  const lateral = page.getByLabel('Categorías y filtros');
  await lateral.getByRole('link', { name: /^Ropa/ }).click();
  await lateral.getByRole('link', { name: /^Camisas/ }).click();
  await expect(page).toHaveURL(/sub=\d+/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Camisas');
  await expect(page.locator('.tarjeta-producto')).toHaveCount(3);

  await lateral.locator('details', { hasText: 'Material' }).getByText('Lino').click();
  await expect(page).toHaveURL(/op=\d+/);
  await expect(page.locator('.tarjeta-producto')).toHaveCount(1);
  await expect(page.getByText('Camisa de lino manga corta')).toBeVisible();
  await expect(page.getByRole('button', { name: /Quitar filtro Material: Lino/ })).toBeVisible();
  await captura(page, '22-tienda-filtros');

  // Quitar el chip vuelve a mostrar las tres; el filtro de precio se aplica y se puede quitar
  await page.getByRole('button', { name: /Quitar filtro Material: Lino/ }).click();
  await expect(page.locator('.tarjeta-producto')).toHaveCount(3);
  await lateral.getByLabel('Precio mínimo').fill('17');
  await lateral.getByRole('button', { name: 'Aplicar' }).click();
  await expect(page.locator('.tarjeta-producto')).toHaveCount(2);
  await page.getByLabel('Ordenar por').selectOption('precio-desc');
  await expect(page.locator('.tarjeta-producto h3').first()).toHaveText('Camisa Oxford manga larga');

  // Recargar conserva los filtros (están en la dirección)
  await page.reload();
  await expect(page.locator('.tarjeta-producto')).toHaveCount(2);
  await expect(page.getByLabel('Ordenar por')).toHaveValue('precio-desc');
});

test('búsqueda, sin resultados y paginación', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Buscar productos').fill('licuadora');
  await page.getByLabel('Buscar productos').press('Enter');
  await expect(page).toHaveURL(/q=licuadora/);
  await expect(page.locator('.tarjeta-producto h3')).toContainText(['Licuadora de 600 W']);

  await page.goto('/tienda?q=zzzzzz');
  await expect(page.getByRole('heading', { name: 'No encontramos productos' })).toBeVisible();

  await page.goto('/tienda');
  await expect(page.locator('.tarjeta-producto')).toHaveCount(12);
  await page.getByRole('button', { name: 'Página siguiente' }).click();
  await expect(page).toHaveURL(/pag=2/);
  await expect(page.locator('.tarjeta-producto').first()).toBeVisible();
});

test('producto, carrito y registro de un cliente', async ({ page }) => {
  await page.goto('/tienda?q=Oxford');
  await page.getByRole('link', { name: /Ver Camisa Oxford/ }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Camisa Oxford manga larga');
  await expect(page.getByText('Características')).toBeVisible();
  await expect(page.locator('.caracteristicas')).toContainText('Marca');
  await captura(page, '23-tienda-producto');

  // Cantidad con tope por pedido y agregar al carrito (el contador del encabezado se actualiza)
  await page.getByRole('button', { name: 'Una más' }).click();
  await page.getByRole('button', { name: 'Agregar al carrito' }).first().click();
  await expect(page.getByLabel(/Carrito, 2 productos/)).toBeVisible();

  await page.getByRole('link', { name: /Carrito/ }).click();
  await expect(page.getByRole('heading', { name: 'Tu carrito' })).toBeVisible();
  await expect(page.locator('.carrito-linea')).toHaveCount(1);
  await expect(page.locator('.carrito-resumen')).toContainText('$49.98');
  await captura(page, '24-tienda-carrito');

  // El carrito sobrevive a recargar la página
  await page.reload();
  await expect(page.locator('.carrito-linea')).toHaveCount(1);

  // Sin sesión: pide ingresar y vuelve; el registro valida y crea la cuenta
  await page.getByRole('button', { name: /Ingresar para hacer el pedido/ }).click();
  await expect(page).toHaveURL(/ingresar\?retorno=%2Fpedido/);
  await page.getByRole('link', { name: 'Créala aquí' }).click();
  await expect(page.getByRole('heading', { name: 'Crea tu cuenta' })).toBeVisible();
  await page.getByRole('button', { name: 'Crear cuenta' }).last().click();
  await expect(page.getByText('Los nombres es obligatorio.')).toBeVisible();
  await captura(page, '25-tienda-registro');

  const n = Date.now().toString().slice(-8);
  await page.getByLabel('Nombres').fill('Valeria');
  await page.getByLabel('Apellidos').fill('Quintanilla');
  await page.getByLabel('Correo electrónico').fill(`valeria${n}@ejemplo.com`);
  await page.getByLabel('Teléfono', { exact: true }).fill('78945612');
  await page.getByLabel('Departamento').selectOption({ label: 'San Salvador' });
  await page.getByLabel('Distrito').selectOption({ index: 1 });
  await page.getByLabel('Dirección de entrega').fill('Colonia Escalón, pasaje 3, casa 12');
  await page.getByLabel('Contraseña (mínimo 10 caracteres)').fill('ClaveCliente_2026');
  await page.getByLabel('Repite la contraseña').fill('ClaveCliente_2026');
  await page.getByRole('button', { name: 'Crear cuenta' }).last().click();
  await expect(page.getByRole('button', { name: /Valeria/ })).toBeVisible();   // sesión iniciada: el encabezado saluda

  // Hacer el pedido: datos de entrega de la cuenta, fecha, comentarios y confirmación
  await page.getByRole('link', { name: /Carrito/ }).click();
  await page.getByRole('button', { name: 'Hacer pedido' }).click();
  await expect(page.getByRole('heading', { name: 'Confirma tu pedido' })).toBeVisible();
  await expect(page.getByText('Colonia Escalón, pasaje 3, casa 12')).toBeVisible();
  await page.getByLabel('Comentarios (opcional)').fill('Entregar por la tarde');
  await captura(page, '28-tienda-pedido');
  await page.getByRole('button', { name: 'Confirmar pedido' }).click();

  await expect(page).toHaveURL(/mis-pedidos\?nuevo=\d+/);
  await expect(page.getByText(/Registramos tu pedido/)).toBeVisible();
  const tarjeta = page.locator('.pedido-tarjeta').first();
  await expect(tarjeta).toContainText('2 × Camisa Oxford manga larga');
  await expect(tarjeta).toContainText('$49.98');
  await expect(tarjeta.locator('.estado-pedido')).toHaveText('Pendiente');
  await captura(page, '29-tienda-mis-pedidos');
  await expect(page.getByLabel('Carrito, 0 productos')).toBeVisible();   // el carrito quedó vacío

  // Cancelar el pedido pendiente (con confirmación)
  await tarjeta.getByRole('button', { name: 'Cancelar pedido' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Sí, cancelar el pedido' }).click();
  await expect(tarjeta.locator('.estado-pedido')).toHaveText('Cancelado');
  await expect(tarjeta.getByRole('button', { name: 'Cancelar pedido' })).toHaveCount(0);
});

test('modo oscuro y color de acento también cambian la tienda', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: /Apariencia/ }).click();
  await page.getByRole('radio', { name: /Oscuro/ }).click();
  await page.getByRole('radio', { name: /Rosa/ }).click();
  await page.keyboard.press('Escape');
  await expect(page.locator('html')).toHaveAttribute('data-modo', 'oscuro');
  await expect(page.locator('.tarjeta-producto').first()).toBeVisible();
  await captura(page, '26-tienda-oscuro');
  await page.goto('/tienda?categoria=1');   // la preferencia se recuerda al cambiar de página
  await expect(page.locator('.tarjeta-producto').first()).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-acento', 'rosa');
  await page.waitForTimeout(700);
  await captura(page, '27-tienda-oscuro-catalogo');
});

test('laptops y celulares con marcas reales: volumen, paginación y filtro por marca (O dentro de la marca, Y con otros filtros)', async ({ page }) => {
  await page.goto('/tienda');
  const lateral = page.getByLabel('Categorías y filtros');
  await lateral.getByRole('link', { name: /^Tecnología/ }).click();
  await lateral.getByRole('link', { name: /^Laptops/ }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Laptops');
  await expect(page.getByText('35 productos')).toBeVisible();
  await expect(page.locator('.tarjeta-producto')).toHaveCount(12);   // 35 productos = 3 páginas
  await page.getByRole('button', { name: 'Página siguiente' }).click();
  await expect(page).toHaveURL(/pag=2/);
  await page.getByRole('button', { name: 'Última página' }).click();
  await expect(page.locator('.tarjeta-producto')).toHaveCount(11);

  // Marca: HP (6) y luego HP O ASUS (12); con el filtro de pantalla 16" se cruzan (Y)
  const marca = lateral.locator('details', { hasText: 'Marca' });
  await marca.getByText('HP', { exact: true }).click();
  await expect(page.getByText('6 productos')).toBeVisible();
  await expect(page.locator('.tarjeta-producto h3').first()).toContainText('HP');
  await marca.getByText('ASUS', { exact: true }).click();
  await expect(page.getByText('12 productos')).toBeVisible();
  await lateral.locator('details', { hasText: 'Pantalla' }).getByText('16"', { exact: true }).click();
  await expect(page.getByText('2 productos')).toBeVisible();   // HP Victus 16 y ASUS ROG Strix G16
  expect((await page.locator('.tarjeta-producto h3').allInnerTexts()).sort()).toEqual(['ASUS ROG Strix G16', 'HP Victus 16']);   // sin depender del orden
  const foto = page.locator('.tarjeta-producto img').first();
  await expect.poll(() => foto.evaluate((i: HTMLImageElement) => i.complete && i.naturalWidth > 0)).toBe(true);   // cada uno con su foto del almacén
  await captura(page, '30-tienda-laptops-marca');

  // Celulares: 25 en total, con Samsung y Motorola entre sus marcas
  await page.goto('/tienda');
  await lateral.getByRole('link', { name: /^Tecnología/ }).click();
  await lateral.getByRole('link', { name: /^Celulares/ }).click();
  await expect(page.getByText('25 productos')).toBeVisible();
  const marcaTel = lateral.locator('details', { hasText: 'Marca' });
  for (const m of ['Samsung', 'Motorola', 'Xiaomi', 'Apple']) await expect(marcaTel.getByText(m, { exact: true })).toBeVisible();
});
