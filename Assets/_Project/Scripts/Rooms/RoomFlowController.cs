using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class RoomFlowController : MonoBehaviour
{
    private readonly struct ExitKey : IEquatable<ExitKey>
    {
        public ExitKey(string roomId, RoomDirection direction)
        {
            RoomId = roomId;
            Direction = direction;
        }

        public string RoomId { get; }
        public RoomDirection Direction { get; }

        public bool Equals(ExitKey other)
        {
            return RoomId == other.RoomId && Direction == other.Direction;
        }

        public override bool Equals(object obj)
        {
            return obj is ExitKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RoomId != null ? RoomId.GetHashCode() : 0) * 397) ^ (int)Direction;
            }
        }
    }

    private sealed class GeneratedRoomNode
    {
        public GeneratedRoomNode(GameObject prefab, string runtimeId, Vector2Int cell, Vector3 worldPosition)
        {
            Prefab = prefab;
            RuntimeId = runtimeId;
            Cell = cell;
            WorldPosition = worldPosition;
        }

        public GameObject Prefab { get; }
        public string RuntimeId { get; }
        public Vector2Int Cell { get; }
        public Vector3 WorldPosition { get; }
        public RoomDefinition Definition { get; set; }
    }

    private sealed class PlacementOption
    {
        public GameObject RoomPrefab { get; set; }
        public GeneratedRoomNode Parent { get; set; }
        public RoomDirection ParentExit { get; set; }
        public Vector2Int Cell { get; set; }
        public Vector3 WorldPosition { get; set; }
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> roomPrefabs = new List<GameObject>();

    [Header("Generation")]
    [SerializeField, Min(1)] private int roomsToGenerate = 3;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed;
    [SerializeField] private bool alignRoomInstancesByExits = true;
    [SerializeField, Min(0f)] private float roomConnectionGap;
    [SerializeField] private Vector2 roomSpacing = new Vector2(4f, 2.5f);

    [Header("Runtime")]
    [SerializeField] private Vector2 cameraOffset;
    [SerializeField, Min(0f)] private float enemySpawnRadius = 0.12f;
    [SerializeField, Min(0f)] private float transitionCooldown = 0.25f;
    [SerializeField] private bool logRoomFlow = true;

    [Header("Room Camera Transition Prototype")]
    [SerializeField] private bool animateRoomTransitions = true;
    [SerializeField, Min(0f)] private float cameraTransitionDuration = 0.42f;
    [SerializeField] private AnimationCurve cameraTransitionCurve = CreateDefaultCameraTransitionCurve();
    [SerializeField] private bool blockGameplayInputDuringCameraTransition = true;
    [SerializeField, Min(0f)] private float entryTeleportPadding = 0.01f;
    [SerializeField, Range(0f, 1f)] private float playerTeleportAtCameraProgress = 0.82f;
    [SerializeField, Min(0f)] private float entryExitRearmDistance = 0.16f;
    [SerializeField, Min(0f)] private float previousRoomDeactivateDelay = 0.12f;

    [SerializeField, HideInInspector] private GameObject startRoomPrefab;
    [SerializeField, HideInInspector] private GameObject room01Prefab;
    [SerializeField, HideInInspector] private GameObject room02Prefab;
    [FormerlySerializedAs("slimePrefab")]
    [SerializeField, HideInInspector] private GameObject legacyEnemyPrefab;

    private readonly Dictionary<string, GeneratedRoomNode> generatedRoomsById = new Dictionary<string, GeneratedRoomNode>();
    private readonly Dictionary<Vector2Int, GeneratedRoomNode> generatedRoomsByCell = new Dictionary<Vector2Int, GeneratedRoomNode>();
    private readonly Dictionary<ExitKey, GeneratedRoomNode> generatedTransitions = new Dictionary<ExitKey, GeneratedRoomNode>();
    private readonly Dictionary<GameObject, RoomDirection[]> prefabExitsCache = new Dictionary<GameObject, RoomDirection[]>();
    private readonly Dictionary<GameObject, Dictionary<RoomDirection, Vector3>> prefabExitPositionsCache = new Dictionary<GameObject, Dictionary<RoomDirection, Vector3>>();
    private readonly Dictionary<GameObject, Bounds> prefabRoomBoundsCache = new Dictionary<GameObject, Bounds>();
    private readonly Dictionary<RoomDefinition, Coroutine> previousRoomDeactivateRoutines = new Dictionary<RoomDefinition, Coroutine>();
    private readonly HashSet<string> completedRoomIds = new HashSet<string>();

    private Transform roomsRoot;
    private PlayerController player;
    private Camera cachedMainCamera;
    private GeneratedRoomNode activeRoom;
    private float nextAllowedTransitionTime;
    private Coroutine cameraTransitionRoutine;
    private bool roomTransitionInProgress;
    private bool gameplayInputBlockedByRoomTransition;
    private RoomExit entryExitWaitingForRearm;

    public static RoomFlowController Instance { get; private set; }
    public static event Action<int> CompletedRoomCountChanged;

    public int CompletedRoomCount => completedRoomIds.Count;
    public bool CameraTransitionsEnabled => animateRoomTransitions;

    public void SetCameraTransitionsEnabled(bool enabled)
    {
        animateRoomTransitions = enabled;
    }

    private void Awake()
    {
        AutoAssignEditorPrefabs();
        MigrateLegacyRoomFields();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        if (gameplayInputBlockedByRoomTransition)
        {
            GameplayInputGate.SetSceneTransitionBlocked(false);
            gameplayInputBlockedByRoomTransition = false;
        }
        entryExitWaitingForRearm = null;

        foreach (Coroutine routine in previousRoomDeactivateRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        previousRoomDeactivateRoutines.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        roomsRoot = new GameObject("GeneratedRooms").transform;
        player = FindOrCreatePlayer();

        GenerateMap();

        GeneratedRoomNode startRoom = FindStartNode();

        if (startRoom == null)
        {
            Log("Map generation failed: start room was not generated.");
            return;
        }

        EnterRoom(startRoom, RoomDirection.None, true);
    }

    private void Update()
    {
        UpdateEntryExitRearm();
    }

    public void TryEnterExit(RoomExit roomExit, PlayerController transitionPlayer)
    {
        if (roomExit == null || roomExit.Room == null || transitionPlayer == null)
        {
            return;
        }

        if (roomTransitionInProgress ||
            Time.unscaledTime < nextAllowedTransitionTime)
        {
            return;
        }

        if (activeRoom == null || roomExit.Room != activeRoom.Definition)
        {
            return;
        }

        if (IsEntryExitWaitingForRearm(roomExit))
        {
            return;
        }

        if (roomExit.Room.HasAliveSpawnedEnemies)
        {
            nextAllowedTransitionTime = Time.unscaledTime + transitionCooldown;
            Log($"Exit locked: {roomExit.Room.RoomId} still has alive enemies.");
            return;
        }

        ExitKey exitKey = new ExitKey(roomExit.Room.RoomId, roomExit.Direction);

        if (!generatedTransitions.TryGetValue(exitKey, out GeneratedRoomNode targetRoom))
        {
            Log($"Exit ignored: {roomExit.Room.RoomId} -> {roomExit.Direction}. No generated adjacent room.");
            return;
        }

        player = transitionPlayer;
        RegisterCompletedRoom(roomExit.Room);
        EnterRoom(targetRoom, RoomDirectionUtility.Opposite(roomExit.Direction), false);
    }

    private void GenerateMap()
    {
        generatedRoomsById.Clear();
        generatedRoomsByCell.Clear();
        generatedTransitions.Clear();
        prefabExitsCache.Clear();
        prefabExitPositionsCache.Clear();
        prefabRoomBoundsCache.Clear();
        completedRoomIds.Clear();
        entryExitWaitingForRearm = null;
        CompletedRoomCountChanged?.Invoke(CompletedRoomCount);

        int generationSeed = randomizeSeed ? Guid.NewGuid().GetHashCode() : seed;
        System.Random random = new System.Random(generationSeed);
        List<GameObject> validRooms = GetValidRoomPrefabs();
        GameObject startRoom = FindStartRoomPrefab(validRooms);

        if (startRoom == null)
        {
            Log("Map generation failed: Room_Start prefab was not found.");
            return;
        }

        GeneratedRoomNode startNode = AddGeneratedRoom(startRoom, Vector2Int.zero, 0, Vector3.zero);
        List<GameObject> reusableRooms = GetReusableRoomPrefabs(validRooms, startRoom);
        List<GameObject> unusedUniqueRooms = new List<GameObject>(reusableRooms);
        Shuffle(reusableRooms, random);
        Shuffle(unusedUniqueRooms, random);

        int targetRoomCount = Mathf.Max(1, roomsToGenerate);
        int placementIndex = 1;
        int safetyLimit = Mathf.Max(32, targetRoomCount * 24);

        while (generatedRoomsById.Count < targetRoomCount && safetyLimit-- > 0)
        {
            if (TryPlaceNextRoom(
                    unusedUniqueRooms,
                    reusableRooms,
                    placementIndex,
                    random,
                    out GameObject placedPrefab,
                    out GeneratedRoomNode placedRoom,
                    out bool usedUniqueRoom))
            {
                placementIndex++;

                if (usedUniqueRoom)
                {
                    unusedUniqueRooms.Remove(placedPrefab);
                }

                string sourceMode = usedUniqueRoom ? "unique" : "reused";
                Log($"Generated room: {placedRoom.RuntimeId} at {placedRoom.Cell} from {placedPrefab.name}. Source: {sourceMode}.");
                continue;
            }

            Log("Map generation stopped: no remaining room can connect to the current generated map.");
            break;
        }

        InstantiateGeneratedRooms();
        BuildGeneratedTransitions();
        LogGeneratedMap(generationSeed, startNode);
    }

    private GeneratedRoomNode AddGeneratedRoom(GameObject roomPrefab, Vector2Int cell, int index, Vector3 worldPosition)
    {
        string runtimeId = GetUniqueRuntimeRoomId(roomPrefab, index);
        GeneratedRoomNode node = new GeneratedRoomNode(roomPrefab, runtimeId, cell, worldPosition);
        generatedRoomsById.Add(node.RuntimeId, node);
        generatedRoomsByCell.Add(cell, node);
        return node;
    }

    private bool TryPlaceNextRoom(
        List<GameObject> unusedUniqueRooms,
        List<GameObject> reusableRooms,
        int placementIndex,
        System.Random random,
        out GameObject placedPrefab,
        out GeneratedRoomNode placedRoom,
        out bool usedUniqueRoom)
    {
        List<PlacementOption> options = BuildPlacementOptions(unusedUniqueRooms);
        usedUniqueRoom = options.Count > 0;

        if (!usedUniqueRoom)
        {
            options = BuildPlacementOptions(reusableRooms);
        }

        if (options.Count == 0)
        {
            placedPrefab = null;
            placedRoom = null;
            usedUniqueRoom = false;
            return false;
        }

        PlacementOption selected = options[random.Next(options.Count)];
        placedPrefab = selected.RoomPrefab;
        placedRoom = AddGeneratedRoom(selected.RoomPrefab, selected.Cell, placementIndex, selected.WorldPosition);
        Log($"Placement selected: {selected.Parent.RuntimeId}.{selected.ParentExit} -> {placedRoom.RuntimeId}.");
        return true;
    }

    private List<PlacementOption> BuildPlacementOptions(List<GameObject> sourceRooms)
    {
        List<PlacementOption> options = new List<PlacementOption>();

        for (int i = 0; i < sourceRooms.Count; i++)
        {
            GameObject roomPrefab = sourceRooms[i];

            if (roomPrefab == null || GetPrefabId(roomPrefab) == "Room_Start")
            {
                continue;
            }

            foreach (GeneratedRoomNode parent in generatedRoomsById.Values)
            {
                RoomDirection[] parentExits = GetPrefabExitDirections(parent.Prefab);

                for (int j = 0; j < parentExits.Length; j++)
                {
                    RoomDirection parentExit = parentExits[j];
                    RoomDirection requiredEntry = RoomDirectionUtility.Opposite(parentExit);

                    if (!PrefabHasExit(roomPrefab, requiredEntry))
                    {
                        continue;
                    }

                    Vector2Int targetCell = parent.Cell + RoomDirectionUtility.ToCellOffset(parentExit);

                    if (generatedRoomsByCell.ContainsKey(targetCell))
                    {
                        continue;
                    }

                    Vector3 worldPosition = GetRoomWorldPosition(parent, parentExit, roomPrefab, requiredEntry, targetCell);

                    options.Add(new PlacementOption
                    {
                        RoomPrefab = roomPrefab,
                        Parent = parent,
                        ParentExit = parentExit,
                        Cell = targetCell,
                        WorldPosition = worldPosition
                    });
                }
            }
        }

        return options;
    }

    private Vector3 GetRoomWorldPosition(
        GeneratedRoomNode parent,
        RoomDirection parentExit,
        GameObject childPrefab,
        RoomDirection childEntry,
        Vector2Int targetCell)
    {
        if (!alignRoomInstancesByExits ||
            parent == null ||
            !TryGetPrefabRoomBoundsLocal(parent.Prefab, out Bounds parentBounds) ||
            !TryGetPrefabRoomBoundsLocal(childPrefab, out Bounds childBounds))
        {
            return GetFallbackRoomWorldPosition(targetCell);
        }

        Vector2Int cellOffset = RoomDirectionUtility.ToCellOffset(parentExit);

        if (cellOffset == Vector2Int.zero)
        {
            return GetFallbackRoomWorldPosition(targetCell);
        }

        Vector3 worldPosition = parent.WorldPosition;

        if (cellOffset.x != 0)
        {
            float parentEdge = parent.WorldPosition.x + (cellOffset.x > 0 ? parentBounds.max.x : parentBounds.min.x);
            float childEdge = cellOffset.x > 0 ? childBounds.min.x : childBounds.max.x;
            worldPosition.x = parentEdge - childEdge + cellOffset.x * roomConnectionGap;
            worldPosition.y = GetPerpendicularAlignedRoomPosition(parent, parentExit, childPrefab, childEntry, parentBounds, childBounds, false);
        }
        else
        {
            float parentEdge = parent.WorldPosition.y + (cellOffset.y > 0 ? parentBounds.max.y : parentBounds.min.y);
            float childEdge = cellOffset.y > 0 ? childBounds.min.y : childBounds.max.y;
            worldPosition.x = GetPerpendicularAlignedRoomPosition(parent, parentExit, childPrefab, childEntry, parentBounds, childBounds, true);
            worldPosition.y = parentEdge - childEdge + cellOffset.y * roomConnectionGap;
        }

        worldPosition.z = 0f;
        return worldPosition;
    }

    private float GetPerpendicularAlignedRoomPosition(
        GeneratedRoomNode parent,
        RoomDirection parentExit,
        GameObject childPrefab,
        RoomDirection childEntry,
        Bounds parentBounds,
        Bounds childBounds,
        bool alignX)
    {
        if (TryGetPrefabExitLocalPosition(parent.Prefab, parentExit, out Vector3 parentExitLocalPosition) &&
            TryGetPrefabExitLocalPosition(childPrefab, childEntry, out Vector3 childEntryLocalPosition))
        {
            return alignX
                ? parent.WorldPosition.x + parentExitLocalPosition.x - childEntryLocalPosition.x
                : parent.WorldPosition.y + parentExitLocalPosition.y - childEntryLocalPosition.y;
        }

        return alignX
            ? parent.WorldPosition.x + parentBounds.center.x - childBounds.center.x
            : parent.WorldPosition.y + parentBounds.center.y - childBounds.center.y;
    }

    private Vector3 GetFallbackRoomWorldPosition(Vector2Int cell)
    {
        return new Vector3(cell.x * roomSpacing.x, cell.y * roomSpacing.y, 0f);
    }

    private void InstantiateGeneratedRooms()
    {
        foreach (GeneratedRoomNode node in generatedRoomsById.Values)
        {
            GameObject roomObject = Instantiate(node.Prefab, node.WorldPosition, Quaternion.identity, roomsRoot);
            roomObject.name = node.RuntimeId;

            RoomDefinition roomDefinition = roomObject.GetComponent<RoomDefinition>();

            if (roomDefinition == null)
            {
                roomDefinition = roomObject.AddComponent<RoomDefinition>();
            }

            roomDefinition.SetRuntimeRoomId(node.RuntimeId);
            roomDefinition.PrepareRuntime();
            roomObject.SetActive(false);
            node.Definition = roomDefinition;
        }
    }

    private void BuildGeneratedTransitions()
    {
        foreach (GeneratedRoomNode node in generatedRoomsById.Values)
        {
            RoomDirection[] exits = GetPrefabExitDirections(node.Prefab);

            for (int i = 0; i < exits.Length; i++)
            {
                RoomDirection exit = exits[i];
                Vector2Int neighborCell = node.Cell + RoomDirectionUtility.ToCellOffset(exit);

                if (!generatedRoomsByCell.TryGetValue(neighborCell, out GeneratedRoomNode neighbor))
                {
                    continue;
                }

                RoomDirection neighborEntry = RoomDirectionUtility.Opposite(exit);

                if (!PrefabHasExit(neighbor.Prefab, neighborEntry))
                {
                    continue;
                }

                generatedTransitions[new ExitKey(node.RuntimeId, exit)] = neighbor;
            }
        }
    }

    private void RegisterCompletedRoom(RoomDefinition roomDefinition)
    {
        if (roomDefinition == null || roomDefinition.IsStartRoom || roomDefinition.HasAliveSpawnedEnemies)
        {
            return;
        }

        if (!completedRoomIds.Add(roomDefinition.RoomId))
        {
            return;
        }

        CompletedRoomCountChanged?.Invoke(CompletedRoomCount);
        Log($"Room completed: {roomDefinition.RoomId}. Completed rooms: {CompletedRoomCount}.");
    }

    private void EnterRoom(GeneratedRoomNode targetRoom, RoomDirection entryDirection, bool usePlayerSpawn)
    {
        if (targetRoom == null || targetRoom.Definition == null)
        {
            Log("Room transition failed: target generated room is missing.");
            return;
        }

        RoomDefinition previousRoomDefinition = activeRoom != null ? activeRoom.Definition : null;
        bool shouldAnimateCamera = ShouldAnimateCameraTransition(previousRoomDefinition, targetRoom.Definition);

        activeRoom = targetRoom;
        CancelPreviousRoomDeactivate(activeRoom.Definition);
        activeRoom.Definition.gameObject.SetActive(true);
        activeRoom.Definition.PrepareRuntime();

        bool shouldTeleportPlayer = usePlayerSpawn || entryDirection != RoomDirection.None;
        RoomExit entryExitToRearm = !usePlayerSpawn && entryDirection != RoomDirection.None
            ? activeRoom.Definition.GetExit(entryDirection)
            : null;
        Vector3 playerTeleportPosition = usePlayerSpawn
            ? activeRoom.Definition.GetPlayerStartPosition()
            : activeRoom.Definition.GetEntryPosition(entryDirection, entryTeleportPadding, GetPlayerCollider());
        bool delayPlayerTeleport = shouldTeleportPlayer && !usePlayerSpawn && shouldAnimateCamera;

        if (usePlayerSpawn)
        {
            TeleportPlayer(playerTeleportPosition);
            entryExitWaitingForRearm = null;
        }
        else if (shouldTeleportPlayer && !delayPlayerTeleport)
        {
            TeleportPlayer(playerTeleportPosition);
            ArmEntryExitRearm(entryExitToRearm);
        }

        MoveCameraToRoom(
            previousRoomDefinition,
            activeRoom.Definition,
            shouldAnimateCamera,
            delayPlayerTeleport,
            playerTeleportPosition,
            entryExitToRearm);

        int spawnedEnemies = activeRoom.Definition.SpawnEnemiesOnce(enemyPrefabs, enemySpawnRadius, entryDirection);
        float cameraLockTime = shouldAnimateCamera ? cameraTransitionDuration : 0f;
        nextAllowedTransitionTime = Time.unscaledTime + transitionCooldown + cameraLockTime;

        Log($"Room entered: {activeRoom.RuntimeId}. Cell: {activeRoom.Cell}. Entry: {entryDirection}. Enemies spawned now: {spawnedEnemies}.");
    }

    private PlayerController FindOrCreatePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null && playerPrefab != null)
        {
            playerObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            playerObject.name = playerPrefab.name;
        }

        if (playerObject == null)
        {
            return null;
        }

        if (playerObject.GetComponent<RoomAwarenessMember>() == null)
        {
            playerObject.AddComponent<RoomAwarenessMember>();
        }

        ResetPlayerForNewRun(playerObject);

        return playerObject.GetComponent<PlayerController>();
    }

    private void ResetPlayerForNewRun(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerHurtReaction hurtReaction = playerObject.GetComponent<PlayerHurtReaction>();

        if (hurtReaction != null)
        {
            hurtReaction.ResetReaction();
        }

        PlayerDeathController deathController = playerObject.GetComponent<PlayerDeathController>();

        if (deathController != null)
        {
            deathController.ResetDeathState();
        }

        Health playerHealth = playerObject.GetComponent<Health>();

        if (playerHealth != null)
        {
            playerHealth.RestoreToFull();
        }
    }

    private Collider2D GetPlayerCollider()
    {
        return player != null ? player.GetComponent<Collider2D>() : null;
    }

    private bool IsEntryExitWaitingForRearm(RoomExit roomExit)
    {
        return entryExitWaitingForRearm != null && roomExit == entryExitWaitingForRearm;
    }

    private void UpdateEntryExitRearm()
    {
        if (entryExitWaitingForRearm == null)
        {
            return;
        }

        Collider2D playerCollider = GetPlayerCollider();

        if (playerCollider == null)
        {
            entryExitWaitingForRearm = null;
            return;
        }

        float distanceToExit = entryExitWaitingForRearm.GetDistanceTo(playerCollider);

        if (distanceToExit < entryExitRearmDistance)
        {
            return;
        }

        Log($"Entry exit rearmed: {entryExitWaitingForRearm.name}. Player distance: {distanceToExit:0.###}.");
        entryExitWaitingForRearm = null;
    }

    private void ArmEntryExitRearm(RoomExit entryExit)
    {
        entryExitWaitingForRearm = entryExit;

        if (entryExitWaitingForRearm != null)
        {
            Log($"Entry exit locked until player moves {entryExitRearmDistance:0.###} units away: {entryExitWaitingForRearm.name}.");
        }
    }

    private void TeleportPlayer(Vector3 position)
    {
        if (player == null)
        {
            Log("Player teleport skipped: PlayerController was not found.");
            return;
        }

        position.z = player.transform.position.z;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = new Vector2(position.x, position.y);
        }
        else
        {
            player.transform.position = position;
        }

        Physics2D.SyncTransforms();
    }

    private void MoveCameraToRoom(
        RoomDefinition previousRoomDefinition,
        RoomDefinition targetRoomDefinition,
        bool animateCamera,
        bool teleportPlayerDuringTransition,
        Vector3 playerTeleportPosition,
        RoomExit entryExitToRearm)
    {
        Vector3 targetCameraPosition = GetCameraPositionForRoom(targetRoomDefinition);

        if (!animateCamera)
        {
            if (teleportPlayerDuringTransition)
            {
                TeleportPlayer(playerTeleportPosition);
                ArmEntryExitRearm(entryExitToRearm);
            }

            SnapCameraToPosition(targetCameraPosition);
            SchedulePreviousRoomDeactivate(previousRoomDefinition);
            return;
        }

        StartCameraTransition(
            previousRoomDefinition,
            targetRoomDefinition,
            targetCameraPosition,
            teleportPlayerDuringTransition,
            playerTeleportPosition,
            entryExitToRearm);
    }

    private bool ShouldAnimateCameraTransition(RoomDefinition previousRoomDefinition, RoomDefinition targetRoomDefinition)
    {
        return animateRoomTransitions &&
               cameraTransitionDuration > 0f &&
               previousRoomDefinition != null &&
               targetRoomDefinition != null &&
               previousRoomDefinition != targetRoomDefinition;
    }

    private Vector3 GetCameraPositionForRoom(RoomDefinition roomDefinition)
    {
        Camera mainCamera = GetMainCamera();

        if (mainCamera == null)
        {
            Log("Camera move skipped: Main Camera was not found.");
            return Vector3.zero;
        }

        if (roomDefinition == null)
        {
            Log("Camera move skipped: room definition was not found.");
            return mainCamera.transform.position;
        }

        Vector3 targetPosition = roomDefinition.GetCameraCenter();
        Vector3 cameraPosition = mainCamera.transform.position;
        cameraPosition.x = targetPosition.x + cameraOffset.x;
        cameraPosition.y = targetPosition.y + cameraOffset.y;
        return cameraPosition;
    }

    private void SnapCameraToPosition(Vector3 cameraPosition)
    {
        Camera mainCamera = GetMainCamera();

        if (mainCamera == null)
        {
            return;
        }

        Transform cameraTransform = mainCamera.transform;
        cameraTransform.position = cameraPosition;
    }

    private void StartCameraTransition(
        RoomDefinition previousRoomDefinition,
        RoomDefinition targetRoomDefinition,
        Vector3 targetCameraPosition,
        bool teleportPlayerDuringTransition,
        Vector3 playerTeleportPosition,
        RoomExit entryExitToRearm)
    {
        if (cameraTransitionRoutine != null)
        {
            StopCoroutine(cameraTransitionRoutine);
            FinishCameraTransition(previousRoomDefinition, targetRoomDefinition);
        }

        cameraTransitionRoutine = StartCoroutine(AnimateCameraTransition(
            previousRoomDefinition,
            targetRoomDefinition,
            targetCameraPosition,
            teleportPlayerDuringTransition,
            playerTeleportPosition,
            entryExitToRearm));
    }

    private IEnumerator AnimateCameraTransition(
        RoomDefinition previousRoomDefinition,
        RoomDefinition targetRoomDefinition,
        Vector3 targetCameraPosition,
        bool teleportPlayerDuringTransition,
        Vector3 playerTeleportPosition,
        RoomExit entryExitToRearm)
    {
        Camera mainCamera = GetMainCamera();

        if (mainCamera == null)
        {
            if (teleportPlayerDuringTransition)
            {
                TeleportPlayer(playerTeleportPosition);
                ArmEntryExitRearm(entryExitToRearm);
            }

            FinishCameraTransition(previousRoomDefinition, targetRoomDefinition);
            yield break;
        }

        roomTransitionInProgress = true;

        if (blockGameplayInputDuringCameraTransition || teleportPlayerDuringTransition)
        {
            GameplayInputGate.SetSceneTransitionBlocked(true);
            gameplayInputBlockedByRoomTransition = true;
        }

        Transform cameraTransform = mainCamera.transform;
        Vector3 startCameraPosition = cameraTransform.position;
        float elapsedTime = 0f;
        float duration = Mathf.Max(0.01f, cameraTransitionDuration);
        bool playerWasTeleported = false;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            float curvedTime = EvaluateCameraTransitionCurve(normalizedTime);
            Vector3 cameraPosition = Vector3.LerpUnclamped(startCameraPosition, targetCameraPosition, curvedTime);
            cameraPosition.z = targetCameraPosition.z;
            cameraTransform.position = cameraPosition;

            if (teleportPlayerDuringTransition &&
                !playerWasTeleported &&
                normalizedTime >= playerTeleportAtCameraProgress)
            {
                TeleportPlayer(playerTeleportPosition);
                ArmEntryExitRearm(entryExitToRearm);
                playerWasTeleported = true;
            }
            yield return null;
        }

        if (teleportPlayerDuringTransition && !playerWasTeleported)
        {
            TeleportPlayer(playerTeleportPosition);
            ArmEntryExitRearm(entryExitToRearm);
        }

        cameraTransform.position = targetCameraPosition;
        FinishCameraTransition(previousRoomDefinition, targetRoomDefinition);
    }

    private float EvaluateCameraTransitionCurve(float normalizedTime)
    {
        if (cameraTransitionCurve == null || cameraTransitionCurve.length == 0)
        {
            return SmootherStep(normalizedTime);
        }

        return Mathf.Clamp01(cameraTransitionCurve.Evaluate(normalizedTime));
    }

    private static AnimationCurve CreateDefaultCameraTransitionCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.24f, 0.055f, 0.28f, 0.28f),
            new Keyframe(0.76f, 0.945f, 0.28f, 0.28f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    private static float SmootherStep(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private Camera GetMainCamera()
    {
        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
        }

        return cachedMainCamera;
    }

    private void FinishCameraTransition(RoomDefinition previousRoomDefinition, RoomDefinition targetRoomDefinition)
    {
        cameraTransitionRoutine = null;
        roomTransitionInProgress = false;

        if (gameplayInputBlockedByRoomTransition)
        {
            GameplayInputGate.SetSceneTransitionBlocked(false);
            gameplayInputBlockedByRoomTransition = false;
        }
        SchedulePreviousRoomDeactivate(previousRoomDefinition);
    }

    private void SchedulePreviousRoomDeactivate(RoomDefinition previousRoomDefinition)
    {
        if (!ShouldDeactivatePreviousRoom(previousRoomDefinition))
        {
            return;
        }

        CancelPreviousRoomDeactivate(previousRoomDefinition);

        if (previousRoomDeactivateDelay <= 0f)
        {
            previousRoomDefinition.gameObject.SetActive(false);
            return;
        }

        Coroutine routine = StartCoroutine(DeactivatePreviousRoomAfterDelay(previousRoomDefinition));
        previousRoomDeactivateRoutines[previousRoomDefinition] = routine;
    }

    private IEnumerator DeactivatePreviousRoomAfterDelay(RoomDefinition previousRoomDefinition)
    {
        yield return new WaitForSecondsRealtime(previousRoomDeactivateDelay);
        previousRoomDeactivateRoutines.Remove(previousRoomDefinition);

        if (ShouldDeactivatePreviousRoom(previousRoomDefinition))
        {
            previousRoomDefinition.gameObject.SetActive(false);
        }
    }

    private void CancelPreviousRoomDeactivate(RoomDefinition roomDefinition)
    {
        if (roomDefinition == null ||
            !previousRoomDeactivateRoutines.TryGetValue(roomDefinition, out Coroutine routine))
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        previousRoomDeactivateRoutines.Remove(roomDefinition);
    }

    private bool ShouldDeactivatePreviousRoom(RoomDefinition previousRoomDefinition)
    {
        return previousRoomDefinition != null &&
               activeRoom != null &&
               previousRoomDefinition != activeRoom.Definition &&
               previousRoomDefinition.gameObject.activeSelf;
    }

    private GeneratedRoomNode FindStartNode()
    {
        foreach (GeneratedRoomNode node in generatedRoomsById.Values)
        {
            if (GetPrefabId(node.Prefab) == "Room_Start")
            {
                return node;
            }
        }

        return null;
    }

    private GameObject FindStartRoomPrefab(List<GameObject> validRooms)
    {
        for (int i = 0; i < validRooms.Count; i++)
        {
            if (GetPrefabId(validRooms[i]) == "Room_Start")
            {
                return validRooms[i];
            }
        }

        return null;
    }

    private List<GameObject> GetValidRoomPrefabs()
    {
        List<GameObject> validRooms = new List<GameObject>();

        for (int i = 0; i < roomPrefabs.Count; i++)
        {
            GameObject roomPrefab = roomPrefabs[i];

            if (roomPrefab == null || roomPrefab.name.Contains("Template"))
            {
                continue;
            }

            if (GetPrefabExitDirections(roomPrefab).Length == 0 && roomPrefab.name != "Room_Start")
            {
                continue;
            }

            if (!validRooms.Contains(roomPrefab))
            {
                validRooms.Add(roomPrefab);
            }
        }

        return validRooms;
    }

    private List<GameObject> GetReusableRoomPrefabs(List<GameObject> validRooms, GameObject startRoom)
    {
        List<GameObject> reusableRooms = new List<GameObject>();

        for (int i = 0; i < validRooms.Count; i++)
        {
            GameObject roomPrefab = validRooms[i];

            if (roomPrefab == null || roomPrefab == startRoom || GetPrefabId(roomPrefab) == "Room_Start")
            {
                continue;
            }

            reusableRooms.Add(roomPrefab);
        }

        return reusableRooms;
    }

    private RoomDirection[] GetPrefabExitDirections(GameObject roomPrefab)
    {
        if (roomPrefab == null)
        {
            return Array.Empty<RoomDirection>();
        }

        if (prefabExitsCache.TryGetValue(roomPrefab, out RoomDirection[] cachedExits))
        {
            return cachedExits;
        }

        List<RoomDirection> directions = new List<RoomDirection>();
        Transform[] children = roomPrefab.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (!child.name.StartsWith("Exit") || child.GetComponent<Collider2D>() == null)
            {
                continue;
            }

            if (!RoomDirectionUtility.TryParseFromName(child.name, out RoomDirection direction) ||
                direction == RoomDirection.None ||
                directions.Contains(direction))
            {
                continue;
            }

            directions.Add(direction);
        }

        RoomDirection[] exits = directions.ToArray();
        prefabExitsCache.Add(roomPrefab, exits);
        return exits;
    }

    private bool TryGetPrefabExitLocalPosition(GameObject roomPrefab, RoomDirection direction, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;

        if (roomPrefab == null || direction == RoomDirection.None)
        {
            return false;
        }

        Dictionary<RoomDirection, Vector3> exitPositions = GetPrefabExitLocalPositions(roomPrefab);
        return exitPositions.TryGetValue(direction, out localPosition);
    }

    private Dictionary<RoomDirection, Vector3> GetPrefabExitLocalPositions(GameObject roomPrefab)
    {
        if (prefabExitPositionsCache.TryGetValue(roomPrefab, out Dictionary<RoomDirection, Vector3> cachedPositions))
        {
            return cachedPositions;
        }

        Dictionary<RoomDirection, Vector3> positions = new Dictionary<RoomDirection, Vector3>();
        Transform[] children = roomPrefab.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (!child.name.StartsWith("Exit") || child.GetComponent<Collider2D>() == null)
            {
                continue;
            }

            if (!RoomDirectionUtility.TryParseFromName(child.name, out RoomDirection direction) ||
                direction == RoomDirection.None ||
                positions.ContainsKey(direction))
            {
                continue;
            }

            positions.Add(direction, roomPrefab.transform.InverseTransformPoint(child.position));
        }

        prefabExitPositionsCache.Add(roomPrefab, positions);
        return positions;
    }

    private bool TryGetPrefabRoomBoundsLocal(GameObject roomPrefab, out Bounds localBounds)
    {
        localBounds = default;

        if (roomPrefab == null)
        {
            return false;
        }

        if (prefabRoomBoundsCache.TryGetValue(roomPrefab, out localBounds))
        {
            return true;
        }

        if (TryGetPrefabRoomBoundsCollider(roomPrefab, out Collider2D roomBoundsCollider) &&
            TryGetColliderLocalBounds(roomPrefab.transform, roomBoundsCollider, out localBounds))
        {
            prefabRoomBoundsCache.Add(roomPrefab, localBounds);
            return true;
        }

        if (TryGetPrefabRendererLocalBounds(roomPrefab, out localBounds))
        {
            prefabRoomBoundsCache.Add(roomPrefab, localBounds);
            return true;
        }

        return false;
    }

    private static bool TryGetPrefabRoomBoundsCollider(GameObject roomPrefab, out Collider2D roomBoundsCollider)
    {
        roomBoundsCollider = null;

        if (roomPrefab == null)
        {
            return false;
        }

        Transform[] children = roomPrefab.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null || child.name != "Bounds")
            {
                continue;
            }

            roomBoundsCollider = child.GetComponent<Collider2D>();

            if (roomBoundsCollider != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetColliderLocalBounds(Transform root, Collider2D boundsCollider, out Bounds localBounds)
    {
        localBounds = default;

        if (root == null || boundsCollider == null)
        {
            return false;
        }

        BoxCollider2D boxCollider = boundsCollider as BoxCollider2D;

        if (boxCollider != null)
        {
            Vector2 halfSize = boxCollider.size * 0.5f;
            Vector2 offset = boxCollider.offset;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            EncapsulateLocalPoint(root, boxCollider.transform, new Vector3(offset.x - halfSize.x, offset.y - halfSize.y, 0f), ref min, ref max);
            EncapsulateLocalPoint(root, boxCollider.transform, new Vector3(offset.x - halfSize.x, offset.y + halfSize.y, 0f), ref min, ref max);
            EncapsulateLocalPoint(root, boxCollider.transform, new Vector3(offset.x + halfSize.x, offset.y - halfSize.y, 0f), ref min, ref max);
            EncapsulateLocalPoint(root, boxCollider.transform, new Vector3(offset.x + halfSize.x, offset.y + halfSize.y, 0f), ref min, ref max);

            localBounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        Bounds worldBounds = boundsCollider.bounds;

        if (worldBounds.size == Vector3.zero)
        {
            return false;
        }

        localBounds = ConvertWorldBoundsToLocalBounds(root, worldBounds);
        return true;
    }

    private static bool TryGetPrefabRendererLocalBounds(GameObject roomPrefab, out Bounds localBounds)
    {
        localBounds = default;

        if (roomPrefab == null)
        {
            return false;
        }

        Renderer[] renderers = roomPrefab.GetComponentsInChildren<Renderer>(true);
        Bounds worldBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            worldBounds.Encapsulate(renderer.bounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        localBounds = ConvertWorldBoundsToLocalBounds(roomPrefab.transform, worldBounds);
        return true;
    }

    private static Bounds ConvertWorldBoundsToLocalBounds(Transform root, Bounds worldBounds)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        EncapsulateWorldBoundsCorner(root, worldBounds.min.x, worldBounds.min.y, worldBounds.min.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.min.x, worldBounds.min.y, worldBounds.max.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.min.x, worldBounds.max.y, worldBounds.min.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.min.x, worldBounds.max.y, worldBounds.max.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.max.x, worldBounds.min.y, worldBounds.min.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.max.x, worldBounds.min.y, worldBounds.max.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.max.x, worldBounds.max.y, worldBounds.min.z, ref min, ref max);
        EncapsulateWorldBoundsCorner(root, worldBounds.max.x, worldBounds.max.y, worldBounds.max.z, ref min, ref max);

        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static void EncapsulateWorldBoundsCorner(Transform root, float x, float y, float z, ref Vector3 min, ref Vector3 max)
    {
        Vector3 localPoint = root.InverseTransformPoint(new Vector3(x, y, z));
        min = Vector3.Min(min, localPoint);
        max = Vector3.Max(max, localPoint);
    }

    private static void EncapsulateLocalPoint(
        Transform root,
        Transform pointOwner,
        Vector3 ownerLocalPoint,
        ref Vector3 min,
        ref Vector3 max)
    {
        Vector3 localPoint = root.InverseTransformPoint(pointOwner.TransformPoint(ownerLocalPoint));
        min = Vector3.Min(min, localPoint);
        max = Vector3.Max(max, localPoint);
    }

    private bool PrefabHasExit(GameObject roomPrefab, RoomDirection direction)
    {
        RoomDirection[] exits = GetPrefabExitDirections(roomPrefab);

        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i] == direction)
            {
                return true;
            }
        }

        return false;
    }

    private void MigrateLegacyRoomFields()
    {
        enemyPrefabs ??= new List<GameObject>();
        roomPrefabs ??= new List<GameObject>();

        AddEnemyPrefabIfNeeded(legacyEnemyPrefab);
        AddRoomPrefabIfNeeded(startRoomPrefab);
        AddRoomPrefabIfNeeded(room01Prefab);
        AddRoomPrefabIfNeeded(room02Prefab);
    }

    private void AddEnemyPrefabIfNeeded(GameObject enemyPrefab)
    {
        enemyPrefabs ??= new List<GameObject>();

        if (enemyPrefab != null && !enemyPrefabs.Contains(enemyPrefab))
        {
            enemyPrefabs.Add(enemyPrefab);
        }
    }

    private void AddRoomPrefabIfNeeded(GameObject roomPrefab)
    {
        roomPrefabs ??= new List<GameObject>();

        if (roomPrefab != null && !roomPrefabs.Contains(roomPrefab))
        {
            roomPrefabs.Add(roomPrefab);
        }
    }

    private string GetPrefabId(GameObject roomPrefab)
    {
        return roomPrefab != null ? roomPrefab.name.Replace("(Clone)", string.Empty).Trim() : string.Empty;
    }

    private string GetUniqueRuntimeRoomId(GameObject roomPrefab, int index)
    {
        string baseId = GetPrefabId(roomPrefab);

        if (!generatedRoomsById.ContainsKey(baseId))
        {
            return baseId;
        }

        int suffix = Mathf.Max(1, index);
        string candidateId = $"{baseId}_{suffix}";

        while (generatedRoomsById.ContainsKey(candidateId))
        {
            suffix++;
            candidateId = $"{baseId}_{suffix}";
        }

        return candidateId;
    }

    private void LogGeneratedMap(int generationSeed, GeneratedRoomNode startNode)
    {
        Log($"Map generated. Seed: {generationSeed}. Rooms: {generatedRoomsById.Count}. Start: {(startNode != null ? startNode.RuntimeId : "none")}.");

        foreach (GeneratedRoomNode node in generatedRoomsById.Values)
        {
            RoomDirection[] exits = GetPrefabExitDirections(node.Prefab);
            Log($"Map node: {node.RuntimeId} from {node.Prefab.name} at {node.Cell}. Prefab exits: {string.Join(", ", exits)}.");
        }

        foreach (KeyValuePair<ExitKey, GeneratedRoomNode> transition in generatedTransitions)
        {
            Log($"Map transition: {transition.Key.RoomId}.{transition.Key.Direction} -> {transition.Value.RuntimeId}.");
        }
    }

    private void Log(string message)
    {
        if (logRoomFlow)
        {
            Debug.Log($"[RoomFlow] {message}", this);
        }
    }

    private static void Shuffle<T>(List<T> items, System.Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private void OnValidate()
    {
        roomsToGenerate = Mathf.Max(1, roomsToGenerate);
        enemySpawnRadius = Mathf.Max(0f, enemySpawnRadius);
        transitionCooldown = Mathf.Max(0f, transitionCooldown);
        roomConnectionGap = Mathf.Max(0f, roomConnectionGap);
        roomSpacing.x = Mathf.Max(0.1f, roomSpacing.x);
        roomSpacing.y = Mathf.Max(0.1f, roomSpacing.y);
        cameraTransitionDuration = Mathf.Max(0f, cameraTransitionDuration);
        entryTeleportPadding = Mathf.Max(0f, entryTeleportPadding);
        playerTeleportAtCameraProgress = Mathf.Clamp01(playerTeleportAtCameraProgress);
        entryExitRearmDistance = Mathf.Max(0f, entryExitRearmDistance);
        previousRoomDeactivateDelay = Mathf.Max(0f, previousRoomDeactivateDelay);

        AutoAssignEditorPrefabs();
        MigrateLegacyRoomFields();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void AutoAssignEditorPrefabs()
    {
#if UNITY_EDITOR
        enemyPrefabs ??= new List<GameObject>();
        roomPrefabs ??= new List<GameObject>();

        playerPrefab = playerPrefab != null
            ? playerPrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");

        string[] enemyGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" });

        for (int i = 0; i < enemyGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(enemyGuids[i]);
            AddEnemyPrefabIfNeeded(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        AddRoomPrefabIfNeeded(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Rooms/Room_Start.prefab"));

        string[] roomGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Rooms" });

        for (int i = 0; i < roomGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(roomGuids[i]);

            if (Path.GetFileNameWithoutExtension(path).Contains("Template"))
            {
                continue;
            }

            AddRoomPrefabIfNeeded(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
#endif
    }
}
