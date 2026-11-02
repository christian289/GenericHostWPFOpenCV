# 영상·컨테이너 클라이언트 테스트 인벤토리

35개 메서드/49개 실행 사례를 등록합니다. 실제 실행 결과는 `../../VALIDATION.md`를 따릅니다. 실제 카메라/WPF 조작은 포함하지 않습니다. `ContainerHttpTests`의 HTTP 응답 시험은 루프백 .NET 시험 서버이며 실제 C++ 모델은 별도 ContainerSmoke로 검증합니다. 삭제된 Python/Named Pipe 테스트는 현재 실행 수에 합산하지 않습니다.

| 파일 | 테스트 메서드 | 분류 |
| --- | --- | --- |
| BoothCompositorTests | `CompositePlacesFourShotsInReadingOrderAndKeepsSourceOwnership` | Happy (라이트/다크 2사례) |
| BoothCompositorTests | `CompositeCenterCropsWidePhotoWithoutStretching` | Boundary |
| BoothCompositorTests | `CompositeRejectsIncompletePhotosInsteadOfExportingBlankSlots` | Error |
| CameraManagerLifetimeTests | `StopBeforeStartIsSafeAndDoesNotRequireAnyCamera` | Boundary |
| CameraManagerLifetimeTests | `DisposeBeforeStartAndRepeatedDisposeAreSafe` | Boundary |
| CameraManagerLifetimeTests | `SwitchingWithoutAnyCameraDoesNotStartHardwareDiscovery` | Error |
| FrameGenerationTests | `SwitchingCameraRejectsLateResponseFromPreviousCamera` | Error / 현재 세대 Happy 포함 |
| FrameGenerationTests | `StopThenStartSameCameraRejectsPreviousSession` | Boundary |
| FrameGenerationTests | `CameraIdentityMustMatchEvenWhenGenerationNumberMatches` | Error |
| FrameGenerationTests | `DelayedResponseCannotPublishAfterInvalidationReturns` | Error |
| LatestFrameProcessorTests | `SlowConsumerKeepsOnlyNewestWaitingFrameAndDisposesSupersededFrames` | Boundary |
| LatestFrameProcessorTests | `ConsumerNeverOverlapsEvenWhenManyFramesArriveDuringAwait` | Happy |
| LatestFrameProcessorTests | `StopCancelsCurrentFrameDisposesWaitingFrameAndRejectsFurtherOwnership` | Boundary |
| LatestFrameProcessorTests | `FailedFrameIsDisposedWithoutCompletingReusableConsumer` | Error |
| CaptureFreshnessTests | `OnlyCaptureAgesFromZeroThroughOneSecondAreEligible` | Happy / Boundary / Error (7사례) |
| CaptureFreshnessTests | `OneStopwatchTickPastOneSecondIsAlreadyExpired` | Boundary |
| CaptureFreshnessTests | `ReadFailureRejectsEvenARecentlyCapturedCurrentGenerationFrame` | Error |
| CaptureFreshnessTests | `StoppedOrSwitchedGenerationRejectsEvenAnOtherwiseFreshFrame` | Error |
| CaptureFreshnessTests | `InferenceAndQueueDelayRemainPartOfAgeEvenWhenFrameHasJustBeenPublished` | Error |
| ContainerHttpTests | `Invalid_response_is_rejected` | Error (식별·좌표 경계·상태 5사례) |
| ContainerHttpTests | `No_face_is_a_valid_result` | Boundary |
| ContainerHttpTests | `Authentication_failure_is_not_empty_success` | Error |
| ContainerHttpTests | `Native_failure_is_reported_separately_from_no_face` | Error |
| ContainerHttpTests | `Valid_points_are_returned` | Happy |
| ContainerHttpTests | `Cancelled_start_waiter_does_not_release_owned_startup_before_disconnect` | Boundary / 취소 대기와 자원 수명 분리 |
| ContainerHttpTests | `Concurrent_disconnects_join_cleanup_and_allow_restart` | Happy / 중복 종료 합류·재시작 |
| ContainerHttpTests | `Dispose_during_startup_drains_creation_and_owned_resource` | Boundary |
| ContainerHttpTests | `Cancelled_disconnect_waiter_does_not_abandon_cleanup` | Boundary |
| ContainerHttpTests | `Failed_disconnect_is_reported_and_prevents_restart` | Error |
| ContainerManifestTests | `Rejects_archive_paths_outside_manifest_directory` | Error (3사례) |
| ContainerManifestTests | `Rejects_archive_with_wrong_digest` | Error |
| ContainerManifestTests | `Accepts_verified_archive` | Happy |
| ContainerManifestTests | `Rejects_invalid_or_mismatched_package_version` | Error (2사례) |
| ContainerManifestTests | `Rejects_a_valid_but_different_version` | Error |
| ContainerManifestTests | `Session_string_does_not_expose_token` | Error / 비밀 노출 방지 |

CameraManager의 실제 hardware-start는 승인 전 실행하지 않습니다. CaptureFreshness는 시간·세션 경계, FrameGeneration은 세대 경계, CameraManagerLifetime은 미기동 수명 경계를 집중 검사하므로 각각 모든 하드웨어 정상 동작을 주장하지 않습니다. 메서드 이름은 기존 프로젝트 관례를 유지하며 일괄 이름 변경은 하지 않습니다. 단위 테스트 결과를 실제 UI·장치·설치 검증으로 계산하지 않습니다.
