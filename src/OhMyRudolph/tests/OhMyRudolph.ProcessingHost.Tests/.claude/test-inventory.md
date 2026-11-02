# 처리 호스트 테스트 인벤토리

9개 메서드/19개 실행 사례입니다. 실행 결과와 실제 Linux C++ 추론 증거는 `../../VALIDATION.md`에 구분합니다. 이 프로젝트는 HTTP 봉투·인증·상태 변환·소유자 lease의 순수 계약을 검사하며, 네이티브 모델이나 Kestrel을 단위 테스트의 가짜 성공으로 대체하지 않습니다.

| 파일 | 테스트 메서드 | 분류 | 소비자 동작 |
| --- | --- | --- | --- |
| ProtocolTests | `ParsesMetadataAndRetainsJpegOffset` | Happy | LE 길이 뒤의 메타데이터와 JPEG 경계 해석 |
| ProtocolTests | `RejectsInvalidMetadata` | Error (6사례) | 누락·과대 크기·잘못된 회전·타입·후행 JSON·잘못된 JSON 거부 |
| ProtocolTests | `RejectsTruncatedOversizedAndEmptyJpeg` | Error | 잘린 길이·과대 메타데이터·빈 JPEG 거부 |
| ProtocolTests | `TotalBodyLimitIsInclusive` | Boundary | 8MiB 포함 상한과 초과 거부 |
| ProtocolTests | `BearerRequiresExactValidToken` | Error / Happy 포함 | 정확한 bearer만 허용, 잘못된 비밀 설정 거부 |
| ProtocolTests | `NativeFailuresMapToHttpErrors` | Error (4사례) | C ABI 실패를 빈 얼굴 없음 성공으로 숨기지 않음 |
| ProtocolTests | `JsonPointContractUsesObjectCoordinatesAndNumericStatus` | Happy | WPF가 소비하는 객체 좌표·숫자 상태의 wire 계약 |
| OwnerLeaseTests | `MissingOwnerExpiresAtTheDeadline` | Boundary (3사례) | 29초 유지·정확히 30초와 이후 만료 |
| OwnerLeaseTests | `RenewingTheOwnerExtendsItsDeadline` | Happy | heartbeat가 마지막 요청 기준으로 만료 연장 |

OwnerLease의 잘못된 OS 시간·실제 프로세스 종료는 이 순수 시계 테스트의 범위가 아닙니다. 실제 무갱신 종료와 컨테이너 자동 정리는 별도 실행 증거로 기록합니다. 기존 테스트 명명 관례를 유지하며 전체 이름/형식을 일괄 변경하지 않습니다.
