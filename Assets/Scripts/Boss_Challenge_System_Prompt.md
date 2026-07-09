# 보스 챌린지 시스템 (Boss Challenge System) 설계 문서

## 1\. 시스템 개요

보스전의 리플레이 가치를 높이고 플레이어에게 명확한 목표와 보상을 제공하기 위한 챌린지 시스템입니다.
전투 중 발생하는 다양한 데이터를 실시간으로 추적하고, 전투 종료 시 챌린지 달성 여부를 판별하여 보상(포인트)을 지급합니다.

## 2\. 추적 데이터 (Tracking Data)

### 2.1 공통 추적 데이터 (Common)

* `전투 소요 시간 (Clear Time)`: 전투 시작부터 종료까지 걸린 총 시간 (float)
* `페이즈 진행도 (Phase Progress)`: 현재 도달한 보스의 페이즈 (int)
* `총 피격 횟수 (Total Hit Count)`: 플레이어가 보스에게 피격당한 총 횟수 (int)
* 패링 성공 횟수 (int)
* *(추가 가능)*

### 2.2 보스 개별 추적 데이터 (Specific)

* `특정 패턴 피격 횟수 (Pattern Hit Count)`: 패턴별 고유 ID를 기반으로 해당 패턴에 피격당한 횟수 (Dictionary<string, int>)
* *(추가 가능)*

## 3\. 챌린지 유형 (Challenge Types)

* `PhaseReach`: 특정 페이즈 N회 진입
* `TimeAttack`: N초 안에 전투 승리
* `HitLimit`: 전투 중 총 피격 횟수 N회 이하로 클리어
* `PatternEvade`: 특정 패턴에 피격당한 횟수 N회 이하로 클리어

## 4\. 데이터 구조 및 상태 (Data Structures)

### 4.1 챌린지 상태 (Challenge State)

* `Active`: 현재 도전 중 (기본 상태)
* `Failed`: 이번 전투 중 조건 실패 (예: 제한 시간 초과, 피격 횟 초과)
* `Completed`: 달성 완료 및 보상 수령 대기(또는 수령 완료)

### 4.2 챌린지 데이터 테이블 구조 (Table Schema)

* `ChallengeID` (string): 챌린지 고유 식별자
* `BossID` (string): 대상 보스 식별자
* `ChallengeType` (Enum): 챌린지 목표 유형
* `TargetValue` (float/int): 목표 수치
* `TargetPatternID` (string): (선택) 특정 패턴 피격 조건일 경우 대상 패턴의 ID
* `RewardPoint` (int): 달성 시 지급될 포인트

## 5\. 기술적 요구사항 및 아키텍처 (Technical Requirements)

* **이벤트 기반 처리 (Event-Driven):** 보스 패턴 피격, 페이즈 전환 등의 이벤트 발생 시 `ChallengeManager`가 이를 구독(Subscribe)하여 데이터를 업데이트해야 합니다. 보스 스크립트와 챌린지 스크립트의 결합도를 낮추는 것이 핵심입니다.
* **원자적 저장 (Atomic Save):** 챌린지 진행 여부 및 달성 결과는 보스전이 완전히 종료된 시점에만 저장되어야 합니다. 전투 중 강제 종료 시 데이터 오염을 방지해야 합니다.
* **실시간 실패 처리 (Fail-fast):** `TimeAttack`이나 `HitLimit` 같은 제한형 챌린지의 경우, 전투 중 조건을 이탈하면 즉시 상태를 `Failed`로 변경하고 더 이상 연산하지 않도록 최적화해야 합니다.

\---

### 

