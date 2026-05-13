# 🏔️ Rey de la Colina — Arquitectura del Jugador

> **Motor:** Unity 6000.4.2f1 · **Red:** Netcode for GameObjects (NGO 2.x) · **Plataforma:** Android LAN

---

## Resumen del PDF aplicado

| Técnica del PDF | Dónde se aplica |
|---|---|
| Evitar allocs en runtime | `PlayerCombat`: `OverlapSphereNonAlloc` + buffer estático |
| Cachear hashes de Animator | `PlayerCombat`, `PlayerNetworkSync`, `PlayerHUD` |
| No usar Find() en runtime | `PlayerSpawner`: array de SpawnPoints en Inspector |
| `sharedMaterial` | No se crea copia de material en scripts |
| Desactivar cálculos innecesarios | `PlayerController`: desactiva Movement/Combat en clientes remotos |
| Actualizar UI solo cuando cambia | `PlayerHUD`: guards `_lastHealth`, `_lastTeam` |
| Evitar polling de input | `VirtualJoystick`: EventSystems (OnDrag) en vez de Update |
| Object Pooling | `PlayerSpawner` + respawn reutiliza el mismo NetworkObject |

---

## Estructura de archivos creados

```
Assets/Scripts/
├── Players/
│   ├── PlayerStats.cs          ← Datos de red (vida, equipo, nombre)
│   ├── PlayerMovement.cs       ← Movimiento 3D con CharacterController
│   ├── PlayerCombat.cs         ← Ataque cuerpo a cuerpo (OverlapSphereNonAlloc)
│   ├── PlayerNetworkSync.cs    ← Sincronización de Transform + Animator por red
│   ├── PlayerHUD.cs            ← UI: barra de vida + billboard flotante
│   └── PlayerController.cs     ← Orquestador principal del prefab
├── Managers/
│   └── PlayerSpawner.cs        ← Spawn/respawn de jugadores en el servidor
└── UI/
    ├── VirtualJoystick.cs      ← Joystick virtual para Android
    └── MobileInputBridge.cs    ← Puente joystick → InputSystem
```

---

## Jerarquía del Prefab `Player`

```
Player (NetworkObject)
├── [Components]: NetworkObject, CharacterController
├── [Components]: PlayerStats, PlayerMovement, PlayerCombat
├── [Components]: PlayerNetworkSync, PlayerHUD, PlayerController
│
├── Body (SkinnedMeshRenderer)           ← modelo 3D + Animator
│
├── CameraRoot (Transform)               ← pivot de cámara
│   └── PlayerCamera (Camera)            ← Main Camera del jugador
│
└── HUD_Billboard (Canvas WorldSpace)    ← nameTag + healthbar flotante
    ├── PlayerNameText (TextMeshPro)
    └── HealthBar (Slider)
```

## Configuración de componentes

| Componente | Campo | Valor |
|---|---|---|
| `CharacterController` | Height | 1.8 |
| `CharacterController` | Radius | 0.4 |
| `CharacterController` | Center Y | 0.9 |
| `PlayerMovement` | cameraRoot | CameraRoot transform |
| `PlayerMovement` | lookSensitivity | 0.15 |
| `PlayerCombat` | playerLayer | Layer "Player" |
| `PlayerCombat` | attackRange | 1.8 |
| `PlayerHUD` | billboardRoot | HUD_Billboard GO |
| `PlayerController` | playerCamera | PlayerCamera GO |
| `PlayerController` | bodyRenderer | Body SkinnedMeshRenderer |

---

## Flujo de red

```
Cliente (Owner)
    ├─ Lee Input System → mueve con CharacterController
    ├─ Escribe NetworkVariables (Position, Rotation, Speed)
    └─ ServerRpc: TakeDamage, PerformAttack

Servidor
    ├─ Valida ataques (OverlapSphereNonAlloc)
    ├─ Aplica daño en PlayerStats.Health
    ├─ Retransmite NetworkVariables a todos
    └─ Gestiona respawn timer

Clientes remotos
    ├─ Leen NetworkVariables
    ├─ Interpolan posición/rotación (Lerp/Slerp)
    └─ Actualizan Animator
```

> [!IMPORTANT]
> Crear el Layer **"Player"** en Tags & Layers y asignarlo al prefab. Asignar ese layer en `PlayerCombat.playerLayer`.

---

## Android — Input táctil

Agregar `On-Screen Stick` de Unity al Canvas del joystick y asignarlo a la action `Move` del InputSystem. Esto integra directamente sin código adicional.

> [!TIP]
> En `Project Settings → Player → Other Settings` activar **Scripting Backend: IL2CPP** y **Target Architectures: ARM64** para mejor rendimiento en Android.

---

## Próximos pasos

- [ ] `HillZone.cs` — zona de la colina (trigger que detecta jugadores)
- [ ] `HillGameManager.cs` — puntuación, rondas, condición de victoria
- [ ] `LANNetworkManager.cs` — host/join por IP local (UDP)
- [ ] `LobbyUI.cs` — pantalla de host/join para Android
- [ ] `GameHUD.cs` — puntuaciones en partida, temporizador de ronda
