#!/usr/bin/env python3
"""
FastAPI FaceMesh 서버
MediaPipe를 사용하여 얼굴 랜드마크를 검출하고 JSON으로 반환
"""

import cv2
import mediapipe as mp
import numpy as np
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
import uvicorn
from typing import List, Dict, Any
import logging

# 로깅 설정
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# FastAPI 앱 생성
app = FastAPI(
    title="FaceMesh Detection Server",
    description="MediaPipe를 사용한 얼굴 랜드마크 검출 API",
    version="1.0.0"
)

# MediaPipe 초기화
mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(
    static_image_mode=True,
    max_num_faces=5,  # 최대 5명까지
    refine_landmarks=True,
    min_detection_confidence=0.5
)

def find_noses(img: np.ndarray) -> List[Dict[str, int]]:
    """
    이미지에서 코 좌표를 찾는 함수
    
    Args:
        img: OpenCV 이미지 (BGR)
        
    Returns:
        코 좌표 리스트 [{"x": int, "y": int}, ...]
    """
    try:
        # BGR을 RGB로 변환
        img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        result = face_mesh.process(img_rgb)
        
        if not result.multi_face_landmarks:
            return []

        h, w, _ = img.shape
        noses = []
        
        for face_landmarks in result.multi_face_landmarks:
            # MediaPipe에서 코끝은 landmark index 1
            nose_tip = face_landmarks.landmark[1]
            noses.append({
                "x": int(nose_tip.x * w),
                "y": int(nose_tip.y * h)
            })
            
        logger.info(f"검출된 얼굴 수: {len(noses)}")
        return noses
        
    except Exception as e:
        logger.error(f"얼굴 검출 중 오류: {e}")
        return []

@app.get("/")
async def root():
    """헬스 체크 엔드포인트"""
    return {"status": "running", "message": "FaceMesh Detection Server"}

@app.post("/detect-noses")
async def detect_noses(file: UploadFile = File(...)) -> JSONResponse:
    """
    업로드된 이미지에서 코 좌표를 검출
    
    Args:
        file: 업로드된 이미지 파일
        
    Returns:
        JSON 응답: {"noses": [{"x": int, "y": int}, ...]}
    """
    try:
        # 파일 타입 검증
        if not file.content_type.startswith('image/'):
            raise HTTPException(
                status_code=400, 
                detail="이미지 파일만 업로드 가능합니다."
            )
        
        # 파일 읽기
        contents = await file.read()
        if len(contents) == 0:
            raise HTTPException(
                status_code=400,
                detail="빈 파일입니다."
            )
        
        # numpy 배열로 변환
        nparr = np.frombuffer(contents, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        if img is None:
            raise HTTPException(
                status_code=400,
                detail="이미지 디코딩에 실패했습니다."
            )
        
        # 얼굴 검출
        noses = find_noses(img)
        
        logger.info(f"파일: {file.filename}, 검출된 코: {len(noses)}개")
        
        return JSONResponse(
            content={
                "success": True,
                "filename": file.filename,
                "noses": noses,
                "count": len(noses)
            }
        )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"이미지 처리 중 오류: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"서버 내부 오류: {str(e)}"
        )

@app.get("/shutdown")
async def shutdown():
    """서버 종료 엔드포인트 (개발용)"""
    logger.info("서버 종료 요청됨")
    return {"message": "서버가 종료됩니다."}

if __name__ == "__main__":
    logger.info("🚀 FaceMesh Detection Server 시작")
    uvicorn.run(
        app,
        host="127.0.0.1",
        port=9320,
        log_level="info"
    )