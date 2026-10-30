# 촬영 Domain 테스트 인벤토리

`BoothSessionTests.cs`의 9개 메서드/11개 실행 사례. 최종 Release 실행: 통과 11, 실패 0, 건너뜀 0. 상태 전이와 확정 슬롯 수는 촬영 계약이며 기본값/배선 테스트가 아닙니다.

| 테스트 메서드 | 분류 | 소비자 동작 |
| --- | --- | --- |
| `CancelledCountdownDoesNotConfirmShotAndCanResume` | Boundary | 취소 시 확정하지 않고 같은 슬롯에서 재개 |
| `FourthConfirmationEntersReviewRatherThanAutoSaving` | Happy | 4장 뒤 자동 저장 없이 검토 진입 |
| `FinalShotRetakeReplacesOnlyFinalSlot` | Happy | 마지막 사진 교체 후 4장을 유지하고 저장 가능 |
| `CancelledRetakePreservesCompleteReview` | Boundary | 재촬영 취소 후 원래 검토 유지 |
| `UnsuccessfulSaveRetainsAllShotsAndAllowsRetry` | Error | 취소/실패 2사례에서 사진 유지·재시도 |
| `ResetClearsSavedOrPartialSessionAndRestoresFrameChoice` | Happy | 저장/부분 세션 초기화 후 다음 사용자 준비 |
| `CancellingThirdShotKeepsFirstTwoAndResumesAtThird` | Boundary | 부분 확정 촬영을 취소해도 앞 두 장 유지 |
| `SavingAndRetakingCannotBypassFourConfirmedPhotos` | Error | 준비되지 않은 상태에서 저장·재촬영 거부 |
| `RetakeRejectsOutOfRangeSlotsWithoutLosingReview` | Error | -1/4 슬롯 2사례 거부 후 검토 유지 |

실제 카운트다운 시간·WPF 명령·카메라·저장 대화상자 검증이 아닙니다. 메서드 이름은 처음 추가한 이름을 유지하며, 별도 일괄 이름 변경은 하지 않습니다.
