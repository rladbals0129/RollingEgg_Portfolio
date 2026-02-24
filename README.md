# RollingEgg - Unity 2D 인디게임 프로젝트

## 프로젝트 개요
- **장르**: 육성(알 까기) + 러닝(리듬게임+퍼즐) 도트풍 인디게임
- **개발 인원**: 6명 (기획2,아트1,사운드1,개발2) 
- **엔진**: Unity 6000.0.48f1 LTS
- **아키텍처**: Service Locator 패턴 + Event Bus 시스템

## 코딩 컨벤션

### 네임스페이스 규칙
- `RollingEgg.Core`: 핵심 시스템 (GameManager, ServiceLocator, EventBus 등)
- `RollingEgg.UI`: UI 관련 클래스들
- `RollingEgg.Gameplay`: 게임플레이 로직
- `RollingEgg.Data`: 데이터 및 설정 관련
- `RollingEgg.Audio`: 오디오 관련
- `RollingEgg.Utils`: 유틸리티 클래스들

### 네이밍 규칙
- **클래스**: PascalCase (예: `GameManager`, `UI_Base`)
- **메서드**: PascalCase (예: `InitializeAsync`, `ShowPopup`)
- **변수/필드**: camelCase (예: `_currentScene`, `isInitialized`)
- **상수**: UPPER_CASE (예: `MAX_LEVEL`, `DEFAULT_VALUE`)
- **프라이빗 필드**: 언더스코어 접두사 (예: `_privateField`)
- **인터페이스**: I 접두사 (예: `IResourceService`, `IEventBus`)

### 파일 구조
- 스크립트는 기능별로 폴더 분리
- UI는 `UI/Scene`, `UI/Popup` 하위 폴더 사용
- 각 클래스는 별도 파일로 분리
- 메타 파일은 자동 생성되므로 수정 금지

## 아키텍처 가이드라인

### Service Locator 패턴
- 모든 서비스는 `ServiceLocator`를 통해 등록/조회
- 인터페이스와 구현체 분리 필수
- 서비스 등록은 `GameManager.ServiceRegister()`에서 수행

### Event Bus 시스템
- 컴포넌트 간 통신은 EventBus 사용
- 이벤트 데이터는 struct로 정의
- 구독/해제는 적절한 생명주기에서 관리

### UI 시스템
- 모든 UI는 `UI_Base` 상속
- Scene UI는 `UI_Scene` 상속
- Popup UI는 `UI_Popup` 상속
- UI 표시/숨김은 `UIManager`를 통해 관리

### 리소스 관리
- 모든 리소스는 Addressables 사용
- `ResourceManager`를 통해 비동기 로딩
- 메모리 관리 및 언로딩 적절히 처리

## 기술 스택 및 의존성

### 필수 패키지
- **UniTask**: 비동기 처리
- **Addressables**: 리소스 관리
- **Input System**: 입력 처리
- **URP**: 2D 렌더링
- **DOTween**: 애니메이션

### 권장 패키지
- **TextMeshPro**: 텍스트 렌더링
- **2D Sprite**: 2D 그래픽스
- **Audio**: 오디오 시스템

## 개발 원칙

### 1. 코드 품질
- 단일 책임 원칙 준수
- 의존성 주입 활용
- 비동기 처리 시 UniTask 사용
- 예외 처리 및 로깅 적절히 구현

### 2. 성능 최적화
- 오브젝트 풀링 활용
- 불필요한 Update() 호출 방지
- 메모리 할당 최소화
- 2D 스프라이트 최적화

### 3. 확장성
- 모듈화된 구조 유지
- 설정 데이터는 ScriptableObject 활용
- 이벤트 기반 통신으로 결합도 낮추기

### 4. 유지보수성
- 명확한 주석 작성
- 의미있는 변수/메서드명 사용
- 코드 중복 최소화
- 일관된 코딩 스타일 유지

## 게임 시스템 가이드라인

### 육성 시스템
- 확률 기반 알 까기 시스템
- 알 경험치 시스템 + 공용 육성재화 + 전용 육성재화
- ScriptableObject로 확률 테이블 관리
- 이벤트 기반 결과 처리

### 러닝 게임플레이
- 리듬 게임 요소 (선을 따라 색상을 변경하는 시스템 Q, W , E , R , T ) 
- 퍼즐 요소 (장애물, 아이템)
- 점수 및 진행도 시스템
- Input System 활용한 입력 처리

### 데이터 관리
- JSON 기반 데이터 저장
- 클라우드 저장 지원
- 데이터 검증 및 백업

## 디버깅 및 테스트

### 로깅
- `Debug.Log()`: 일반 정보
- `Debug.LogWarning()`: 경고 메시지
- `Debug.LogError()`: 에러 메시지
- 릴리즈 빌드에서는 로깅 최소화

### 테스트
- 핵심 로직에 대한 유닛 테스트 작성
- UI 테스트 케이스 작성
- 게임플레이 시나리오 테스트

## Git 워크플로우

### 브랜치 전략
- `main`: 안정적인 릴리즈 버전
- `develop`: 개발 통합 브랜치
- `feature/*`: 기능 개발 브랜치
- `hotfix/*`: 긴급 수정 브랜치

### 커밋 메시지
- `feat:`: 새로운 기능
- `fix:`: 버그 수정
- `refactor:`: 코드 리팩토링
- `docs:`: 문서 수정
- `style:`: 코드 스타일 변경
- `test:`: 테스트 추가/수정

## 주의사항

### 금지사항
- `MonoBehaviour`의 `Update()` 남발
- 하드코딩된 값 사용
- 직접적인 컴포넌트 참조 (GetComponent 남발)
- 메모리 누수 발생 가능한 코드

### 권장사항
- 이벤트 기반 통신 사용
- 비동기 처리 적극 활용
- 설정 데이터 외부화
- 코드 재사용성 고려

## 문서화
- 각 시스템별 README 작성
- API 문서 자동 생성
- 코드 주석은 한국어로 작성
- 변경사항은 CHANGELOG에 기록

---

이 규칙들을 따라 일관성 있고 유지보수 가능한 코드를 작성하세요.
