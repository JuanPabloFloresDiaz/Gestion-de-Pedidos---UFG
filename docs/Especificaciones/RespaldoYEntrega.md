# 📦 Respaldo, Restauración, Entrega y Defensa

> Cubre **RE-02 a RE-04**. Todo se ejecuta en Docker (el contenedor se llama `sqlserver` en el compose; la contraseña de `sa` viene de `.env`).

## 1. Respaldo FULL y Restauración (entregable obligatorio)

El enunciado pide un **respaldo FULL (o script) de la base y comprobar que se puede hacer el proceso de restauración**. Entregamos **ambas** cosas:

| Entregable | Contenido | Cómo se genera |
|---|---|---|
| `GestionPedidos_FULL.bak` | Esquema **y datos** (incluye los datos de demostración) | `scripts/db-backup.sh` |
| `GestionPedidos_esquema.sql` | Solo esquema (idempotente, desde las migraciones) | `scripts/ef-docker.sh migrations script --idempotent` |
| `RestauracionVerificada.txt` | Evidencia de la restauración (salida de `db-restore.sh`) | `scripts/db-restore.sh` |

### 1.1 Respaldo (`scripts/db-backup.sh`)

```sql
BACKUP DATABASE [GestionPedidos]
TO DISK = N'/var/opt/mssql/backup/GestionPedidos_FULL.bak'
WITH INIT, FORMAT, CHECKSUM, STATS = 10;
```

El script crea la carpeta de respaldo dentro del contenedor, ejecuta la sentencia con `sqlcmd` (la contraseña se lee de la variable del contenedor, no se escribe en la línea de comandos) y copia el `.bak` a `dist/respaldo/` con `docker compose cp`.

### 1.2 Restauración y verificación (`scripts/db-restore.sh`)

1. Copiar el `.bak` al contenedor (`docker compose cp`).
2. `RESTORE VERIFYONLY FROM DISK = N'…' WITH CHECKSUM;` (integridad del archivo).
3. `RESTORE FILELISTONLY …` para obtener los **nombres lógicos** de los archivos (normalmente `GestionPedidos` y `GestionPedidos_log`).
4. Restaurar en una base **nueva** para no pisar la original:

```sql
RESTORE DATABASE [GestionPedidos_Restaurada]
FROM DISK = N'/var/opt/mssql/backup/GestionPedidos_FULL.bak'
WITH MOVE N'GestionPedidos'     TO N'/var/opt/mssql/data/GestionPedidos_Restaurada.mdf',
     MOVE N'GestionPedidos_log' TO N'/var/opt/mssql/data/GestionPedidos_Restaurada_log.ldf',
     STATS = 10;
```

5. **Comparar conteos de filas** de cada tabla entre `GestionPedidos` y `GestionPedidos_Restaurada` y escribir el resultado en `RestauracionVerificada.txt`. Si algún conteo difiere, el script termina con error.
6. (Opcional) apuntar la API a la base restaurada y hacer login para demostrarlo en la defensa.

> Hacer esta prueba **en una máquina limpia** (volumen nuevo) al menos una vez antes de entregar.

## 2. Contenido del Comprimido (carpeta única → U-virtual)

> **Se sube a la U-virtual cuando terminen todas las exposiciones (diciembre de 2026)**. La versión defendida el 12/11 se marca con la etiqueta `v1.0-defensa` en git para poder reconstruirla tal cual.

```
GestionPedidos_UFG_<fecha>.zip
├── 00_Enunciado_Respondido.pdf        # el documento del ingeniero en PDF, con nombres completos y carnets
├── 01_Codigo/                          # fuentes (sin bin/, obj/, .env ni secretos)
├── 02_Ejecutable/                      # .exe de escritorio (MAUI Blazor Hybrid) publicado + appsettings.json de ejemplo
├── 03_BaseDeDatos/
│   ├── GestionPedidos_FULL.bak
│   ├── GestionPedidos_esquema.sql
│   └── RestauracionVerificada.txt
├── 04_Documentacion/                   # carpeta docs/ (análisis, arquitectura, specs)
└── LEEME.txt                           # cómo levantar todo con Docker y cómo ejecutar el .exe, usuarios de prueba
```

Lista de verificación antes de subirlo:
- [ ] Nombres completos y carnets correctos en el PDF y en la ventana *Integrantes*.
- [ ] `.env` y secretos **no** incluidos; `LEEME.txt` explica cómo crear el `.env` desde `.env.example`.
- [ ] El `.bak` se restaura (§1) y la aplicación funciona contra esa base.
- [ ] Usuarios de prueba documentados (administrador y operador) **solo con credenciales de demostración**.
- [ ] El zip se abre y se ejecuta siguiendo solo el `LEEME.txt`.

## 3. Datos de Demostración

Seeder (solo `Development` o con bandera explícita): ~30 clientes de varios departamentos, ~40 productos (algunos con stock bajo), ~60 pedidos en los tres estados, un administrador y un operador. **Todo ficticio** y coherente (precios, fechas de entrega).

## 4. Demostración y Defensa

La defensa es **ponderada y todos defienden**. Cada integrante debe poder explicar **cualquier** parte, no solo la suya.

### 4.1 Guion de la demostración (≈ 10 min)
1. Iniciar sesión como **administrador** en el escritorio: mostrar menús completos y la interfaz de **pestañas de documentos** (varios módulos abiertos, cambiar entre ellos).
2. **Cliente**: registrar uno (ver la **notificación** por evento), cambiar departamento (lista dependiente de ciudades), editar, desactivar.
3. **Producto**: registrar y editar; ver el indicador de stock bajo.
4. **Pedido**: crear con 2 productos (total en vivo), intentar exceder el stock (evento `StockInsuficiente`), marcar entregado, cancelar otro (el stock se repone).
5. **Búsquedas**: por apellido de cliente, por producto, por rango de precio.
6. **Historial**: mostrar las acciones recién hechas.
7. **Tienda web**: registrar un cliente, hacer un pedido desde el navegador y ver en el escritorio que el pedido aparece y el stock bajó.
8. Cerrar sesión e iniciar como **operador**: menús distintos y acción denegada.
9. Ayuda → **Integrantes**.
10. Mostrar código: un evento completo (delegado, `EventArgs`, publicadora, suscripción con `+=`), una consulta parametrizada, el procedimiento almacenado, la cadena de conexión segura, los comentarios XML.
11. Mostrar el **respaldo** y la **restauración** verificada.

### 4.2 Banco de preguntas probables
* ¿Qué es un delegado? ¿Y un evento? ¿En qué se diferencian? ¿Por qué `?.Invoke`? ¿Qué pasa si no hay suscriptores?
* Muéstrame dónde se declara, se dispara y se suscribe un evento en tu módulo.
* ¿Qué es una interfaz de múltiples documentos? ¿Cómo evitaron abrir dos veces el mismo módulo?
* ¿Cómo evitan la inyección SQL? ¿Dónde se usa el procedimiento almacenado?
* ¿Cómo se conecta la aplicación a la base de datos? ¿Por qué el `.exe` no tiene la cadena de conexión?
* ¿Cómo se guardan las contraseñas? ¿Qué diferencia hay entre autenticación y autorización?
* ¿Qué pasa si dos operadores piden la última unidad al mismo tiempo?
* ¿Cómo restauran la base de datos? ¿Cómo verificaron que el respaldo sirve?
* ¿Por qué baja lógica y no `DELETE`?
* ¿Qué hace `async/await` aquí y por qué no se congela la ventana?
* ¿Qué es Blazor Hybrid? ¿Cómo comparten pantallas el escritorio y la web?
* ¿Qué diferencia hay entre un evento de Blazor (`@onclick`), un `EventCallback` y un evento propio con delegado?

### 4.3 Preparación
* Ensayo completo con cronómetro y **reparto de turnos** (cada integrante conduce una parte y responde sobre otra).
* Defensa **local** (API, web y BD en Docker en la máquina Windows, sin internet) ensayada de punta a punta ([Infraestructura.md §5](../Infraestructura.md)).
* Equipo de defensa con Docker Desktop iniciado **antes**, imágenes ya descargadas y el `.bak` a mano.
* **Defensa: jueves 12 de noviembre de 2026** (fecha asignada por el ingeniero). Paquete de entrega listo el 10/11 y congelamiento de funcionalidades el 6/11 ([Implementacion.md §3](../Implementacion.md)).
