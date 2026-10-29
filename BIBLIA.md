# 📖 BIBLIA DEL PROYECTO · Partículas RA
### Simulador de partículas subatómicas en realidad aumentada (APK para Android)

Este documento reúne todo lo necesario para montar el proyecto, compilarlo y entenderlo.

> **Atajo: compilar en la nube con Forja3D.** No hace falta Unity en tu PC. El proyecto incluye `Packages/manifest.json` con AR Foundation, ARCore e Input System, y `Assets/Editor/Forja3DPreparar.cs`, que antes de compilar activa el Input System y ARCore, configura el Player y genera la escena. Con la plantilla `basica` de Forja3D basta con pedir `compilar_apk` en el proyecto *particulas-ra*. Los pasos manuales de la sección 2 solo hacen falta si compilas en tu computadora (ahí también sirve el menú **Partículas RA › 0. Preparar todo**). El paquete trae el código completo: 16 scripts de juego y 2 de Editor. La escena, los materiales y los prefabs no vienen incluidos: un menú del Editor los genera con un clic.

| Variable | Valor por defecto | Dónde se cambia |
|---|---|---|
| NOMBRE_APP | Partículas RA | `Assets/Editor/SceneSetup.cs` → `AppName` |
| PACKAGE_NAME | com.ejemplo.particulasra | `SceneSetup.cs` → `PackageName` |
| VERSION_UNITY | Unity 6 LTS (6000.0.x) · válido también 2022.3 LTS | Unity Hub |
| TAMANO_CAMARA | 0,5 m de lado | `SceneSetup.cs` → `ChamberSize` (y el campo `chamberSize` de ARPlacementController) |
| VERSION_APP | 1.0.0 (versionCode 1) | `SceneSetup.cs` → `Version`, `VersionCode` |

---

## Índice
1. Requisitos
2. Montaje paso a paso: de cero al APK
3. Cómo se usa la app
4. Arquitectura
5. Física
6. Renderizado y rendimiento
7. Interfaz
8. Decisiones de diseño
9. Solución de problemas
10. Criterios de aceptación
11. Cómo ampliar el proyecto

---

## 1. Requisitos

- **Computadora** con Windows 10/11 o macOS. Con unos 15 GB libres.
- **Unity Hub** con **Unity 6 LTS** (o 2022.3 LTS). Al instalar el Editor, marca estos módulos:
  - **Android Build Support**
  - **OpenJDK**
  - **Android SDK & NDK Tools**
- **Un móvil Android compatible con ARCore.** Consulta la lista oficial: https://developers.google.com/ar/devices
  - En el móvil deben estar los *Servicios de Google Play para RA*. Si faltan, la app los pide al abrirse.
- **Cable USB** con la depuración USB activada, si vas a instalar con `adb`. También puedes copiar el APK al móvil.

---

## 2. Montaje paso a paso: de cero al APK

### 2.1 Crea el proyecto
1. Abre Unity Hub y elige **New project**. Selecciona la plantilla **3D (Built-in)** o **Universal 3D** (URP) y ponle de nombre `ParticulasRA`.
   - Los materiales usan un shader propio (`Assets/Shaders/PR_Unlit.shader`) que funciona en los dos pipelines.
   - También sirve la plantilla **AR Mobile**.

### 2.2 Instala los paquetes
En **Window › Package Manager › Unity Registry**:

| Paquete | Unity 6 | Unity 2022.3 |
|---|---|---|
| AR Foundation | 6.x | 5.1.x |
| Google ARCore XR Plugin | 6.x (misma versión que AR Foundation) | 5.1.x |
| Input System | 1.8 o superior | 1.7 o superior |
| XR Plugin Management | se instala solo | se instala solo |
| uGUI (Unity UI) | ya incluido | ya incluido |

Cuando Unity pregunte si quieres activar el nuevo Input System, responde **Yes**. El Editor se reiniciará.

### 2.3 Copia el código
Copia las carpetas del paquete dentro de la carpeta `Assets/` del proyecto:
```
Assets/Scripts/   (16 archivos .cs)
Assets/Editor/    (SceneSetup.cs, BuildScript.cs)
```
Espera a que Unity compile. La consola no debe mostrar errores rojos. Arriba aparecerá un menú nuevo: **Partículas RA**.

### 2.4 Configura el Player
1. **File › Build Settings › Android › Switch Platform.**
2. Menú **Partículas RA › 1. Configurar Player Settings (Android)**. Este paso configura:
   - nombre, package name y versión
   - IL2CPP, solo ARM64, minSdk 24
   - solo OpenGLES3 (sin Vulkan) y orientación vertical
3. **Edit › Project Settings › Player › Other Settings › Active Input Handling**: elige *Input System Package (New)* o *Both*.

### 2.5 Activa ARCore (manual, imprescindible)
1. **Project Settings › XR Plug-in Management** → pestaña Android → marca **Google ARCore**.
2. **XR Plug-in Management › Project Validation** → **Fix All**. Repite hasta que no quede ningún aviso.
3. **Solo si usas URP**: comprueba que el renderer tiene la **AR Background Renderer Feature**:
   - Abre `Assets/Settings/` y selecciona el *Universal Renderer Data* que usa Android (por ejemplo `URP-Performant-Renderer` o `Mobile_Renderer`).
   - Pulsa **Add Renderer Feature › AR Background Renderer Feature**, si no está ya.
   - ⚠️ Sin este paso, la cámara del móvil se ve **negra**.
4. En **ARCore** (dentro de XR Plug-in Management), deja *Requirement* en **Required**.

### 2.6 Genera la escena
Menú **Partículas RA › 2. Crear escena**. Hace lo siguiente:
- Crea los materiales en `Assets/Materials/`: PR_Core, PR_Glow, PR_Plane, PR_Trail y PR_TrailDash.
- Crea el prefab `Assets/Prefabs/PlanoRA.prefab`.
- Monta la jerarquía completa y conecta todas las referencias (ver 4.3).
- Guarda `Assets/Scenes/Main.unity` y la añade a Build Settings.

### 2.7 Prueba en el Editor (opcional)
Pulsa **Play**. En el Editor no hay RA, así que la app entra en modo de prueba:
- un clic coloca la cámara de burbujas 1,2 m delante;
- arrastrar con el ratón lanza partículas;
- la rueda del ratón cambia la escala.

Sirve para revisar la física y la interfaz. Si quieres simular RA completa en el PC, activa **XR Simulation** en XR Plug-in Management, en la pestaña de PC.

### 2.8 Compila el APK
- **Opción A**: menú **Partículas RA › 3. Compilar APK**. El resultado queda en `Builds/ParticulasRA.apk`.
- **Opción B**: **File › Build Settings › Build**.
- **Opción C** (línea de comandos):
  - **Windows**
    ```
    "C:\Program Files\Unity\Hub\Editor\6000.0.XXf1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\ruta\ParticulasRA" -buildTarget Android -executeMethod BuildScript.BuildAndroid -logFile -
    ```
  - **macOS**
    ```
    /Applications/Unity/Hub/Editor/6000.0.XXf1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/ParticulasRA -buildTarget Android -executeMethod BuildScript.BuildAndroid -logFile -
    ```
  - Si termina con código de salida 0, la compilación salió bien.

### 2.9 Firma
- **Para pruebas**: no hace falta hacer nada. Unity firma el APK con una clave de depuración.
- **Para publicar o distribuir**:
  1. Ve a **Player › Publishing Settings › Keystore Manager › Create New**.
  2. Guarda el archivo `.keystore` y sus contraseñas en un lugar seguro. Si los pierdes, no podrás actualizar la app.
  3. Marca **Custom Keystore** y vuelve a compilar.

### 2.10 Instala en el móvil
- **Con cable**, si tienes `adb` (viene en el SDK de Unity: `.../Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools`):
  ```
  adb install -r Builds/ParticulasRA.apk
  ```
- **Sin cable**: copia el APK al móvil, ábrelo y permite *instalar apps de origen desconocido*.

---

## 3. Cómo se usa la app

1. Al abrirla por primera vez aparece un **tutorial** de 3 tarjetas, que se puede saltar.
2. **Apunta al suelo** y mueve el móvil despacio. Cuando aparezca el **anillo azul**, toca la pantalla.
3. La **cámara de burbujas** (un cubo de 50 cm) queda anclada al suelo y arranca sola una **lluvia de rayos cósmicos**.
4. **Lanzar partículas**:
   - Elige una partícula en el panel inferior y **arrastra** el dedo.
   - La dirección del arrastre marca hacia dónde sale y la longitud fija la velocidad. En el fotón, fija la energía en MeV.
   - Un toque corto la lanza hacia delante.
5. **Gestos**:
   - **pellizco**: escala, de 0,2× a 4×
   - **giro con dos dedos**: rota el cubo
   - **Reubicar**: vuelve a colocar el cubo en otro sitio
6. **Experimentos**:
   - **Rayos cósmicos**
   - **p̄ + p** (aniquilación)
   - **γ → e⁻e⁺** (producción de pares)
   - **e⁻ + e⁺** (con el campo apagado)
7. **Ajustes de la cámara**:
   - campo B (de −12 a +12) y su dirección (X, Y, Z)
   - velocidad, longitud de trazas y densidad del medio
   - Coulomb y rebote en los bordes
8. **Camina alrededor** del cubo para ver las hélices en 3D.

---

## 4. Arquitectura

### 4.1 Árbol de archivos
```
Assets/
├─ Scripts/
│  ├─ ParticleDefs.cs          Tabla de 10 partículas, PType, PInfo, formato de números y fichas
│  ├─ SimulationEngine.cs      Estado SoA, subpasos, Boris, Coulomb, medio, límites, cola programada
│  ├─ ReactionSystem.cs        Aniquilaciones, producción de pares, desintegraciones
│  ├─ ExperimentPresets.cs     Los 4 experimentos
│  ├─ TrailManager.cs          Pool de 560 LineRenderer, buffer circular y trazas fantasma
│  ├─ ParticleRenderer.cs      Núcleos y halos con GPU instancing (2 llamadas por tipo)
│  ├─ FieldVisualizer.cs       64 flechas instanciadas que muestran B
│  ├─ FlashEffects.cs          Destellos de 0,6 s en cada reacción
│  ├─ ChamberVisual.cs         Cubo translúcido con 12 aristas luminosas
│  ├─ ProceduralAssets.cs      Mallas y texturas generadas por código
│  ├─ ARPlacementController.cs Planos, indicador, anclaje, Reubicar, pellizco y giro, permisos
│  ├─ LaunchInput.cs           Slab test, arrastre, previsualización y lanzamiento
│  ├─ PointerInput.cs          Toque (EnhancedTouch) más ratón para el Editor
│  ├─ UIFactory.cs             Constructores de uGUI y paleta de colores
│  ├─ UIController.cs          Toda la interfaz en español
│  └─ TutorialController.cs    Tutorial de primera vez
├─ Shaders/
│  └─ PR_Unlit.shader          Shader sin luces con instancing (integrado y URP)
└─ Editor/
   ├─ Forja3DPreparar.cs       Menú 0 y gancho de Forja3D: Input System, ARCore, Player y escena
   ├─ SceneSetup.cs            Menús 1 y 2: Player Settings y escena completa
   └─ BuildScript.cs           Menú 3 y batchmode: genera Builds/ParticulasRA.apk
Packages/manifest.json         AR Foundation 6.0, ARCore 6.0, Input System 1.11, uGUI
```
Todo el código de juego está en el namespace `ParticulasRA`.

### 4.2 Flujo de un frame
1. **ARPlacementController.Update**: comprueba el permiso y el estado de ARCore. Después gestiona la colocación o los gestos.
2. **UIController.Update**: actualiza el área segura, el registro y el contador.
3. **SimulationEngine.Update**: divide `simDt = deltaTime · velocidad` (máximo 0,05 s) en `n = ceil(simDt / (1/120))` subpasos. En cada subpaso:
   1. Calcula Coulomb y detecta reacciones en un único bucle O(n²).
   2. Integra con Boris, aplica el frenado, el límite de c y los bordes.
   3. Gestiona las desintegraciones y añade los puntos de traza.
   4. Ejecuta la cola programada.
   5. Compacta los arrays.
4. **LaunchInput.Update**: arrastre y previsualización.
5. **LateUpdate**: TrailManager sube las trazas modificadas a los LineRenderer. ParticleRenderer, FieldVisualizer y FlashEffects dibujan con `Graphics.RenderMesh*`.

### 4.3 Jerarquía de la escena (la genera el menú 2)
```
AR Session                 ARSession (+ ARInputManager si existe)
XR Origin (Mobile AR)      XROrigin, ARPlaneManager (prefab PlanoRA), ARRaycastManager, ARAnchorManager
 └─ Camera Offset
     └─ Main Camera        Camera, ARCameraManager, ARCameraBackground, TrackedPoseDriver
Simulacion                 SimulationEngine, ReactionSystem, ExperimentPresets
CamaraBurbujas (inactiva)  ← se activa y se ancla al colocarla
 └─ SimRoot (escala 0,25)  TrailManager, ParticleRenderer, FieldVisualizer, FlashEffects, ChamberVisual
Interfaz                   UIController, TutorialController   (el Canvas se crea por código)
EventSystem                EventSystem, InputSystemUIInputModule
Controladores              ARPlacementController, LaunchInput
```
**Clave del diseño**: la física vive en el cubo `[-1, 1]³` local de **SimRoot**, cuya escala es TAMANO/2. Al escalar o mover la cámara de burbujas no cambia nada de la física.

### 4.4 Referencias entre componentes
Todas son `[SerializeField]` y las conecta SceneSetup. Si alguna falta, el `Awake` de cada componente lo avisa en consola y el componente se desactiva. Para reconectar a mano, estos son los nombres de los campos:

| Componente | Campos |
|---|---|
| SimulationEngine | reactions, trails |
| ExperimentPresets | engine |
| TrailManager | trailMaterial (PR_Trail), dashMaterial (PR_TrailDash) |
| ParticleRenderer | engine, coreMaterial (PR_Core), glowMaterial (PR_Glow), viewCamera |
| FieldVisualizer | engine, glowMaterial |
| FlashEffects | engine, glowMaterial, viewCamera |
| ChamberVisual | glowMaterial, lineMaterial (PR_Trail) |
| UIController | engine, presets, trails |
| TutorialController | ui |
| ARPlacementController | raycastManager, planeManager, arCamera, chamber, simRoot, engine, ui, presets, lineMaterial, chamberSize |
| LaunchInput | engine, placement, ui, arCamera, simRoot, lineMaterial |

---

## 5. Física

### 5.1 Unidades
- **Longitud**: la cámara mide 2 u de lado. Con 0,5 m, 1 u equivale a 25 cm.
- **Tiempo**: segundos simulados, afectados por el control de velocidad.
- **c = 1,2 u/s.** Ninguna partícula con masa supera 0,98·c. El fotón viaja siempre a c.

### 5.2 Tabla de partículas (`ParticleDefs.cs`)
| Símbolo | m_sim | q | τ_sim | Masa real | Familia | Quarks | Vida real |
|---|---|---|---|---|---|---|---|
| e⁻ | 1 | −1 | — | 0,511 MeV/c² | Leptón | — | Estable |
| e⁺ | 1 | +1 | — | 0,511 MeV/c² | Antileptón | — | Estable |
| μ⁻ / μ⁺ | 4 | ∓1 | 3 s | 105,7 MeV/c² | Leptón | — | 2,2 μs |
| π⁺ / π⁻ | 5 | ±1 | 1,6 s | 139,6 MeV/c² | Mesón | u d̄ / d ū | 26 ns |
| p | 14 | +1 | — | 938,3 MeV/c² | Barión | u u d | >10³⁴ años |
| p̄ | 14 | −1 | — | 938,3 MeV/c² | Antibarión | ū ū d̄ | Estable |
| n | 14 | 0 | 12 s | 939,6 MeV/c² | Barión | u d d | ≈ 880 s |
| γ | 0 | 0 | — | 0 | Bosón de gauge | — | Estable |

La vida de cada partícula inestable se sortea al nacer: `vida = −ln(1 − U) · τ`.

### 5.3 Fuerza de Lorentz: método de Boris
Para `dv/dt = (q/m) · v × B`, en cada subpaso:
```
v⁻ = v + a·dt/2                      (a = aceleración de Coulomb)
t  = (q·B/m)·dt/2
s  = 2t / (1 + |t|²)
v' = v⁻ + v⁻ × t
v⁺ = v⁻ + v' × s
v  = v⁺ + a·dt/2
v *= (1 − densidad·0,36·dt)          (solo con carga)
x += v·dt
```
Boris conserva exactamente |v| en un campo magnético puro, así que las órbitas no se abren ni se cierran por error numérico. Las espirales que se cierran vienen solo del medio.

**Radio de giro**: `r = m·v⊥ / (|q|·|B|)`. Con B = 3,6 y v = 0,6 u/s:
- e⁻ → r ≈ 0,17 u (espiral cerrada)
- μ → 0,67 u
- p → 2,3 u (casi recta)

### 5.4 Coulomb
`F = K·qi·qj·r / (|r|² + ε²)^(3/2)`, con K = 0,04 y ε = 0,04. Se puede desactivar.

### 5.5 Medio (densidad de 0 a 1)
Cuando una partícula cargada baja de 0,036 u/s y tiene más de 0,5 s de edad:
- **e⁺** → se aniquila con un electrón del medio y produce γγ.
- **e⁻** → queda absorbido.
- **μ y π** → se quedan quietos hasta desintegrarse.
- **p y p̄** → permanecen.

### 5.6 Reacciones
| Reacción | Condición | Productos |
|---|---|---|
| e⁻ + e⁺ | d < 0,03 u, ambos con edad > 0,25 s | 2γ de 0,511 MeV en direcciones opuestas |
| p + p̄ | d < 0,06 u | 2π⁺ + 2π⁻ (tetraedro con ±15°, de 0,66 a 1,08 u/s más v_cm) + 2γ de 67 MeV |
| γ + nucleón | E > 1,022 MeV, d < 0,1 u, probabilidad 9·dt (o segura a < 0,07 u si tiene forzarPar) | e⁻ + e⁺ abiertos ±0,16–0,26 rad, rapidez 0,42 + 0,135·(E − 1,022) |
| π⁺ → μ⁺ν | vida | impulso 0,36 |
| π⁻ → μ⁻ν̄ | vida | impulso 0,36 |
| μ⁻ → e⁻ν̄ν / μ⁺ → e⁺νν̄ | vida | impulso 0,78 |
| n → p e⁻ ν̄ | vida | p: 0,09 · e⁻: 0,84 |

Los fotones de una aniquilación e⁻e⁺ (0,511 MeV) **no pueden** crear pares: no llegan al umbral. Los del π⁰ (67 MeV) sí pueden. Esto respeta la física real.

### 5.7 Constantes ajustables
| Qué | Dónde |
|---|---|
| c, masas, radios, colores, τ | `ParticleDefs.cs` |
| K, ε, paso base, rango de reacción | `SimulationEngine.cs` (constantes al inicio) |
| Frenado (0,36) y umbral de parada (0,036) | `SimulationEngine.Step` |
| Umbrales y velocidades de las reacciones | `ReactionSystem.cs` |
| Ancho de trazas y tamaño del pool | `TrailManager.cs` |
| Fórmulas de velocidad y energía del lanzamiento | `LaunchInput.Speed` / `LaunchInput.PhotonEnergy` |

---

## 6. Renderizado y rendimiento

- **Objetivo**: 60 fps con 300 partículas y trazas.
- **Partículas**: `Graphics.RenderMeshInstanced` hace una llamada por tipo para los núcleos (URP Unlit opaco) y otra para los halos (aditivo). Son 20 llamadas como máximo.
- **Trazas**: un pool de 560 LineRenderer en espacio local, cada uno con un buffer circular de 400 puntos. Solo se suben a la GPU las trazas que cambiaron, con `SetPositions(NativeSlice)`, sin basura de memoria.
- **Fantasmas**: al morir una partícula, su traza pierde max(2, 1,2 %) de puntos por frame. Hay un máximo de 260.
- **Sin GC en el bucle**: el estado vive en arrays preasignados (SoA), la compactación se hace en el sitio y los RenderParams son structs.
- **Mallas y texturas procedurales**: esfera de 14×9 segmentos, quad, cubo, pirámide, degradado radial y textura discontinua.
- **Shader propio** `ParticulasRA/Unlit`: color × textura, sin luces, con GPU instancing y mezcla configurable por material (opaco, aditivo o alfa). Funciona igual en el pipeline integrado y en URP.
- **Por qué los materiales son assets**: si un material se crea solo en tiempo de ejecución, Unity puede eliminar al compilar la variante del shader que necesita (por ejemplo la de instancing). Entonces se ve rosa o invisible en el móvil. Por eso SceneSetup crea los materiales base como assets y en tiempo de ejecución solo se clonan y se tiñen.

Si un móvil de gama baja va lento:
- baja la longitud de trazas;
- reduce `SimulationEngine.MaxParticles`;
- o reduce los segmentos de la esfera en `ProceduralAssets`.

---

## 7. Interfaz

La interfaz se construye entera por código en `UIController.Awake`. No hay que montar nada en el Editor.
- **Canvas** en modo Screen Space Overlay, escalado a 1080×1920 por ancho, y ajustado a `Screen.safeArea`.
- **Barra superior**: el nombre de la app, el contador ("N partículas · M con carga · K eventos") y el botón **Reubicar**.
- **Estado**: muestra los mensajes de guía, el aviso de permiso de cámara y el de compatibilidad con ARCore.
- **Registro**: 5 líneas con la ecuación de cada reacción. Cada línea se desvanece a los 3,8 s.
- **Panel inferior**: ocupa el 45 % de la pantalla, se puede plegar y desplazar. Contiene:
  - 10 chips de partícula
  - la ficha educativa
  - los 4 experimentos
  - los botones Pausa y Limpiar
  - los Ajustes, plegables
- **Paleta de colores**: panel #0D121E, bordes #1C2537, texto #E7ECF6, texto secundario #8B97AE, acento #4FD1FF.

---

## 8. Decisiones de diseño (respecto al superprompt)

1. **Texto uGUI clásico con fuente del sistema, no TextMeshPro.**
   - La app usa muchos símbolos: μ, π, γ, ν̄, ⁺, ⁻, ₑ, p̄.
   - TextMeshPro necesitaría un atlas de fuentes con todos esos glifos y la importación de *TMP Essentials*.
   - La fuente dinámica del sistema (Roboto o Noto en Android) los cubre sin configurar nada.
2. **Botones sin emojis** (☄️💥⚡✨). El texto clásico no dibuja emojis en color de forma fiable.
3. **Materiales base creados como assets** por SceneSetup, con un shader propio en lugar de URP Unlit (ver sección 6). Así el proyecto no depende de configurar URP para compilar en la nube.
4. **Cull Off** en todos los materiales, porque las mallas procedurales y los halos se ven desde cualquier lado.
5. **Ancla** creada con `AddComponent<ARAnchor>()`. Funciona en AR Foundation 5 y 6.
6. **Modo de prueba en el Editor** con ratón: colocar con clic, escalar con la rueda.
7. **Los mensajes de los experimentos no cuentan como eventos.** Solo las reacciones físicas suman en el contador.
8. **Coulomb O(n²) en C# normal, sin Jobs ni Burst.** Con 300 partículas son unos 45 000 pares por subpaso, asumible en un móvil de gama media. Si hiciera falta, es el primer sitio donde aplicar Burst.

---

## 9. Solución de problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| La cámara se ve negra | Falta la AR Background Renderer Feature, o Vulkan sigue activo | Sección 2.5, paso 3. Player › Graphics APIs: solo OpenGLES3 |
| "Este dispositivo no es compatible con ARCore" | El móvil no está en la lista de ARCore | Prueba con otro dispositivo |
| "Instalando Servicios de Google Play para RA…" se queda fijo | Falta la app de ARCore o no está actualizada | Instálala o actualízala desde Play Store |
| "Se necesita permiso de cámara" | El permiso fue denegado | Botón *Conceder permiso*, o Ajustes › Apps › Partículas RA › Permisos |
| Objetos rosas | El shader no es de URP o se eliminó su variante | Vuelve a ejecutar el menú 2. Comprueba que el proyecto usa URP (Graphics › Default Render Pipeline) |
| Partículas o halos invisibles | Falta un material en el inspector | Revisa la tabla 4.4 o vuelve a ejecutar el menú 2 |
| Error de Gradle o JDK | Versiones mezcladas | Preferences › External Tools: usa el JDK, SDK y NDK que instala Unity Hub |
| `InvalidOperationException: You are trying to read Input using UnityEngine.Input` | Active Input Handling en modo antiguo | Pon *Input System Package (New)* o *Both* (sección 2.4) |
| Los toques sobre el panel lanzan partículas | Falta el EventSystem con InputSystemUIInputModule | El menú 2 lo crea; UIController también lo crea si falta |
| Error de compilación con `ARInputManager` o con un menú de XR | Diferencias entre versiones de AR Foundation | SceneSetup lo detecta por reflexión y tiene una alternativa manual. Si falla, crea *GameObject › XR › XR Origin (Mobile AR)* y *AR Session* a mano y vuelve a ejecutar el menú 2 |
| No detecta el suelo | Poca luz o suelo sin textura | Más luz, o apunta a una superficie con dibujo |

> **Importante.** Este código se escribió siguiendo las APIs de Unity 6 y AR Foundation 5/6, pero **no se compiló en Unity** (el entorno donde se generó no tiene Unity). Revisé a mano las llaves y los tipos. Si al importar aparece algún error en la consola, copia el mensaje completo, con archivo y línea, y se corrige en el momento.

---

## 10. Criterios de aceptación

Márcalos al probar en el dispositivo.

- [ ] El APK se instala y se abre en un dispositivo con ARCore, detecta el suelo y coloca la cámara de burbujas anclada.
- [ ] El pellizco escala la cámara y el giro con dos dedos la rota. Tocar la interfaz no lanza partículas.
- [ ] Con B ≠ 0, el e⁻ y el e⁺ giran en sentidos opuestos. El e⁻ traza una espiral cerrada y el p apenas se curva.
- [ ] Una partícula lanzada en paralelo a B (eje Z, hacia delante) va recta. Lanzada en oblicuo, traza una hélice.
- [ ] Con densidad > 0, las espirales se cierran hacia dentro y un e⁺ frenado produce un destello.
- [ ] "γ → e⁻e⁺" muestra 4 "V" de producción de pares.
- [ ] "p̄ + p" impacta y produce 4 piones. En el registro aparecen π→μ→e y las trazas cambian de color.
- [ ] "e⁻ + e⁺" con B = 0: se atraen, se aniquilan y salen dos fotones opuestos.
- [ ] Con 300 partículas y trazas, ~60 fps sin picos de GC (compruébalo con Profiler: Window › Analysis › Profiler, con un *Development Build* y *Autoconnect Profiler*).
- [ ] Toda la interfaz está en español y el tutorial solo sale la primera vez. Para volver a verlo, borra los datos de la app.

---

## 11. Cómo ampliar el proyecto

- **Partícula nueva**:
  1. Añádela a `PType` y a `ParticleDefs.All`, en el mismo orden.
  2. Sube `ParticleDefs.Count`.
  3. Si es inestable, añade su desintegración en `ReactionSystem.Decay`.
  4. La interfaz y el render la incluyen solos.
- **Experimento nuevo**: crea un método público en `ExperimentPresets` y añade su botón en `UIController.BuildPanel`.
- **Sonido**: suscríbete a `SimulationEngine.OnFlash` y reproduce un clip con un `AudioSource` en la posición `SimRoot.TransformPoint(pos)`.
- **Captura o vídeo**: usa `ScreenCapture.CaptureScreenshot` desde un botón nuevo.
- **Rendimiento extra**: pasa el bucle O(n²) de `SimulationEngine.Step` a un `IJobParallelFor` con Burst, o usa una rejilla espacial para las reacciones.
