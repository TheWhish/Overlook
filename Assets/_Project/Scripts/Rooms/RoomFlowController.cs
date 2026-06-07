using System;
using System.Collections.Generic;
using UnityEngine;

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
        public GeneratedRoomNode(GameObject prefab, string runtimeId, Vector2Int cell)
        {
            Prefab = prefab;
            RuntimeId = runtimeId;
            Cell = cell;
        }

        public GameObject Prefab { get; }
        public string RuntimeId { get; }
        public Vector2Int Cell { get; }
        public RoomDefinition Definition { get; set; }
    }

    private sealed class PlacementOption
    {
        public GameObject RoomPrefab { get; set; }
        public GeneratedRoomNode Parent { get; set; }
        public RoomDirection ParentExit { get; set; }
        public Vector2Int Cell { get; set; }
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject slimePrefab;
    [SerializeField] private List<GameObject> roomPrefabs = new List<GameObject>();

    [Header("Generation")]
    [SerializeField, Min(1)] private int roomsToGenerate = 3;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed;
    [SerializeField] private Vector2 roomSpacing = new Vector2(4f, 2.5f);

    [Header("Runtime")]
    [SerializeField] private Vector2 cameraOffset;
    [SerializeField, Min(0f)] private float entrySpawnPadding = 0.12f;
    [SerializeField, Min(0f)] private float enemySpawnRadius = 0.12f;
    [SerializeField, Min(0f)] private float transitionCooldown = 0.25f;
    [SerializeField] private bool logRoomFlow = true;

    [SerializeField, HideInInspector] private GameObject startRoomPrefab;
    [SerializeField, HideInInspector] private GameObject room01Prefab;
    [SerializeField, HideInInspector] private GameObject room02Prefab;

    private readonly Dictionary<string, GeneratedRoomNode> generatedRoomsById = new Dictionary<string, GeneratedRoomNode>();
    private readonly Dictionary<Vector2Int, GeneratedRoomNode> generatedRoomsByCell = new Dictionary<Vector2Int, GeneratedRoomNode>();
    private readonly Dictionary<ExitKey, GeneratedRoomNode> generatedTransitions = new Dictionary<ExitKey, GeneratedRoomNode>();
    private readonly Dictionary<GameObject, RoomDirection[]> prefabExitsCache = new Dictionary<GameObject, RoomDirection[]>();
    private readonly HashSet<string> completedRoomIds = new HashSet<string>();

    private Transform roomsRoot;
    private PlayerController player;
    private GeneratedRoomNode activeRoom;
    private float nextAllowedTransitionTime;

    public static RoomFlowController Instance { get; private set; }
    public static event Action<int> CompletedRoomCountChanged;

    public int CompletedRoomCount => completedRoomIds.Count;

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

    public void TryEnterExit(RoomExit roomExit, PlayerController transitionPlayer)
    {
        if (roomExit == null || roomExit.Room == null || transitionPlayer == null)
        {
            return;
        }

        if (Time.unscaledTime < nextAllowedTransitionTime)
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

        RegisterCompletedRoom(roomExit.Room);

        player = transitionPlayer;
        EnterRoom(targetRoom, RoomDirectionUtility.Opposite(roomExit.Direction), false);
    }

    private void GenerateMap()
    {
        generatedRoomsById.Clear();
        generatedRoomsByCell.Clear();
        generatedTransitions.Clear();
        prefabExitsCache.Clear();
        completedRoomIds.Clear();
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

        GeneratedRoomNode startNode = AddGeneratedRoom(startRoom, Vector2Int.zero, 0);
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

    private GeneratedRoomNode AddGeneratedRoom(GameObject roomPrefab, Vector2Int cell, int index)
    {
        string runtimeId = GetUniqueRuntimeRoomId(roomPrefab, index);
        GeneratedRoomNode node = new GeneratedRoomNode(roomPrefab, runtimeId, cell);
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
        placedRoom = AddGeneratedRoom(selected.RoomPrefab, selected.Cell, placementIndex);
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

                    options.Add(new PlacementOption
                    {
                        RoomPrefab = roomPrefab,
                        Parent = parent,
                        ParentExit = parentExit,
                        Cell = targetCell
                    });
                }
            }
        }

        return options;
    }

    private void InstantiateGeneratedRooms()
    {
        foreach (GeneratedRoomNode node in generatedRoomsById.Values)
        {
            Vector3 worldPosition = new Vector3(node.Cell.x * roomSpacing.x, node.Cell.y * roomSpacing.y, 0f);
            GameObject roomObject = Instantiate(node.Prefab, worldPosition, Quaternion.identity, roomsRoot);
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

        if (activeRoom != null && activeRoom != targetRoom)
        {
            activeRoom.Definition.gameObject.SetActive(false);
        }

        activeRoom = targetRoom;
        activeRoom.Definition.gameObject.SetActive(true);
        activeRoom.Definition.PrepareRuntime();

        Vector3 spawnPosition = usePlayerSpawn
            ? activeRoom.Definition.GetPlayerStartPosition()
            : activeRoom.Definition.GetEntryPosition(entryDirection, entrySpawnPadding, GetPlayerCollider());

        TeleportPlayer(spawnPosition);
        SnapCameraToRoom(activeRoom.Definition);

        int spawnedEnemies = activeRoom.Definition.SpawnEnemiesOnce(slimePrefab, enemySpawnRadius, entryDirection);
        nextAllowedTransitionTime = Time.unscaledTime + transitionCooldown;

        Log($"Room entered: {activeRoom.RuntimeId}. Cell: {activeRoom.Cell}. Entry: {entryDirection}. Slimes spawned now: {spawnedEnemies}.");
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

        return playerObject.GetComponent<PlayerController>();
    }

    private Collider2D GetPlayerCollider()
    {
        return player != null ? player.GetComponent<Collider2D>() : null;
    }

    private void TeleportPlayer(Vector3 position)
    {
        if (player == null)
        {
            Log("Player teleport skipped: PlayerController was not found.");
            return;
        }

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = position;
        }
        else
        {
            player.transform.position = position;
        }
    }

    private void SnapCameraToRoom(RoomDefinition roomDefinition)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Log("Camera snap skipped: Main Camera was not found.");
            return;
        }

        if (roomDefinition == null)
        {
            Log("Camera snap skipped: room definition was not found.");
            return;
        }

        Vector3 targetPosition = roomDefinition.GetCameraCenter();
        Transform cameraTransform = mainCamera.transform;
        Vector3 cameraPosition = cameraTransform.position;
        cameraPosition.x = targetPosition.x + cameraOffset.x;
        cameraPosition.y = targetPosition.y + cameraOffset.y;
        cameraTransform.position = cameraPosition;
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
        AddRoomPrefabIfNeeded(startRoomPrefab);
        AddRoomPrefabIfNeeded(room01Prefab);
        AddRoomPrefabIfNeeded(room02Prefab);
    }

    private void AddRoomPrefabIfNeeded(GameObject roomPrefab)
    {
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
        entrySpawnPadding = Mathf.Max(0f, entrySpawnPadding);
        enemySpawnRadius = Mathf.Max(0f, enemySpawnRadius);
        transitionCooldown = Mathf.Max(0f, transitionCooldown);
        roomSpacing.x = Mathf.Max(0.1f, roomSpacing.x);
        roomSpacing.y = Mathf.Max(0.1f, roomSpacing.y);

        AutoAssignEditorPrefabs();
        MigrateLegacyRoomFields();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void AutoAssignEditorPrefabs()
    {
#if UNITY_EDITOR
        playerPrefab = playerPrefab != null
            ? playerPrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");

        slimePrefab = slimePrefab != null
            ? slimePrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Slime1.prefab");

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
