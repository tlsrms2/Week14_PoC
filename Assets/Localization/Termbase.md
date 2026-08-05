# Termbase

Localization Table의 최신 StringTable asset을 기준으로 다시 작성한 용어집입니다.

- 스캔 범위: `Assets/Localization/Boss`, `Challenge`, `Skill`, `Story`, `UI`, `Weapon`
- 기준 언어: `ko-KR`
- 대상 언어: `en`, `ja-JP`, `zh-Hans`, `zh-Hant`, `ru`, `pt-BR`, `es-419`
- 작성일: 2026-08-05
- 반영 사항:
  - 러시아어, 포르투갈어(브라질), 스페인어(중남미) 로컬라이징 기준 컬럼을 추가했습니다.

## Translation Rules

- Rich Text 태그(`<color=...>`, `<sprite ...>`)와 플레이스홀더(`{0}`, `{1}`)는 번역 중 삭제하거나 순서를 바꾸지 않습니다.
- `ARGUS`, `AEGIS`, `E.R.I.S.`, `ATLAS`, `DUEL`, `MAKELA`는 모든 언어에서 지정 표기를 유지합니다.
- 보스명은 각 언어에서 음역 고유명사로 처리하고, 일반명사처럼 의미 번역하지 않습니다.
- 시스템명은 UI 제목/튜토리얼 제목에서는 각 언어의 고유 용어로 고정하고, 문장 안 동작 표현은 자연스럽게 활용합니다.
- `방문`은 이 게임의 처형/습격/임무 수행을 완곡하게 부르는 세계관 용어입니다. 단순 방문과 구분해 일관된 톤을 유지합니다.
- 러시아어는 플레이어를 지칭할 때 기본적으로 정중한 `вы` 톤을 쓰되, 이온의 독백/대사는 더 간결하고 건조하게 처리합니다.
- 브라질 포르투갈어는 `você` 기반의 자연스러운 게임 UI 톤을 사용합니다. 유럽식 표현은 피합니다.
- 중남미 스페인어는 `tú`/무인칭 명령형 중심의 중립 라틴아메리카 표현을 사용하고, 스페인 본토식 `vosotros`는 사용하지 않습니다.

## Characters / Speakers

| 한국어    | English      | 日本語       | 简体中文     | 繁體中文     | Русский           | Português (Brasil) | Español (LatAm)      | 분류          | 비고                                                                                                                                            |
| --------- | ------------ | ------------ | ------------ | ------------ | ----------------- | ------------------ | -------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| 이온      | Ion          | イオン       | 伊温         | 伊溫         | Ион               | Ion                | Ion                  | 주인공/코드명 | 코드명 문맥은 `Code name Ion` / `コードネーム：イオン` / `代号伊温` / `代號伊溫` / `Кодовое имя: Ион` / `Codinome Ion` / `Nombre en clave Ion`. |
| E.R.I.S.  | E.R.I.S.     | E.R.I.S.     | E.R.I.S.     | E.R.I.S.     | E.R.I.S.          | E.R.I.S.           | E.R.I.S.             | 시스템/화자   | 마침표 포함 표기 유지.                                                                                                                          |
| 아버지    | Father       | 父           | 父亲         | 父親         | Отец              | Pai                | Padre                | 화자/호칭     | 대사 중 친근한 호칭은 EN `Dad`, RU `папа`, PT-BR `pai`, ES-419 `papá` 가능.                                                                     |
| 살보      | Salvo        | サルヴォ     | 萨尔沃       | 薩爾沃       | Сальво            | Salvo              | Salvo                | 보스명        | 무기상.                                                                                                                                         |
| 캐넌      | Canon        | キャノン     | 卡农         | 卡農         | Кэнон             | Canon              | Canon                | 보스명        | 영문은 현재 테이블 기준 `Canon`. `cannon`으로 바꾸지 않음.                                                                                      |
| 램        | Ram          | ラム         | 拉姆         | 拉姆         | Рэм               | Ram                | Ram                  | 보스명        | 거구의 용병.                                                                                                                                    |
| 미라주    | Mirage       | ミラージュ   | 米拉玖       | 米拉玖       | Мираж             | Mirage             | Mirage               | 보스명        | 순간이동/암살 이미지의 고유명.                                                                                                                  |
| 에코      | Echo         | エコー       | 艾可         | 艾可         | Эхо               | Echo               | Echo                 | 보스명        | 해커/방문자 문맥.                                                                                                                               |
| 훈련용 봇 | Training Bot | 訓練用ボット | 训练用机器人 | 訓練用機器人 | тренировочный бот | bot de treinamento | bot de entrenamiento | NPC/대상      | 튜토리얼 더미와 구분.                                                                                                                           |
| 방문자    | Visitor      | 訪問者       | 访问者       | 訪問者       | Визитер           | Visitante          | Visitante            | 세계관 호칭   | 임무 수행자/암살자에 가까운 완곡한 명칭.                                                                                                        |

## Places / Organizations

| 한국어           | English                | 日本語             | 简体中文       | 繁體中文       | Русский                 | Português (Brasil)        | Español (LatAm)         | 분류      | 비고                                                  |
| ---------------- | ---------------------- | ------------------ | -------------- | -------------- | ----------------------- | ------------------------- | ----------------------- | --------- | ----------------------------------------------------- |
| 신디케이트       | Syndicate              | シンジケート       | 辛迪加         | 辛迪加         | Синдикат                | Sindicato                 | Sindicato               | 조직/빌런 | 빌런 역할의 중심 조직. 다른 인물명으로 치환하지 않음. |
| 신디케이트 본부  | Syndicate headquarters | シンジケート本部   | 辛迪加总部     | 辛迪加總部     | штаб-квартира Синдиката | sede do Sindicato         | sede del Sindicato      | 장소      | Act3 목표 위치.                                       |
| R-2 블록         | R-2 Block              | R-2ブロック        | R-2街区        | R-2街區        | блок R-2                | Bloco R-2                 | Bloque R-2              | 장소      | 구역명은 영문 코드 유지.                              |
| M-8 블록         | M-8 Block              | M-8ブロック        | M-8街区        | M-8街區        | блок M-8                | Bloco M-8                 | Bloque M-8              | 장소      | 구역명은 영문 코드 유지.                              |
| M-8 암시장       | M-8 Black Market       | M-8闇市場          | M-8黑市        | M-8黑市        | черный рынок M-8        | Mercado Negro M-8         | Mercado Negro M-8       | 장소      | 보스 위치.                                            |
| 카페 베스퍼      | Cafe Vesper            | カフェ・ヴェスパー | 维斯珀咖啡馆   | 維斯珀咖啡館   | кафе Vesper             | Café Vesper               | Café Vesper             | 장소      | 첫 언급은 전체명, 이후 `Vesper` 가능.                 |
| 베스퍼           | Vesper                 | ヴェスパー         | 维斯珀         | 維斯珀         | Vesper                  | Vesper                    | Vesper                  | 장소 약칭 | Cafe Vesper의 약칭.                                   |
| 메켈레 호텔 옥상 | MAKELA Hotel Rooftop   | MAKELAホテル屋上   | MAKELA酒店屋顶 | MAKELA飯店屋頂 | крыша отеля MAKELA      | cobertura do Hotel MAKELA | azotea del Hotel MAKELA | 장소      | `MAKELA` 대문자 유지.                                 |
| 아틀라스 주차장  | ATLAS Parking Deck     | ATLAS駐車場        | ATLAS停车楼    | ATLAS停車樓    | парковка ATLAS          | estacionamento ATLAS      | estacionamiento ATLAS   | 장소      | `ATLAS` 대문자 유지.                                  |
| V-4 공터         | V-4 Vacant Lot         | V-4空き地          | V-4空地        | V-4空地        | пустырь V-4             | terreno baldio V-4        | lote baldío V-4         | 장소      | 보스 위치.                                            |
| 듀얼 타워        | DUEL Tower             | DUELタワー         | DUEL塔         | DUEL塔         | башня DUEL              | Torre DUEL                | Torre DUEL              | 장소      | `DUEL` 대문자 유지.                                   |

## Cybernetic / Lore Systems

| 한국어                | English                       | 日本語                    | 简体中文      | 繁體中文      | Русский                             | Português (Brasil)                      | Español (LatAm)                       | 분류        | 비고                                             |
| --------------------- | ----------------------------- | ------------------------- | ------------- | ------------- | ----------------------------------- | --------------------------------------- | ------------------------------------- | ----------- | ------------------------------------------------ |
| ARGUS 전술 의안       | ARGUS tactical prosthetic eye | ARGUS戦術義眼             | ARGUS战术义眼 | ARGUS戰術義眼 | тактический глазной протез ARGUS    | olho protético tático ARGUS             | ojo protésico táctico ARGUS           | 장비명      | 짧은 문맥에서는 `ARGUS eye` / `глаз ARGUS` 가능. |
| ARGUS 안구            | ARGUS eye                     | ARGUS義眼                 | ARGUS义眼     | ARGUS義眼     | глаз ARGUS                          | olho ARGUS                              | ojo ARGUS                             | 장비 약칭   | 전술 의안의 약칭.                                |
| AEGIS 코어 인터페이스 | AEGIS core interface          | AEGISコアインターフェース | AEGIS核心接口 | AEGIS核心介面 | интерфейс ядра AEGIS                | interface de núcleo AEGIS               | interfaz de núcleo AEGIS              | 시스템명    | 신경계 이식 시스템.                              |
| AEGIS 시스템          | AEGIS system                  | AEGISシステム             | AEGIS系统     | AEGIS系統     | система AEGIS                       | sistema AEGIS                           | sistema AEGIS                         | 시스템명    | 방어/피격 처리 시스템.                           |
| AEGIS 인터페이스      | AEGIS interface               | AEGISインターフェース     | AEGIS接口     | AEGIS介面     | интерфейс AEGIS                     | interface AEGIS                         | interfaz AEGIS                        | 시스템 약칭 | AEGIS 코어 인터페이스와 같은 계열.               |
| 의수                  | prosthetic arm                | 義手                      | 义手          | 義手          | протез руки                         | braço protético                         | brazo protésico                       | 장비        | 우측 의수는 `right prosthetic arm`.              |
| 우측 의수             | right prosthetic arm          | 右義手                    | 右义手        | 右義手        | правый протез руки                  | braço protético direito                 | brazo protésico derecho               | 장비        | 신체 손상/장비 설명에서 사용.                    |
| 신경계                | nervous system                | 神経系                    | 神经系统      | 神經系統      | нервная система                     | sistema nervoso                         | sistema nervioso                      | 신체/세계관 | 이식/손상 문맥.                                  |
| 운동 제어계           | motion control system         | 運動制御系                | 运动控制系统  | 運動制御系統  | система управления движением        | sistema de controle motor               | sistema de control motor              | 장비 시스템 | 의수 제어계.                                     |
| 적응 훈련             | adaptation training           | 適応訓練                  | 适应训练      | 適應訓練      | адаптационная тренировка            | treinamento de adaptação                | entrenamiento de adaptación           | 훈련        | 튜토리얼 핵심 용어.                              |
| 장비 적응 훈련        | equipment adaptation training | 装備適応訓練              | 装备适应训练  | 裝備適應訓練  | тренировка адаптации к оборудованию | treinamento de adaptação ao equipamento | entrenamiento de adaptación al equipo | 훈련        | 이식 장비 튜토리얼.                              |
| 모의 전투             | mock battle                   | 模擬戦闘                  | 模拟战斗      | 模擬戰鬥      | учебный бой                         | combate simulado                        | combate simulado                      | 훈련        | 더미 대상 전투.                                  |

## Combat / Gameplay

| 한국어             | English                  | 日本語               | 简体中文       | 繁體中文       | Русский                 | Português (Brasil)           | Español (LatAm)              | 분류           | 비고                                             |
| ------------------ | ------------------------ | -------------------- | -------------- | -------------- | ----------------------- | ---------------------------- | ---------------------------- | -------------- | ------------------------------------------------ |
| 탄환형 에너지 코어 | bullet-type energy core  | 弾丸型エネルギーコア | 子弹型能量核心 | 子彈型能量核心 | пулевой энергокристалл  | núcleo de energia balístico  | núcleo de energía balístico  | 전투 자원      | 핵심 자원명. 처음부터 끝까지 동일 표기.          |
| 에너지 코어        | energy core              | エネルギーコア       | 能量核心       | 能量核心       | энергокристалл          | núcleo de energia            | núcleo de energía            | 전투 자원      | 문맥상 탄환형이 아니어도 쓰는 상위 개념.         |
| 파란색 에너지 코어 | blue energy core         | 青いエネルギーコア   | 蓝色能量核心   | 藍色能量核心   | синий энергокристалл    | núcleo de energia azul       | núcleo de energía azul       | 전투 자원      | 카운터 가능한 취약 코어.                         |
| 코어               | core                     | コア                 | 核心           | 核心           | кристалл                | núcleo                       | núcleo                       | 전투 자원 약칭 | 에너지 코어가 명확할 때만 사용.                  |
| 탄창               | magazine                 | マガジン             | 弹匣           | 彈匣           | магазин                 | carregador                   | cargador                     | UI/전투        | 코어 아이콘과 스킬 게이지 표시 영역.             |
| 투사체             | projectile               | 投射物               | 投射物         | 投射物         | снаряд                  | projétil                     | proyectil                    | 전투 요소      | 챌린지/튜토리얼 공통 표기.                       |
| 유도형 투사체      | Homing Projectile        | 追尾型投射物         | 追踪型投射物   | 追蹤型投射物   | самонаводящийся снаряд  | projétil teleguiado          | proyectil teledirigido       | 투사체 타입    | 챌린지 목표.                                     |
| 음표형 투사체      | Note Projectile          | 音符型投射物         | 音符型投射物   | 音符型投射物   | снаряд-нота             | projétil musical             | proyectil musical            | 투사체 타입    | 챌린지 목표.                                     |
| 폭발형 투사체      | Explosive Projectile     | 爆発型投射物         | 爆炸型投射物   | 爆炸型投射物   | взрывной снаряд         | projétil explosivo           | proyectil explosivo          | 투사체 타입    | 챌린지 목표.                                     |
| 와이어형 투사체    | Wire Projectile          | ワイヤー型投射物     | 线缆型投射物   | 線纜型投射物   | тросовый снаряд         | projétil de cabo             | proyectil de cable           | 투사체 타입    | 챌린지 목표.                                     |
| 요격               | Interception / intercept | 迎撃                 | 拦截           | 攔截           | перехват                | interceptação / interceptar  | intercepción / interceptar   | 전투 액션      | 제목은 명사형, 문장에서는 동사형 사용.           |
| 카운터             | Counter                  | カウンター           | 反击           | 反擊           | контратака              | contra-ataque                | contraataque                 | 전투 액션      | 시스템명으로 취급.                               |
| 패링               | Parry                    | パリィ               | 招架           | 招架           | парирование             | aparar                       | bloqueo perfecto             | 전투 액션/스킬 | `패링 모듈`과 일반 동작 문맥 구분.               |
| 과부하             | Overload                 | オーバーロード       | 过载           | 過載           | перегрузка              | sobrecarga                   | sobrecarga                   | 전투 상태      | 제목/시스템명은 대문자 또는 해당 언어 고정 표기. |
| 자동 배출          | automatic discharge      | 自動排出             | 自动排出       | 自動排出       | автоматический сброс    | ejeção automática            | expulsión automática         | 전투 상태      | 과부하된 코어 배출.                              |
| 포착 범위          | capture range            | 捕捉範囲             | 捕捉范围       | 捕捉範圍       | радиус захвата          | alcance de captura           | alcance de captura           | 전투 수치      | ARGUS 감지 범위.                                 |
| 취약한 에너지 코어 | vulnerable energy core   | 脆弱なエネルギーコア | 脆弱能量核心   | 脆弱能量核心   | уязвимый энергокристалл | núcleo de energia vulnerável | núcleo de energía vulnerable | 전투 요소      | 카운터 가능한 보스 코어.                         |
| 액티브 스킬        | Active Skill             | アクティブスキル     | 主动技能       | 主動技能       | активный навык          | habilidade ativa             | habilidad activa             | 스킬 시스템    | UI 분류/튜토리얼 제목.                           |
| 패시브             | Passive                  | パッシブ             | 被动           | 被動           | пассивный               | passiva                      | pasiva                       | 스킬 분류      | UI 분류.                                         |
| 쿨타임             | cooldown                 | クールタイム         | 冷却时间       | 冷卻時間       | перезарядка             | recarga                      | recarga                      | 스킬 수치      | 문장 중에는 일반명사.                            |
| 스킬 게이지        | skill gauge              | スキルゲージ         | 技能计量槽     | 技能計量槽     | шкала навыка            | medidor de habilidade        | indicador de habilidad       | UI/전투        | 탄창 테두리 게이지.                              |
| 강화 포인트        | Enhancement Points       | 強化ポイント         | 强化点数       | 強化點數       | очки усиления           | pontos de aprimoramento      | puntos de mejora             | 재화           | UI 수치 표기.                                    |
| 전투 데이터 칩     | battle data chips        | 戦闘データチップ     | 战斗数据芯片   | 戰鬥資料晶片   | боевые чипы данных      | chips de dados de combate    | chips de datos de combate    | 재화/진행      | 모듈 확장에 쓰이는 자원.                         |
| 데이터 칩          | data chips               | データチップ         | 数据芯片       | 資料晶片       | чипы данных             | chips de dados               | chips de datos               | 재화 약칭      | 전투 데이터 칩의 약칭.                           |

## Modules / Skills

| 한국어             | English                           | 日本語                   | 简体中文       | 繁體中文       | Русский                          | Português (Brasil)                | Español (LatAm)                   | 분류        | 비고                                      |
| ------------------ | --------------------------------- | ------------------------ | -------------- | -------------- | -------------------------------- | --------------------------------- | --------------------------------- | ----------- | ----------------------------------------- |
| 모듈               | module                            | モジュール               | 模块           | 模組           | модуль                           | módulo                            | módulo                            | 성장 요소   | 장비 확장 단위.                           |
| 모듈 확장          | module expansion                  | モジュール拡張           | 模块扩展       | 模組擴充       | расширение модулей               | expansão de módulos               | expansión de módulos              | 성장 시스템 | 데이터 칩을 소모하는 성장 기능.           |
| 전술 패널          | tactical panel                    | 戦術パネル               | 战术面板       | 戰術面板       | тактическая панель               | painel tático                     | panel táctico                     | UI/세계관   | 목표 확인/이동 패널.                      |
| 모듈 패널          | module panel                      | モジュールパネル         | 模块面板       | 模組面板       | панель модулей                   | painel de módulos                 | panel de módulos                  | UI/세계관   | 모듈 장착/확장 패널.                      |
| 전술 회피 모듈     | Tactical Dodge Module             | 戦術回避モジュール       | 战术闪避模块   | 戰術閃避模組   | модуль тактического уклонения    | módulo de esquiva tática          | módulo de evasión táctica         | 액티브 모듈 | 튜토리얼 스킬명은 `Tactical Dodge skill`. |
| 홀로그램 복제 모듈 | Hologram Duplicate Module         | ホログラム複製モジュール | 全息复制模块   | 全息複製模組   | модуль голографического двойника | módulo de duplicação holográfica  | módulo de duplicado holográfico   | 액티브 모듈 | 홀로그램을 남기는 스킬.                   |
| 시간 조작 모듈     | Time Manipulation Module          | 時間操作モジュール       | 时间操纵模块   | 時間操縱模組   | модуль управления временем       | módulo de manipulação temporal    | módulo de manipulación temporal   | 액티브 모듈 | 시간 둔화.                                |
| 자성 모듈          | Magnetic Module                   | 磁力モジュール           | 磁力模块       | 磁力模組       | магнитный модуль                 | módulo magnético                  | módulo magnético                  | 액티브 모듈 | 투사체 흡수/탄환 회복.                    |
| 패링 모듈          | Parry Module                      | パリィモジュール         | 招架模块       | 招架模組       | модуль парирования               | módulo de aparada                 | módulo de bloqueo perfecto        | 액티브 모듈 | 짧은 무적/탄환 회복.                      |
| 자동 추적 모듈     | Auto-Tracking Module              | 自動追尾モジュール       | 自动追踪模块   | 自動追蹤模組   | модуль автонаведения             | módulo de rastreamento automático | módulo de seguimiento automático  | 패시브 모듈 | 하이픈 유지.                              |
| 고성능 요격 모듈   | High-Performance Intercept Module | 高性能迎撃モジュール     | 高性能拦截模块 | 高性能攔截模組 | модуль усиленного перехвата      | módulo de interceptação avançada  | módulo de intercepción avanzada   | 패시브 모듈 | 요격 범위 증가.                           |
| 가속 모듈          | Acceleration Module               | 加速モジュール           | 加速模块       | 加速模組       | модуль ускорения                 | módulo de aceleração              | módulo de aceleración             | 패시브 모듈 | 탄환 없음 상태의 이동 속도 증가.          |
| 에너지 전환 모듈   | Energy Conversion Module          | エネルギー変換モジュール | 能量转换模块   | 能量轉換模組   | модуль преобразования энергии    | módulo de conversão de energia    | módulo de conversión de energía   | 패시브 모듈 | 요격 성공 시 쿨타임 감소.                 |
| 비상 프로토콜 모듈 | Emergency Protocol Module         | 緊急プロトコルモジュール | 紧急协议模块   | 緊急協議模組   | модуль аварийного протокола      | módulo de protocolo de emergência | módulo de protocolo de emergencia | 패시브 모듈 | 치명 피해 1회 생존.                       |
| 홀로그램           | hologram                          | ホログラム               | 全息影像       | 全息影像       | голограмма                       | holograma                         | holograma                         | 스킬 효과   | 홀로그램 복제 모듈 효과.                  |

## Weapons

| 한국어         | English              | 日本語             | 简体中文    | 繁體中文    | Русский              | Português (Brasil)  | Español (LatAm)        | 분류      | 비고                                                  |
| -------------- | -------------------- | ------------------ | ----------- | ----------- | -------------------- | ------------------- | ---------------------- | --------- | ----------------------------------------------------- |
| 권총           | Pistol               | ハンドガン         | 手枪        | 手槍        | пистолет             | pistola             | pistola                | 무기      | 무기명.                                               |
| 산탄총         | Shotgun              | ショットガン       | 霰弹枪      | 霰彈槍      | дробовик             | escopeta            | escopeta               | 무기      | 무기명.                                               |
| 저격총         | Sniper Rifle         | スナイパーライフル | 狙击步枪    | 狙擊步槍    | снайперская винтовка | rifle de precisão   | rifle de francotirador | 무기      | 영어는 `Sniper Rifle`로 고정.                         |
| 레일건         | Railgun              | レールガン         | 轨道炮      | 軌道炮      | рельсотрон           | canhão elétrico     | cañón de riel          | 무기      | 무기명.                                               |
| 야구 배트      | Baseball Bat         | 野球バット         | 棒球棒      | 棒球棒      | бейсбольная бита     | taco de beisebol    | bate de béisbol        | 무기      | 무기명.                                               |
| 탄환           | ammo / bullet        | 弾 / 弾薬          | 子弹 / 弹药 | 子彈 / 彈藥 | боезапас / пуля      | munição / bala      | munición / bala        | 전투 자원 | 보유량은 ammo/munição/munición, 발사체는 bullet/bala. |
| 최대 탄환 개수 | Max Ammo             | 最大弾数           | 最大弹药量  | 最大彈藥量  | макс. боезапас       | munição máxima      | munición máxima        | 무기 수치 | UI 라벨.                                              |
| 탄환 피해량    | Bullet Damage        | 弾ダメージ         | 子弹伤害    | 子彈傷害    | урон пули            | dano da bala        | daño de bala           | 무기 수치 | UI 라벨.                                              |
| 충전 공격      | charged attack       | チャージ攻撃       | 蓄力攻击    | 蓄力攻擊    | заряженная атака     | ataque carregado    | ataque cargado         | 무기 액션 | 저격총 설명.                                          |
| 반사           | reflect / reflection | 反射               | 反射        | 反射        | отражение            | refletir / reflexão | reflejar / reflejo     | 무기 액션 | 야구 배트 설명.                                       |

## Story / Mission Terms

| 한국어                             | English                                         | 日本語                               | 简体中文                   | 繁體中文                   | Русский                          | Português (Brasil)                                  | Español (LatAm)                                | 분류             | 비고                                                                         |
| ---------------------------------- | ----------------------------------------------- | ------------------------------------ | -------------------------- | -------------------------- | -------------------------------- | --------------------------------------------------- | ---------------------------------------------- | ---------------- | ---------------------------------------------------------------------------- |
| 방문                               | visit                                           | 訪問                                 | 访问                       | 訪問                       | визит                            | visita                                              | visita                                         | 세계관 용어      | 암살/습격/임무 수행을 완곡하게 표현. 강조 태그가 붙는 경우가 많음.           |
| 방문 대상                          | target of the visit                             | 訪問対象                             | 访问目标                   | 訪問目標                   | цель визита                      | alvo da visita                                      | objetivo de la visita                          | 임무 대상        | 로비/스토리 목표.                                                            |
| 공식 임무                          | official mission                                | 公式任務                             | 正式任务                   | 正式任務                   | официальное задание              | missão oficial                                      | misión oficial                                 | 임무             | 비공식 임무와 대비.                                                          |
| 비공식 임무                        | unofficial mission                              | 非公式任務                           | 非正式任务                 | 非正式任務                 | неофициальное задание            | missão não oficial                                  | misión no oficial                              | 임무             | 추적 제한 문맥.                                                              |
| 제거 대상                          | elimination target                              | 排除対象                             | 清除目标                   | 清除目標                   | цель устранения                  | alvo de eliminação                                  | objetivo de eliminación                        | 임무 대상        | 스토리 조사 문맥.                                                            |
| 사건과 연관된 인물                 | individuals connected to the incident           | 事件関係者                           | 与事件相关的人物           | 與事件相關的人物           | лица, связанные с инцидентом     | indivíduos ligados ao incidente                     | personas vinculadas al incidente               | 조사 대상        | 신디케이트 관련자 포함.                                                      |
| 운반책                             | courier                                         | 運び屋                               | 运送人                     | 運送人                     | курьер                           | mensageiro                                          | mensajero                                      | 인물 역할        | 폭발물 배송자.                                                               |
| 회계담당자                         | accountant                                      | 会計担当者                           | 会计负责人                 | 會計負責人                 | бухгалтер                        | contador                                            | contador                                       | 인물 역할        | 신디케이트 자금 관리 계열.                                                   |
| 고위급 간부                        | high-ranking executive                          | 上級幹部                             | 高级干部                   | 高階幹部                   | высокопоставленный руководитель  | executivo de alto escalão                           | ejecutivo de alto rango                        | 조직 역할        | 신디케이트 간부 문맥.                                                        |
| 의뢰인                             | client                                          | 依頼主                               | 委托人                     | 委託人                     | заказчик                         | contratante                                         | cliente                                        | 임무 주체        | Act3에서는 신디케이트.                                                       |
| 다음 표적의 명단을 전송해드릴까요? | Should I transmit the list of the next targets? | 次の標的のリストを送信しましょうか？ | 要发送下一个目标的名单吗？ | 要傳送下一個目標的名單嗎？ | Передать список следующих целей? | Deseja que eu transmita a lista dos próximos alvos? | ¿Transmito la lista de los próximos objetivos? | 반복 대사        | 한국어는 `<color=#ff4c5c>다음 표적의 명단을 전송해드릴까요?</color>`로 통일. |
| 명단                               | list                                            | リスト                               | 名单                       | 名單                       | список                           | lista                                               | lista                                          | 스토리 핵심 물건 | 전송/삭제 문맥.                                                              |
| 자료                               | data                                            | データ                               | 数据                       | 資料                       | данные                           | dados                                               | datos                                          | 조사 정보        | 특정 인물 자료가 아니라 신디케이트/사건 자료로 처리.                         |
| 기록 매체                          | recording media                                 | 記録媒体                             | 记录介质                   | 記錄媒體                   | носитель записи                  | mídia de gravação                                   | medio de grabación                             | 세계관 물건      | 베스퍼 기록 재생 문맥.                                                       |
| 재구성된 기록                      | reconstructed recording                         | 再構成された記録                     | 重建记录                   | 重建記錄                   | восстановленная запись           | gravação reconstruída                               | grabación reconstruida                         | 세계관 물건      | 손상 기록 복원.                                                              |
| 폭발물                             | explosive                                       | 爆発物                               | 爆炸物                     | 爆炸物                     | взрывчатка                       | explosivo                                           | explosivo                                      | 사건 요소        | 베스퍼 사건.                                                                 |
| 가스 사고                          | gas accident                                    | ガス事故                             | 燃气事故                   | 瓦斯事故                   | авария с газом                   | acidente com gás                                    | accidente de gas                               | 위장 사건        | 폭발 위장 계획.                                                              |
| 개인적인 복수                      | personal revenge                                | 個人的な復讐                         | 个人复仇                   | 個人復仇                   | личная месть                     | vingança pessoal                                    | venganza personal                              | 동기             | 이온의 동기.                                                                 |
| 일관된 동기                        | consistent motivation                           | 一貫した動機                         | 一贯的动机                 | 一貫的動機                 | последовательный мотив           | motivação consistente                               | motivación constante                           | 동기             | E.R.I.S.의 분석 표현.                                                        |
| 이윤에 따라 움직이는 이들          | those who move according to profit              | 利益で動く者たち                     | 逐利而动的人               | 逐利而動的人               | те, кто движим выгодой           | aqueles que agem por lucro                          | quienes se mueven por beneficio                | 세계관 표현      | 신디케이트/의뢰 사회 비판 문맥.                                              |

## Challenge / UI

| 한국어    | English        | 日本語     | 简体中文 | 繁體中文 | Русский            | Português (Brasil) | Español (LatAm) | 분류        | 비고                                          |
| --------- | -------------- | ---------- | -------- | -------- | ------------------ | ------------------ | --------------- | ----------- | --------------------------------------------- |
| 처형      | Execute        | 処刑       | 处决     | 處決     | казнить            | executar           | ejecutar        | 보스 UI     | Boss table의 실행/처형 텍스트.                |
| 챌린지    | Challenge      | チャレンジ | 挑战     | 挑戰     | испытание          | desafio            | desafío         | UI/모드     | 보스 선택 탭.                                 |
| 전투      | Battle         | 戦闘       | 战斗     | 戰鬥     | бой                | combate            | combate         | UI/모드     | 로비/보스 선택.                               |
| 강화      | Enhancement    | 強化       | 强化     | 強化     | усиление           | aprimoramento      | mejora          | UI/성장     | 로드아웃/로비.                                |
| 로비      | Lobby          | ロビー     | 大厅     | 大廳     | лобби              | lobby              | lobby           | UI/장소     | 게임 허브.                                    |
| 목표      | Objective      | 目標       | 目标     | 目標     | цель               | objetivo           | objetivo        | UI          | 튜토리얼 목표 라벨.                           |
| 누적      | Cumulative     | 累計       | 累计     | 累計     | суммарно           | acumulado          | acumulado       | 챌린지 조건 | `[Cumulative]` 접두사.                        |
| 페이즈    | Phase          | フェーズ   | 阶段     | 階段     | фаза               | fase               | fase            | 보스 단계   | `Second Phase`, `Third Phase`.                |
| 환불 불가 | Non Refundable | 返金不可   | 不可退款 | 不可退款 | возврат невозможен | sem reembolso      | no reembolsable | UI          | 스타일 통일 시 EN `Non-refundable` 검토 가능. |
| 구매      | Purchase       | 購入       | 购买     | 購買     | купить             | comprar            | comprar         | UI 액션     | 로드아웃.                                     |
| 장착      | Equip          | 装備       | 装备     | 裝備     | экипировать        | equipar            | equipar         | UI 액션     | 로드아웃.                                     |
| 환불      | Refund         | 返金       | 退款     | 退款     | вернуть            | reembolsar         | reembolsar      | UI 액션     | 로드아웃.                                     |

## Source Notes

- 러시아어, 포르투갈어(브라질), 스페인어(중남미)는 이후 StringTable 추가 시 본 용어집 표기를 우선 기준으로 사용합니다.
