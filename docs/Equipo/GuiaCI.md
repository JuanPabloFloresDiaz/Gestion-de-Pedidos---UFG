# 🔁 Mini Guía: Integración Continua (CI) con GitHub Actions

> **Para pasarle al agente (Antigravity) o seguir a mano en la Fase 0.** Objetivo: que cada Pull Request compile, pase pruebas y se revise automáticamente antes de entrar a `main`. No requiere secretos.

## 1. Qué Hay que Hacer (resumen)

| # | Paso | Quién |
|---|---|---|
| 1 | Crear `.github/workflows/ci.yml` con los 4 trabajos de §3 | Agente |
| 2 | Crear `.github/dependabot.yml` (§4) | Agente |
| 3 | Crear `GestionPedidos.Linux.slnf` (solución sin `Desktop`) y los tres trabajos del CI, incluido el de Windows para MAUI (§3) | Agente |
| 4 | Comprobar que el CI pasa en una rama de prueba | Quien tenga el repo |
| 5 | Proteger la rama `main` en GitHub (§5) | **Pablo** (dueño del repositorio, interfaz web) |

## 2. Requisitos Previos del Repositorio

* `global.json` fijando el SDK de **.NET 8** (versión máxima permitida por el ingeniero).
* `Directory.Build.props` con, entre otras propiedades:
  ```xml
  <Project>
    <PropertyGroup>
      <Nullable>enable</Nullable>
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      <GenerateDocumentationFile>true</GenerateDocumentationFile>
    </PropertyGroup>
  </Project>
  ```
* **Dos soluciones**: `GestionPedidos.sln` (todo) y un **filtro de solución** `GestionPedidos.Linux.slnf` que excluye `GestionPedidos.Desktop`, porque **.NET MAUI para Windows no compila en Linux**. Ubuntu usa el filtro; un *runner* de Windows compila `Desktop`.
* `.env.example` con todas las variables (el CI lo copia a `.env`).
* `docker-compose.yml` con *healthchecks* en `sqlserver` y `api`.

## 3. Flujo de Trabajo (`.github/workflows/ci.yml`)

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build-test:
    name: Compilar y probar
    runs-on: ubuntu-latest
    timeout-minutes: 25
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Restaurar
        run: dotnet restore GestionPedidos.Linux.slnf
      - name: Formato
        run: dotnet format GestionPedidos.Linux.slnf --verify-no-changes --no-restore
      - name: Compilar (advertencias como errores; sin Desktop)
        run: dotnet build GestionPedidos.Linux.slnf --no-restore -c Release
      - name: Pruebas (unitarias, integración con Testcontainers, arquitectura)
        run: dotnet test GestionPedidos.Linux.slnf --no-build -c Release --logger "trx" --results-directory TestResults
      - name: Paquetes vulnerables
        run: |
          dotnet list package --vulnerable --include-transitive 2>&1 | tee vuln.txt
          ! grep -q "has the following vulnerable packages" vuln.txt
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: resultados-pruebas
          path: TestResults

  desktop:
    name: Compilar escritorio (MAUI, Windows)
    runs-on: windows-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Carga de trabajo MAUI
        run: dotnet workload install maui
      - name: Compilar Desktop (target Windows)
        run: dotnet build src/GestionPedidos.Desktop -f net8.0-windows10.0.19041.0 -c Release

  secretos:
    name: Escaneo de secretos
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Gitleaks
        run: |
          docker run --rm -v "$PWD:/repo" zricethezav/gitleaks:latest \
            detect --source /repo --redact --verbose

  docker:
    name: Levantar con Docker Compose
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - name: Preparar variables de ejemplo
        run: cp .env.example .env
      - name: Construir y esperar a que esté sano
        run: docker compose up -d --build --wait --wait-timeout 300
      - name: Comprobar la API
        run: curl --fail --silent http://localhost:4390/health/ready
      - name: Logs si falla
        if: failure()
        run: docker compose logs --no-color
      - name: Apagar
        if: always()
        run: docker compose down -v
```

Notas para el agente:
* Los comandos son **los mismos** que usan los scripts `scripts/*-docker.sh`; el CI no necesita contenedor extra porque el *runner* ya es efímero.
* Testcontainers funciona en `ubuntu-latest` (Docker preinstalado).
* El escaneo de secretos usa la imagen oficial de Gitleaks por Docker, así no depende de licencias de acciones de terceros.
* Si una acción cambia de versión mayor, actualizarla con el PR de Dependabot, no a mano.
* No añadir *secrets* al flujo: las pruebas generan sus propias contraseñas.

## 4. Dependabot (`.github/dependabot.yml`)

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
  - package-ecosystem: github-actions
    directory: /
    schedule: { interval: monthly }
  - package-ecosystem: docker
    directory: /
    schedule: { interval: monthly }
```

## 5. Proteger `main` (en la web de GitHub, lo hace Pablo como dueño del repo)

`Settings → Branches → Add branch ruleset` (o *Branch protection rule*) para `main`:

- [x] **Require a pull request before merging** con **1 aprobación**.
- [x] **Require status checks to pass**: `Compilar y probar`, `Compilar escritorio (MAUI, Windows)`, `Escaneo de secretos`, `Levantar con Docker Compose` (aparecen tras la primera ejecución).
- [x] **Require branches to be up to date before merging**.
- [x] **Block force pushes** y **Restrict deletions**.
- [x] Activar **Dependabot alerts** (`Settings → Code security`).

> Si el repositorio es de una organización, el dueño debe dar permisos de escritura a los demás integrantes (*Collaborators*), y las Actions deben estar habilitadas.

## 6. Comandos Locales Equivalentes (por si el CI falla)

Todo con Docker, sin instalar nada (scripts de la Fase 0):

```bash
./scripts/dotnet-docker.sh format GestionPedidos.Linux.slnf --verify-no-changes
./scripts/dotnet-docker.sh build GestionPedidos.Linux.slnf -c Release
./scripts/test-docker.sh
docker compose up -d --build --wait && curl -f http://localhost:4390/health/ready
```

## 7. Puntos a Validar en la Fase 0

1. El repositorio está en la cuenta de Pablo (`JuanPabloFloresDiaz`), que es quien administra la protección de `main` (§5); los demás integrantes necesitan ser **colaboradores con permiso de escritura**.
2. **Actions** debe estar habilitado en el repositorio.
3. El trabajo `desktop` en `windows-latest` consume más minutos de Actions que los de Ubuntu; si el plan gratuito se queda corto, se ejecuta solo en los Pull Requests hacia `main`. Si la compilación de MAUI falla en el *runner*, se ajusta la carga de trabajo o la versión del SDK en la primera ejecución.
