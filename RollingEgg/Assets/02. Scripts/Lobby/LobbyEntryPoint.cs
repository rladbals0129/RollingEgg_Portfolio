using RollingEgg.Lobby;
using UnityEngine;
// using UnityEngine.U2D; // Using fully qualified names to avoid ambiguity

public class LobbyEntryPoint : MonoBehaviour
{
    public enum SpawnContext
    {
        Default,
        FromNurtureEggRoom
    }

    public static SpawnContext NextSpawnContext { get; private set; } = SpawnContext.Default;

    [Header("Settings")]
    public Camera lobbyCamera;             // 로비 카메라 (MainCamera 태그가 없거나 비활성화된 경우를 대비해 직접 할당)
    public Transform spawnPoint;           // 캐릭터가 생성될 위치
    public Transform nurtureReturnSpawnPoint; // 육성 복귀 시 알방 스폰 위치
    public BoxCollider2D mapBoundary;      // 기본 맵 경계

    [Header("Resolution Settings")]
    public Vector2Int lobbyResolution = new Vector2Int(320, 200);   // 로비 진입 시 적용할 해상도
    public Vector2Int eggRoomResolution = new Vector2Int(320, 180); // 알방 진입 시 적용할 해상도
    public Vector2Int defaultResolution = new Vector2Int(320, 180); // 그 외/종료 시 복구할 해상도

    [Header("Egg Room Settings")]
    public BoxCollider2D nurtureReturnBoundary; // 알방 맵 경계

    // 나중에는 이걸 데이터 매니저에서 가져오게 될것 (도감 및 플레이블 캐릭터와 연결 TODO)
    // 지금은 테스트를 위해 Inspector에서 프리팹을 직접 할당
    public GameObject defaultCharacterPrefab;

    // 생성된 캐릭터 참조 저장
    private GameObject currentCharacter;
    private bool isEggRoomMode = false; // 현재 알방 모드인지 여부 (중복 적용 방지)

    /// <summary>
    /// 육성 화면에서 로비로 돌아올 때 알방 스폰 포인트를 사용하도록 요청한다.
    /// </summary>
    public static void RequestSpawnAtEggRoom()
    {
        NextSpawnContext = SpawnContext.FromNurtureEggRoom;
    }

    void Start()
    {
        SpawnContext context = NextSpawnContext;
        Transform targetSpawnPoint = ResolveSpawnPoint(context);
        BoxCollider2D targetBoundary = ResolveBoundary(context);

        if (defaultCharacterPrefab == null)
        {
            Debug.LogError("[LobbyEntryPoint] 기본 캐릭터 프리팹이 설정되지 않았습니다.");
            return;
        }

        Vector3 spawnPosition = targetSpawnPoint != null ? targetSpawnPoint.position : Vector3.zero;

        // 1. 캐릭터 생성 (데이터 매니저가 있다면 거기서 현재 선택된 캐릭터를 가져옴)
        currentCharacter = Instantiate(defaultCharacterPrefab, spawnPosition, Quaternion.identity);

        // 생성된 캐릭터를 부모(Lobby_World)의 자식으로 넣을지, 아니면 최상위에 둘지 결정
        // 보통은 독립적으로 움직여야 하므로 최상위(null) 혹은 전용 컨테이너에 둡니다.
        // 여기서는 Lobby_World와 함께 관리되도록 transform.parent로 설정하겠습니다.
        currentCharacter.transform.SetParent(this.transform);

        // 1-1. LobbyPlayerController 설정
        LobbyPlayerController playerController = currentCharacter.GetComponent<LobbyPlayerController>();
        if (playerController == null)
        {
            playerController = currentCharacter.AddComponent<LobbyPlayerController>();
        }

        // 맵 경계 설정
        if (targetBoundary != null)
            playerController.SetMapBoundary(targetBoundary);

        // 2. 카메라 설정
        if (lobbyCamera == null) lobbyCamera = Camera.main; // 할당되지 않았다면 MainCamera 태그 탐색

        if (lobbyCamera != null)
        {
            lobbyCamera.transform.position = new Vector3(currentCharacter.transform.position.x, currentCharacter.transform.position.y, -10f);

            LobbyCamera camScript = lobbyCamera.GetComponent<LobbyCamera>();
            if (camScript == null) camScript = lobbyCamera.gameObject.AddComponent<LobbyCamera>();

            camScript.target = currentCharacter.transform;
            camScript.mapBoundary = targetBoundary;

            lobbyCamera.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[LobbyEntryPoint] 로비 카메라를 찾을 수 없습니다. Inspector에서 할당하거나 MainCamera 태그를 확인하세요.");
        }

        // 스폰 컨텍스트는 일회성으로 사용
        NextSpawnContext = SpawnContext.Default;

        // 초기 해상도 설정 (스폰 위치에 따라 결정)
        // 만약 알방 스폰이라면 eggRoomResolution, 로비라면 lobbyResolution
        bool isLobby = targetBoundary == mapBoundary;
        isEggRoomMode = !isLobby; // 현재 상태 초기화

        SetCameraResolution(isLobby ? lobbyResolution : eggRoomResolution);
    }

    void Update()
    {
        if (currentCharacter == null) return;

        // 플레이어 위치 확인
        Vector2 playerPos = currentCharacter.transform.position;

        // 1. 알방 구역 체크
        if (nurtureReturnBoundary != null && nurtureReturnBoundary.OverlapPoint(playerPos))
        {
            if (!isEggRoomMode)
            {
                // 로비 -> 알방 진입
                isEggRoomMode = true;
                Debug.Log($"[LobbyEntryPoint] 알방 진입 감지: 해상도 {eggRoomResolution}으로 변경");
                SetCameraResolution(eggRoomResolution);
                UpdateCameraBoundary(nurtureReturnBoundary);
            }
        }
        // 2. 로비 구역 체크
        else if (mapBoundary != null && mapBoundary.OverlapPoint(playerPos))
        {
            if (isEggRoomMode)
            {
                // 알방 -> 로비 진입
                isEggRoomMode = false;
                Debug.Log($"[LobbyEntryPoint] 로비 진입 감지: 해상도 {lobbyResolution}으로 변경");
                SetCameraResolution(lobbyResolution);
                UpdateCameraBoundary(mapBoundary);
            }
        }
    }

    private void UpdateCameraBoundary(BoxCollider2D newBoundary)
    {
        if (lobbyCamera != null)
        {
            var camScript = lobbyCamera.GetComponent<LobbyCamera>();
            if (camScript != null)
            {
                camScript.mapBoundary = newBoundary;
            }
        }

        // 플레이어 이동 제한도 업데이트
        if (currentCharacter != null)
        {
            var playerController = currentCharacter.GetComponent<LobbyPlayerController>();
            if (playerController != null)
            {
                playerController.SetMapBoundary(newBoundary);
            }
        }
    }

    private void OnDisable()
    {
        // 로비 종료 시 Pixel Perfect Camera 해상도 복구 (기본값)
        SetCameraResolution(defaultResolution);
    }

    /// <summary>
    /// Pixel Perfect Camera 해상도를 지정된 값으로 변경합니다.
    /// </summary>
    private void SetCameraResolution(Vector2Int targetResolution)
    {
        // Start에서 찾은 lobbyCamera를 우선 사용하거나, 없으면 다시 찾기
        if (lobbyCamera == null) lobbyCamera = Camera.main;

        if (lobbyCamera == null)
        {
            Debug.LogError("[LobbyEntryPoint] 메인 카메라를 찾을 수 없습니다! Tag가 MainCamera인지, 혹은 Inspector에 할당되었는지 확인하세요.");
            return;
        }

        int targetX = targetResolution.x;
        int targetY = targetResolution.y;
        bool found = false;

        // 1. URP Pixel Perfect Camera 확인 (UnityEngine.Rendering.Universal)
        var urpCam = lobbyCamera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        if (urpCam != null)
        {
            if (urpCam.refResolutionX != targetX || urpCam.refResolutionY != targetY)
            {
                urpCam.refResolutionX = targetX;
                urpCam.refResolutionY = targetY;
                Debug.Log($"[LobbyEntryPoint] URP Pixel Perfect Camera 해상도 변경: {targetX}x{targetY}");
            }
            found = true;
        }

        // 2. U2D Pixel Perfect Camera 확인 (UnityEngine.U2D)
        if (!found)
        {
            var u2dCam = lobbyCamera.GetComponent<UnityEngine.U2D.PixelPerfectCamera>();
            if (u2dCam != null)
            {
                if (u2dCam.refResolutionX != targetX || u2dCam.refResolutionY != targetY)
                {
                    u2dCam.refResolutionX = targetX;
                    u2dCam.refResolutionY = targetY;
                    Debug.Log($"[LobbyEntryPoint] U2D Pixel Perfect Camera 해상도 변경: {targetX}x{targetY}");
                }
                found = true;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[LobbyEntryPoint] {lobbyCamera.name}에서 Pixel Perfect Camera 컴포넌트를 찾을 수 없습니다. (URP/U2D 모두 확인)");
        }
    }

    private Transform ResolveSpawnPoint(SpawnContext context)
    {
        // 육성 -> 로비 복귀 시 알방 스폰을 우선 사용
        if (context == SpawnContext.FromNurtureEggRoom && nurtureReturnSpawnPoint != null)
        {
            return nurtureReturnSpawnPoint;
        }

        // 기본 스폰 포인트가 설정되어 있으면 사용
        if (spawnPoint != null)
            return spawnPoint;

        // 둘 다 비어있는 경우 null 반환 (상위에서 처리)
        return nurtureReturnSpawnPoint;
    }

    private BoxCollider2D ResolveBoundary(SpawnContext context)
    {
        if (context == SpawnContext.FromNurtureEggRoom)
        {
            if (nurtureReturnBoundary != null)
                return nurtureReturnBoundary;

            // 인스펙터 미지정 시 스폰 포인트 부모에서 자동 탐색
            var autoBoundary = GetBoundaryFromSpawn(nurtureReturnSpawnPoint);
            if (autoBoundary != null)
                return autoBoundary;
        }

        if (mapBoundary != null)
            return mapBoundary;

        return nurtureReturnBoundary;
    }

    private static BoxCollider2D GetBoundaryFromSpawn(Transform spawn)
    {
        if (spawn == null)
            return null;

        // 가장 가까운 상위에서 경계 탐색 (Egg Room 루트에 BoxCollider2D가 있다고 가정)
        return spawn.GetComponentInParent<BoxCollider2D>();
    }
}