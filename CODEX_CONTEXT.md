\# Technical Summary for Codex — Unity 6 Pixel Top-Down Roguelike "Overlook"



\## 1. Project Overview



Проект — \*\*2D Pixel Top-Down Roguelike\*\* на \*\*Unity 6\*\*.


Название игры — Overlook

Игра разрабатывается постепенно, с упором на правильный фундамент, чтобы в будущем не пришлось переписывать базовые системы. Разработка ведётся поэтапно: сначала движение, камера, pixel-perfect настройки, UI, stamina, затем атака, hitbox, HP, враги, боевой цикл и дальше roguelike-системы.



Текущая стадия проекта:



```text

Version: 0.1.0-pre-alpha

Stage: Player Foundation + Early Combat Foundation

```



Текущий основной фокус:



```text

\- стабильное движение персонажа;

\- собственная камера с dead zone;

\- pixel-art rendering setup;

\- stamina UI;

\- первая система атаки;

\- hitbox удара;

\- подготовка к HP / Damage system.

```



\---



\# 2. Important Core Design Decisions



\## 2.1. Pixel Art Settings



Все спрайты персонажа и проекта сейчас используют:



```text

Pixels Per Unit = 100

```



Камера настроена под:



```text

Orthographic Size = 1

```



Это означает:



```text

Camera vertical world size = 2 world units

PPU = 100

Visible world height ≈ 200 pixels

```



Был обсуждён вариант reference resolution:



```text

356 x 200

```



Он логически соответствует:



```text

Camera Size = 1

PPU = 100

```



Но была обнаружена важная проблема: `356x200` плохо масштабируется в популярные разрешения, потому что:



```text

720 / 200 = 3.6

1080 / 200 = 5.4

1440 / 200 = 7.2

```



То есть это не integer scaling. Из-за этого иногда может появляться небольшое мыло, особенно на UI.



Более pixel-perfect-friendly вариант на будущее:



```text

Reference Resolution = 320 x 180

Camera Size = 0.9

PPU = 100

```



Пока проект оставлен на текущем визуальном масштабе, но это важное решение для будущего.



\---



\## 2.2. Scene Sorting by Pivot



Очень важное архитектурное решение:



```text

Scene sorting should be based on Pivot.

Player sprite pivot is placed at the feet.

```



Это нужно обязательно учитывать в будущем.



Смысл:



```text

Pivot = точка стояния персонажа на земле

```



Для top-down игры это правильно, потому что sorting должен учитывать, кто визуально находится “ниже” на сцене.



Правило:



```text

Object with lower Y / lower pivot should be drawn in front.

Object with higher Y / higher pivot should be drawn behind.

```



У персонажа Pivot находится в ногах.

У объектов окружения в будущем тоже нужно ставить Pivot в точке касания с землёй:



```text

Tree pivot = base of trunk

Chest pivot = bottom center

Enemy pivot = feet

Rock pivot = bottom center

NPC pivot = feet

```



Важно:



```text

Collider != Pivot

```



Pivot отвечает за визуальную глубину и сортировку.

Collider отвечает за физические столкновения.



\---



\## 2.3. Character Animation Style



Персонаж может двигаться по 8 направлениям, но анимации используются только для 2 горизонтальных направлений:



```text

Left

Right

```



Это осознанный стиль проекта.



Поведение:



```text

\- движение вправо → персонаж смотрит вправо;

\- движение влево → персонаж смотрит влево;

\- движение вверх/вниз → сохраняется последнее горизонтальное направление.

```



Для этого используется `lastHorizontalDirection`.



\---



\# 3. Project Structure



Базовая структура проекта была предложена такая:



```text

Assets

└── \_Project

&#x20;   ├── Animations

&#x20;   │   └── Player

&#x20;   ├── Art

&#x20;   │   ├── Characters

&#x20;   │   │   └── Player

&#x20;   │   ├── Tiles

&#x20;   │   └── UI

&#x20;   ├── Audio

&#x20;   │   ├── Music

&#x20;   │   └── SFX

&#x20;   ├── Materials

&#x20;   ├── Prefabs

&#x20;   │   ├── Player

&#x20;   │   ├── Enemies

&#x20;   │   ├── Environment

&#x20;   │   └── UI

&#x20;   ├── Scenes

&#x20;   ├── Scripts

&#x20;   │   ├── Player

&#x20;   │   │   └── Combat

&#x20;   │   ├── Camera

&#x20;   │   ├── Combat

&#x20;   │   ├── Enemies

&#x20;   │   ├── Core

&#x20;   │   └── UI

&#x20;   ├── Settings

&#x20;   └── Tiles

```



Главный принцип:



```text

All project-owned files should live under Assets/\_Project

```



Это нужно, чтобы не смешивать собственные файлы с Unity packages / external assets.



\---



\# 4. Player Object Setup



Текущая структура объекта игрока:



```text

Player

├── SpriteRenderer

├── Animator

├── Rigidbody2D

├── BoxCollider2D

├── PlayerController

├── PlayerStamina

├── PlayerAttack

└── AttackHitbox

&#x20;   ├── BoxCollider2D

&#x20;   └── PlayerAttackHitbox

```



\---



\# 5. Rigidbody2D Setup



Для игрока используется `Rigidbody2D`.



Рекомендованные настройки:



```text

Body Type: Dynamic

Gravity Scale: 0

Linear Damping / Drag: 0

Angular Damping / Drag: 0

Interpolate: Interpolate

Collision Detection: Discrete

Constraints: Freeze Rotation Z

```



Причины:



```text

Gravity Scale = 0

```



Top-down игра, гравитация не нужна.



```text

Freeze Rotation Z = true

```



Игрок не должен вращаться от столкновений.



```text

Interpolate = Interpolate

```



Помогло визуально сделать движение персонажа плавнее и убрать микродрожание.



\---



\# 6. Player Collider Setup



Используется `BoxCollider2D`.



Коллайдер не должен покрывать весь спрайт.

Он должен покрывать физическую область ног / нижней части тела.



Важное правило:



```text

Collider = physical body / feet area

Pivot = sorting point / feet point

```



Коллайдер должен быть:



```text

\- ниже визуального центра персонажа;

\- не слишком широкий;

\- не на всю высоту спрайта;

\- примерно на нижнюю часть ног / тела.

```



Для top-down это нужно, чтобы персонаж не цеплялся головой за стены, деревья, декор.



Текущий коллайдер был вручную настроен и оценён как хороший: узкий, расположен в нижней части персонажа. Возможная микро-полировка — сделать его совсем чуть выше, если ощущается, что физическая область слишком “плоская”.



\---



\# 7. Player Movement System



\## 7.1. Current Movement Values



Текущие значения скорости:



```text

Walk Speed = 0.5

Run Speed = 0.75

```



Изначально run speed был `1.0`, но был уменьшен до `0.75`, потому что при маленькой камере и pixel-art масштабе `1.0` ощущался слишком резким.



\---



\## 7.2. Movement Logic



Движение читается через legacy Input API:



```csharp

Input.GetAxisRaw("Horizontal")

Input.GetAxisRaw("Vertical")

```



Движение нормализуется, чтобы диагональное движение не было быстрее:



```csharp

if (moveInput.sqrMagnitude > 1f)

{

&#x20;   moveInput.Normalize();

}

```



Физическое движение выполняется в `FixedUpdate()` через:



```csharp

rb.linearVelocity = moveInput \* currentSpeed;

```



Используется именно `linearVelocity`, потому что проект на Unity 6.



\---



\## 7.3. Animator Parameters for Movement



Текущие параметры Animator:



```text

Speed       Float

Horizontal  Float

IsRunning   Bool

IsAttacking Bool

```



`Speed` сейчас означает не реальную скорость в world units, а факт движения:



```text

Speed = moveInput.sqrMagnitude

```



То есть:



```text

Idle: Speed < 0.01

Movement: Speed > 0.01

```



Важно: ранее была ошибка, когда переход в Run был настроен через:



```text

Speed > 0.749

```



Но `Speed` был равен `moveInput.sqrMagnitude`, то есть при любом движении становился `1`. Из-за этого Run включался всегда.



Решение:



```text

Speed отвечает только за idle/movement

IsRunning отвечает за walk/run

```



\---



\# 8. Custom Camera System



Cinemachine был попробован, но отклонён как слишком тяжёлый и неудобный для проекта.



Принято решение использовать собственный скрипт камеры:



```text

PlayerCameraFollow.cs

```



Путь:



```text

Assets/\_Project/Scripts/Camera/PlayerCameraFollow.cs

```



\---



\## 8.1. Camera Goals



Камера должна быть:



```text

\- simple;

\- predictable;

\- easy to tune;

\- with dead zone;

\- with smooth follow;

\- adapted to player pivot at feet;

\- suitable for pixel-art.

```



\---



\## 8.2. Camera Important Fields



Камера имеет поля:



```text

Target

Target Offset

Smooth Time

Max Speed

Dead Zone Size

Pixel Snap

Pixels Per Unit

```



Рекомендуемые значения:



```text

Target = Player

Target Offset X = 0

Target Offset Y ≈ 0.22 - 0.25



Smooth Time = 0.10 - 0.12

Max Speed = 10



Dead Zone X = 0.20

Dead Zone Y = 0.14



Pixel Snap = false

Pixels Per Unit = 100

```



\---



\## 8.3. Why Target Offset Exists



Так как Pivot персонажа находится в ногах, камера не должна следить прямо за `target.position`.



Если камера будет центрироваться по ногам, персонаж визуально окажется слишком высоко на экране.



Поэтому follow point вычисляется как:



```csharp

target.position + targetOffset

```



`targetOffset.y` поднимает точку слежения от ног к корпусу/визуальному центру персонажа.



\---



\## 8.4. Camera Dead Zone



Dead zone — зона вокруг камеры, внутри которой follow point персонажа может двигаться, а камера не реагирует.



Важно:



```text

Dead zone belongs to camera space, not to player.

```



Был добавлен debug gizmo:



```text

Yellow rectangle = camera dead zone

Green point = player pivot / feet

Cyan point = follow point

Cyan line = offset from pivot to follow point

```



Это сделано для удобной настройки.



\---



\## 8.5. Pixel Snap Decision



В скрипте есть `pixelSnap`, но сейчас он оставлен:



```text

pixelSnap = false

```



Причина:



```text

Camera uses SmoothDamp

Pixel snapping can make smooth camera feel jittery/stair-stepped

```



Сейчас приоритет:



```text

smooth camera feel > strict mathematical pixel snapping

```



\---



\# 9. Pixel Perfect Camera / Resolution



\## 9.1. Pixel Perfect Camera



В Unity 6 используется новый интерфейс `Pixel Perfect Camera`, где нет старых галочек типа:



```text

Crop Frame X

Crop Frame Y

Upscale Render Texture

Pixel Snapping

```



Вместо этого есть:



```text

Crop Frame

Grid Snapping

```



Текущие важные настройки:



```text

Assets Pixels Per Unit = 100

Reference Resolution = 356 x 200

Crop Frame = Windowbox or Pillarbox

Grid Snapping = None

```



\---



\## 9.2. Upscale Render Texture Problem



При установке:



```text

Grid Snapping = Upscale Render Texture

```



картинка начала сильно “плыть” и мылиться. Поэтому это было отключено.



Текущий выбор:



```text

Grid Snapping = None

```



\---



\## 9.3. Aspect Ratio / Windowbox Issue



Была обнаружена проблема:



При нестандартном aspect ratio UI-bar может съезжать в область windowbox / black bars.



Причина:



```text

Canvas in Screen Space - Overlay is anchored to full screen,

while Pixel Perfect Camera windowboxes the game area.

```



Решение было предложено:



```text

Canvas Render Mode = Screen Space - Camera

Render Camera = Main Camera

```



Цель:



```text

UI should stay inside visible game area, not inside black bars.

```



\---



\# 10. Canvas / UI Setup



\## 10.1. Canvas Settings



Рекомендованный setup:



```text

Canvas:

Render Mode = Screen Space - Camera

Render Camera = Main Camera

Pixel Perfect = true



Canvas Scaler:

UI Scale Mode = Scale With Screen Size

Reference Resolution = 356 x 200

Screen Match Mode = Match Width Or Height

Match = 0.5

Reference Pixels Per Unit = 100

```



Важно: если будет принято решение перейти на более integer-friendly baseline, возможно перейти на:



```text

Reference Resolution = 320 x 180

Camera Size = 0.9

```



\---



\## 10.2. UI Blur / Mipmap / Filled Image Issues



Иногда UI stamina bar кажется слегка мыльным.



Возможные причины:



```text

\- non-integer scaling with 356x200 reference;

\- Image Type = Filled can create fractional edge blur;

\- RectTransform has fractional positions/sizes;

\- sprite import settings not ideal;

\- Unity Game View preview scaling;

\- non-integer Game View scale.

```



Для UI-спрайтов нужно проверить:



```text

Texture Type = Sprite (2D and UI)

Filter Mode = Point (no filter)

Compression = None

Generate Mip Maps = Off

Mesh Type = Full Rect

```



Для pixel-perfect stamina fill был рекомендован более надёжный вариант:



```text

Use RectMask2D + change RectTransform width

instead of Image Type = Filled + fillAmount

```



Потому что `fillAmount` может давать дробную границу, а изменение width можно округлять до целого пикселя.



\---



\# 11. Stamina System



Система стамины уже спроектирована и частично реализована.



\## 11.1. PlayerStamina.cs



Путь:



```text

Assets/\_Project/Scripts/Player/PlayerStamina.cs

```



Назначение:



```text

\- хранит current stamina;

\- хранит max stamina;

\- тратит stamina;

\- восстанавливает stamina;

\- сообщает UI об изменениях через event.

```



Рекомендуемые значения:



```text

Max Stamina = 100

Drain Per Second = 25

Recovery Per Second = 18

Recovery Delay = 0.5

```



Публичные свойства:



```csharp

CurrentStamina

MaxStamina

NormalizedStamina

HasStamina

```



Event:



```csharp

event Action<float> StaminaChanged;

```



Метод траты:



```csharp

TrySpend(float amount)

```



\---



\## 11.2. Run Stamina



Бег использует stamina.



Рекомендуемое значение:



```text

Run Stamina Cost Per Second = 25

```



Поведение:



```text

\- Shift + movement → run;

\- run consumes stamina;

\- no stamina → player falls back to walk speed;

\- no run input → stamina recovers after delay.

```



\---



\## 11.3. Attack Stamina Cost



Атака должна тратить stamina.



Выбранный стартовый баланс:



```text

Attack Stamina Cost = 18

```



Причина:



```text

100 stamina / 18 ≈ 5 attacks in a row

```



Это даёт игроку несколько ударов подряд, но не позволяет бесконечно спамить.



\---



\# 12. Stamina UI



\## 12.1. Current Stamina Bar Design



Stamina bar был перерисован несколько раз.



Текущий дизайн:



```text

\- yellow / green-ish stamina fill;

\- lightning icon on the left;

\- black pixel outline;

\- stylized inner arrows / direction marks.

```



Оценка:



```text

\- читается как stamina;

\- lightning icon хорошо работает;

\- дизайн стал более игровым и характерным.

```



Недочёты:



```text

\- lightning icon слишком выпирает влево;

\- если anchor top-left и нужно сохранить 8 px offset от edge,

&#x20; сама bar body оказывается слишком далеко от левого края;

\- inner arrows могут создавать visual noise / shimmer;

\- left side visually heavier than right side.

```



Предложенные улучшения:



```text

\- integrate lightning into the body of the bar;

\- reduce the icon's left protrusion;

\- add small common HUD backing/panel;

\- reduce inner arrow contrast or frequency;

\- add 1px top highlight and darker bottom line;

\- keep 1px inner padding.

```



\---



\## 12.2. Future HUD Layout



Для будущего интерфейса предложен минималистичный HUD:



```text

Top Left:

\- HP bar

\- Stamina bar



Top Right:

\- coins / currency

\- keys

\- floor / room counter



Bottom Left:

\- active item / consumable



Bottom Right:

\- skill / ability / cooldown

```



Для roguelike важно не перегружать экран, особенно центр и нижнюю центральную область.



\---



\# 13. Combat System — Current Stage



Началась разработка боевой системы.



Текущий этап:



```text

Player Attack Foundation

```



Цель:



```text

\- удар по ЛКМ;

\- атака тратит stamina;

\- атака имеет hitbox;

\- hitbox появляется только на короткое время;

\- hitbox отрисовывается через gizmos;

\- HP / Damage system ещё не реализована.

```



\---



\# 14. Attack Animations



У игрока есть готовые спрайты / анимации:



```text

\- attack while idle;

\- attack while walking;

\- attack while running.

```



Анимационный стиль — 2 directions:



```text

left / right

```



Attack animation should respect current/last horizontal direction.



\---



\# 15. Attack Hitbox



\## 15.1. Object Setup



Внутри Player создан child object:



```text

Player

└── AttackHitbox

&#x20;   ├── BoxCollider2D

&#x20;   └── PlayerAttackHitbox

```



`AttackHitbox` должен оставаться активным в Hierarchy.



Важно: объект не выключается целиком.

Включается/выключается только `BoxCollider2D.enabled`.



Причина:



```text

If GameObject is disabled, it is hard to see and tune in Scene.

Keeping object active allows gizmo drawing.

```



\---



\## 15.2. AttackHitbox Collider Settings



```text

BoxCollider2D:

Is Trigger = true

```



Стартовые значения:



```text

AttackHitbox Local Position:

Right: X = 0.18, Y = 0.08

Left:  X = -0.18, Y = 0.08



BoxCollider2D Size:

X = 0.26 - 0.28

Y = 0.20 - 0.22

Offset = 0,0

```



Важно с учётом Pivot в ногах:



```text

Hitbox Y should be above feet/pivot, around body/weapon height.

```



\---



\## 15.3. PlayerAttackHitbox.cs



Путь:



```text

Assets/\_Project/Scripts/Player/Combat/PlayerAttackHitbox.cs

```



Назначение:



```text

\- содержит trigger collider;

\- включает/выключает hitbox;

\- фильтрует цели по LayerMask;

\- пока только Debug.Log при попадании;

\- отрисовывает gizmos.

```



Debug gizmo behavior:



```text

Yellow transparent box = hitbox disabled / tuning mode

Red transparent box = hitbox active during attack

```



Для отрисовки используется `OnDrawGizmos()`.



Также был добавлен `HashSet<Collider2D>`:



```text

hitTargets

```



Цель:



```text

One hitbox activation should hit the same target only once.

```



Пока Damage system не реализована, попадание выводит:



```text

Attack hit: EnemyDummy

```



\---



\## 15.4. Layers



Нужно использовать слои:



```text

Player

PlayerAttack

Enemy

Environment

```



Рекомендации:



```text

Player object layer = Player

AttackHitbox layer = PlayerAttack

EnemyDummy / future enemies layer = Enemy

```



В `PlayerAttackHitbox.TargetLayers` выбирается:



```text

Enemy

```



\---



\# 16. PlayerAttack.cs



Путь:



```text

Assets/\_Project/Scripts/Player/Combat/PlayerAttack.cs

```



Назначение:



```text

\- читает attack input;

\- проверяет cooldown;

\- проверяет stamina;

\- тратит stamina;

\- запускает attack animation;

\- позиционирует hitbox left/right;

\- включает hitbox на короткое время;

\- выключает hitbox;

\- блокирует повторную атаку до конца duration + cooldown.

```



\---



\## 16.1. Current Attack Values



Рекомендуемые значения:



```text

Attack Key = Mouse0

Stamina Cost = 18



Attack Duration = 0.38

Attack Cooldown = 0.12



Hitbox Delay = 0.08

Hitbox Active Time = 0.10

```



Итоговый интервал между атаками:



```text

0.38 + 0.12 = 0.50 sec

```



\---



\## 16.2. Initial Animator Approach and Bug



Изначально атаки запускались через Animator transitions:



```text

Any State → Attack\_Idle

Any State → Attack\_Walk

Any State → Attack\_Run

```



С условиями:



```text

IsAttacking = true

AttackType = 0/1/2

```



Но при спаме ЛКМ появились баги:



```text

\- attack animation повторно проигрывалась;

\- animation could freeze;

\- especially in Run state;

\- Attack\_Run sometimes froze on first frame for several seconds after spam.

```



Были проверены/обсуждены настройки:



```text

Loop Time = OFF

Can Transition To Self = false

Interruption Source = None

Transition Duration = 0

```



Но проблема не исчезла полностью.



\---



\## 16.3. Current Recommended Animator Approach



Из-за бага в Run принято решение изменить подход:



```text

Do not enter attacks through Any State transitions.

Start attack animations directly from code using Animator.Play.

```



То есть:



```csharp

animator.Play(attackStateHash, 0, 0f);

```



Цель:



```text

one input → one explicit animation state

```



А не:



```text

Animator decides which attack transition to take.

```



\---



\## 16.4. Animator Setup After Change



Нужно удалить / отключить:



```text

Any State → Attack\_Idle

Any State → Attack\_Walk

Any State → Attack\_Run

Run → Attack\_Run

Walk → Attack\_Walk

Idle → Attack\_Idle

Attack\_Run → Attack\_Run

Attack\_Run → Attack\_Walk

Attack\_Run → Attack\_Idle

```



Вход в attack теперь только через code:



```text

Animator.Play(...)

```



Оставить только выходы:



```text

Attack\_Idle → Idle

Attack\_Walk → Walk

Attack\_Run → Run

```



Condition:



```text

IsAttacking = false

```



Transition settings:



```text

Has Exit Time = false

Transition Duration = 0

Interruption Source = None

```



Attack clips:



```text

Loop Time = OFF

Loop Pose = OFF

```



\---



\## 16.5. Animator State Names



В `PlayerAttack` должны быть exact state names:



```text

Attack Idle State Name = Attack\_Idle

Attack Walk State Name = Attack\_Walk

Attack Run State Name = Attack\_Run

```



Они должны совпадать с Animator state names буква-в-букву.



\---



\# 17. PlayerController Integration With Attack



В `PlayerController` нужно учитывать `PlayerAttack`.



Проблема:



```text

PlayerController continues updating Animator parameters during attack:

Speed

IsRunning

Horizontal

```



Это может мешать attack animations.



Решение:



```csharp

private PlayerAttack playerAttack;

```



В `Awake()`:



```csharp

playerAttack = GetComponent<PlayerAttack>();

```



В `UpdateAnimation()`:



```csharp

if (playerAttack != null \&\& playerAttack.IsAttacking)

{

&#x20;   return;

}

```



То есть во время атаки locomotion system не должна трогать Animator.



Важно: движение физически может продолжаться, но locomotion animation parameters не обновляются во время attack.



\---



\# 18. Known Bugs / Open Issues



\## 18.1. Attack\_Run Freeze Bug



Текущий главный баг:



```text

When spamming LMB, especially in Run state, attack animation can freeze on the first frame for several seconds.

```



Предположительная причина:



```text

Animator transitions around Run → Attack\_Run → Run conflict during spam.

Any State / transition-driven attack entry is unstable for this setup.

```



Принятое решение:



```text

Remove Any State attack transitions.

Use Animator.Play from PlayerAttack.cs.

Prevent PlayerController from updating locomotion animation while attacking.

```



Нужно проверить после внедрения:



```text

\- no repeated attack playback;

\- no freeze on first frame;

\- spam LMB ignored while attackRoutine != null;

\- cooldown respected;

\- Attack\_Run plays once and exits correctly.

```



\---



\## 18.2. UI Pixel Blur



Иногда stamina bar выглядит немного мыльно.



Potential causes:



```text

\- 356x200 reference is not integer-scaled to common resolutions;

\- Filled Image creates fractional fill edge;

\- Canvas positions/sizes are fractional;

\- Game View scaling;

\- wrong sprite import settings.

```



Recommended fixes:



```text

\- ensure all UI RectTransforms use integer coordinates/sizes;

\- use Point filter, no compression, no mipmaps;

\- consider RectMask2D + rounded width instead of Image.fillAmount;

\- consider future switch to 320x180 + Camera Size 0.9.

```



\---



\## 18.3. Windowbox UI Position



When non-16:9 resolution is used, UI can appear inside black bars if Canvas is Screen Space - Overlay.



Recommended fix:



```text

Canvas Render Mode = Screen Space - Camera

Render Camera = Main Camera

```



\---



\## 18.4. Pixel Perfect Grid Snapping



`Grid Snapping = Upscale Render Texture` caused bad visual artifacts / pixel swimming.



Current decision:



```text

Grid Snapping = None

```



Do not re-enable unless rendering pipeline is reconsidered.



\---



\# 19. Files Changed / Created



\## Original file



```text

Player.cs

```



Original script contained:



```text

\- public moveSpeed;

\- Rigidbody2D;

\- Animator;

\- moveInput;

\- Update input;

\- Speed parameter;

\- Horizontal parameter;

\- rb.linearVelocity in FixedUpdate.

```



It was improved and effectively replaced by `PlayerController.cs`.



\---



\## Created / Current files



\### PlayerController.cs



Expected path:



```text

Assets/\_Project/Scripts/Player/PlayerController.cs

```



Responsibilities:



```text

\- read movement input;

\- normalize diagonal movement;

\- calculate walk/run speed;

\- spend stamina for run;

\- move Rigidbody2D;

\- update locomotion animation;

\- preserve last horizontal direction;

\- avoid updating locomotion animation during attack.

```



Important fields:



```text

walkSpeed = 0.5

runSpeed = 0.75

runStaminaCostPerSecond = 25

```



Animator parameters:



```text

Speed

Horizontal

IsRunning

```



Integration:



```text

PlayerAttack playerAttack;

Skip UpdateAnimation if playerAttack.IsAttacking.

```



\---



\### PlayerCameraFollow.cs



Expected path:



```text

Assets/\_Project/Scripts/Camera/PlayerCameraFollow.cs

```



Responsibilities:



```text

\- custom camera follow;

\- smooth damp;

\- dead zone;

\- target offset for feet pivot;

\- debug gizmos;

\- optional pixel snapping.

```



Important values:



```text

targetOffset = (0, 0.22 to 0.25)

smoothTime = 0.10 to 0.12

maxSpeed = 10

deadZoneSize = (0.20, 0.14)

pixelSnap = false

pixelsPerUnit = 100

```



\---



\### PlayerStamina.cs



Expected path:



```text

Assets/\_Project/Scripts/Player/PlayerStamina.cs

```



Responsibilities:



```text

\- current/max stamina;

\- stamina drain;

\- stamina recovery;

\- recovery delay;

\- TrySpend;

\- StaminaChanged event.

```



Important values:



```text

maxStamina = 100

drainPerSecond = 25

recoveryPerSecond = 18

recoveryDelay = 0.5

```



\---



\### StaminaBarView.cs



Expected path:



```text

Assets/\_Project/Scripts/UI/StaminaBarView.cs

```



Responsibilities:



```text

\- listen to PlayerStamina.StaminaChanged;

\- update UI stamina fill.

```



Initial implementation used:



```text

Image.fillAmount

```



Recommended future improvement:



```text

RectTransform width + RectMask2D + Mathf.Round()

```



\---



\### PlayerAttack.cs



Expected path:



```text

Assets/\_Project/Scripts/Player/Combat/PlayerAttack.cs

```



Responsibilities:



```text

\- attack input;

\- stamina cost;

\- attack cooldown;

\- attack duration;

\- select attack animation by current locomotion state;

\- play attack animation via Animator.Play;

\- position hitbox left/right;

\- enable/disable hitbox by timing;

\- prevent spam using attackRoutine and nextAttackAllowedTime.

```



Important values:



```text

attackKey = Mouse0

staminaCost = 18

attackDuration = 0.38

attackCooldown = 0.12

hitboxDelay = 0.08

hitboxActiveTime = 0.10

rightHitboxOffset = (0.18, 0.08)

leftHitboxOffset = (-0.18, 0.08)

```



Important animation state names:



```text

Attack\_Idle

Attack\_Walk

Attack\_Run

```



\---



\### PlayerAttackHitbox.cs



Expected path:



```text

Assets/\_Project/Scripts/Player/Combat/PlayerAttackHitbox.cs

```



Responsibilities:



```text

\- trigger hitbox;

\- target layer filtering;

\- EnableHitbox;

\- DisableHitbox;

\- prevent duplicate hits per activation;

\- Debug.Log on hit;

\- Draw gizmos always.

```



Important behavior:



```text

Object stays active.

Collider enabled only during hitbox active window.

```



Gizmo colors:



```text

Yellow = disabled/tuning

Red = active/hitting

```



\---



\# 20. Current Recommended PlayerAttack.cs Direction



Codex should ensure `PlayerAttack.cs` uses:



```text

Animator.Play(...)

```



not transition-driven attack entry through `Any State`.



Also ensure:



```text

attackRoutine != null prevents new attack

Time.time < nextAttackAllowedTime prevents cooldown abuse

stamina.TrySpend(staminaCost) gates attack

hitbox only active for hitboxActiveTime

```



\---



\# 21. Next Development Plan



\## Next immediate task



Stabilize attack system fully:



```text

1\. Remove Any State attack transitions.

2\. Use Animator.Play for Attack\_Idle / Attack\_Walk / Attack\_Run.

3\. Ensure PlayerController skips animation updates while attacking.

4\. Confirm LMB spam cannot freeze Attack\_Run.

5\. Confirm one click = one attack.

6\. Confirm hitbox appears once and only during hitbox active window.

```



\---



\## Next stage after attack stability



```text

Stage 3.2 — Health \& Damage System

```



Implement:



```text

\- IDamageable interface;

\- Health component;

\- EnemyDummy with Health;

\- PlayerAttackHitbox applies damage instead of Debug.Log;

\- dummy takes damage;

\- dummy dies at 0 HP;

\- optional hit flash;

\- optional small knockback later.

```



Recommended design:



```text

interface IDamageable

{

&#x20;   void TakeDamage(DamageInfo damageInfo);

}

```



Possible future `DamageInfo`:



```text

amount

source

hitDirection

knockback

```



But for the first HP stage, keep it simple.



\---



\## Later combat additions



```text

\- enemy HP UI;

\- player HP;

\- enemy attacks;

\- player invulnerability frames;

\- knockback;

\- damage numbers;

\- weapon stats;

\- attack cooldown upgrades;

\- stamina upgrades;

\- dash;

\- hit stop;

\- screen shake;

\- attack VFX;

\- SFX.

```



\---



\# 22. Important Constraints for Future Codex Work



Codex must preserve these decisions:



```text

\- Unity 6 project.

\- 2D Pixel Top-Down Roguelike.

\- Sprites PPU = 100.

\- Player pivot is at feet.

\- Scene sorting should be based on Pivot.

\- Player moves in 8 directions but animates in 2 directions.

\- Camera is custom, not Cinemachine.

\- Camera follows target.position + targetOffset, not raw target.position.

\- Rigidbody2D movement uses rb.linearVelocity.

\- Run speed = 0.75.

\- Walk speed = 0.5.

\- Stamina max = 100.

\- Attack stamina cost = 18.

\- Current UI reference = 356x200 unless deliberately changed.

\- Pixel Perfect Grid Snapping Upscale Render Texture is currently not acceptable because it caused visual artifacts.

\- AttackHitbox GameObject should remain active; only collider should toggle.

\- Animator transition-driven attack entry caused bugs, especially in Run.

\- Prefer code-driven attack playback through Animator.Play.

```



\---



\# 23. High-Level Architecture Philosophy



Keep the architecture simple but structured.



Current direction:



```text

PlayerController = movement + locomotion animation

PlayerStamina = stamina resource logic

PlayerAttack = attack input + attack timing + hitbox timing + animation playback

PlayerAttackHitbox = hit detection

PlayerCameraFollow = custom camera

StaminaBarView = UI view for stamina

```



Avoid overengineering for now:



```text

No complex state machine yet.

No dependency injection.

No event bus.

No ScriptableObject weapon system yet.

No full combat framework yet.

```



But keep files separated by responsibility so systems can grow later.



\---



\# 24. Current Milestone Summary



Current project state:



```text

0.1.0-pre-alpha



Done:

\- Player movement

\- Walk/run speed

\- Stamina resource

\- Stamina UI

\- Custom camera with dead zone

\- Camera offset for foot pivot

\- Pixel Perfect setup experiments

\- Basic project folder structure

\- Player collider setup

\- Attack hitbox object

\- Attack hitbox gizmo drawing

\- Attack stamina cost

\- Early attack animation integration



In progress:

\- Stabilizing attack animation playback under input spam

\- Fixing Run attack freeze issue



Next:

\- HP / Damage system

\- EnemyDummy with health

\- Real damage from hitbox

```



\---



\# 25. Immediate Codex Task Recommendation



Codex should first inspect and fix:



```text

PlayerAttack.cs

PlayerController.cs

Animator setup assumptions

PlayerAttackHitbox.cs

```



Expected outcome:



```text

\- LMB spam does not restart/freeze attack animations.

\- Attack\_Run never freezes on first frame.

\- Attack animation is played once per accepted input.

\- Hitbox appears once per accepted attack.

\- Cooldown and stamina cost are respected.

```



Implementation priority:



```text

1\. Make PlayerAttack code-driven via Animator.Play.

2\. Ensure no Any State attack transitions remain required.

3\. Ensure PlayerController does not update locomotion animation while attacking.

4\. Keep hitbox object active and toggle only Collider2D.

5\. Keep debug gizmos.

```



\---



Особенно важные красные флажки для него: \*\*Pivot в ногах\*\*, \*\*сортировка по Pivot\*\*, \*\*кастомная камера\*\*, \*\*не использовать Cinemachine\*\*, \*\*не возвращаться к Any State attack transitions\*\*, и \*\*не включать Upscale Render Texture без причины\*\*.



