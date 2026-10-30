# 영상·파이프 테스트 인벤토리

32개 메서드/45개 실행 사례. 최종 Release 실행: 통과 45, 실패 0, 건너뜀 0. 실제 카메라/WPF 조작 없음. `FaceMeshPipeTests`는 실제 Windows named-pipe의 .NET 시험 서버이며, 실제 Python은 별도 PipeSmoke로 검증합니다.

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
| FaceMeshPipeTests | `Missing_server_connect_times_out` | Error |
| FaceMeshPipeTests | `Connecting_to_missing_server_can_be_cancelled` | Boundary |
| FaceMeshPipeTests | `Delayed_server_and_fragmented_response_are_supported` | Happy |
| FaceMeshPipeTests | `Truncated_response_breaks_connection` | Error (5사례) |
| FaceMeshPipeTests | `Invalid_response_length_is_rejected` | Error (3사례) |
| FaceMeshPipeTests | `No_face_is_not_an_error_but_server_error_is` | Happy / Error |
| FaceMeshPipeTests | `Timeout_invalidates_connection_and_reconnect_succeeds` | Error / 복구 |
| FaceMeshPipeTests | `Cancellation_interrupts_response_and_breaks_connection` | Boundary |
| FaceMeshPipeTests | `Disconnect_interrupts_an_inflight_request` | Boundary |
| FaceMeshPipeTests | `Concurrent_requests_are_serialized_and_keep_metadata` | Happy |
| FaceMeshPipeTests | `Mismatched_response_metadata_is_not_accepted` | Error |
| FaceMeshPipeTests | `Dispose_rejects_a_connect_already_waiting_for_an_inflight_request` | Error |
| FaceMeshPipeTests | `Concurrent_disposals_all_finish_before_future_operations_are_rejected` | Boundary |

CameraManager의 Happy hardware-start는 이 프로젝트에서 실행하지 않습니다. 실제 카메라 승인이 필요하며, 빈 성공·가짜 카메라로 통과를 만들지 않습니다. 나머지 정상·경계·오류 시나리오와 실물 미실행 범위는 `../../VALIDATION.md`를 따릅니다. 테스트 이름은 최초 추가 이름을 유지합니다.
