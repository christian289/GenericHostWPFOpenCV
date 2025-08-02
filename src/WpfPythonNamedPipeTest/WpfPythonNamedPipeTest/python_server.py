#!/usr/bin/env python3
"""
Python Named Pipe Server for WPF Communication
pywin32를 사용한 Windows Named Pipe 서버 구현
"""

import win32pipe
import win32file
import win32api
import pywintypes
import json
import time
import logging
import sys
import os
from datetime import datetime

# 로깅 설정 - 콘솔과 파일 모두에 출력
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('python_server.log', encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

# 시작 시 로그 출력
logger.info("=== Python Named Pipe Server 시작 ===")
logger.info(f"작업 디렉토리: {os.getcwd()}")
logger.info(f"Python 버전: {sys.version}")

# pywin32 모듈 로드 확인
try:
    import win32pipe
    logger.info("win32pipe 모듈 로드 성공")
except ImportError as e:
    logger.error(f"win32pipe 모듈 로드 실패: {e}")
    sys.exit(1)

class NamedPipeServer:
    def __init__(self, pipe_name="WpfPythonPipe"):
        self.pipe_name = f"\\\\.\\pipe\\{pipe_name}"
        self.pipe_handle = None
        self.is_running = False
        
    def create_pipe(self):
        """Named Pipe 생성"""
        try:
            logger.info(f"Named Pipe 생성 시도: {self.pipe_name}")
            
            self.pipe_handle = win32pipe.CreateNamedPipe(
                self.pipe_name,
                win32pipe.PIPE_ACCESS_DUPLEX,  # 양방향 통신
                win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
                1,  # 최대 인스턴스 수
                65536,  # 출력 버퍼 크기
                65536,  # 입력 버퍼 크기
                0,  # 기본 타임아웃
                None  # 보안 속성
            )
            
            if self.pipe_handle == win32file.INVALID_HANDLE_VALUE:
                error_code = win32api.GetLastError()
                raise Exception(f"Named Pipe 생성 실패, 오류 코드: {error_code}")
                
            logger.info(f"Named Pipe 생성 완료: {self.pipe_name}")
            logger.info(f"Pipe Handle: {self.pipe_handle}")
            
            # 즉시 연결 대기 상태로 전환
            self.wait_for_connection()
            return True
            
        except Exception as e:
            logger.error(f"Named Pipe 생성 오류: {e}")
            return False
    
    def wait_for_connection(self):
        """클라이언트 연결 대기"""
        try:
            logger.info("클라이언트 연결 대기 중...")
            win32pipe.ConnectNamedPipe(self.pipe_handle, None)
            logger.info("클라이언트 연결됨")
            return True
            
        except pywintypes.error as e:
            if e.args[0] == 535:  # ERROR_PIPE_CONNECTED
                logger.info("클라이언트가 이미 연결됨")
                return True
            else:
                logger.error(f"연결 대기 오류: {e}")
                return False
                
    def read_message(self):
        """클라이언트로부터 메시지 읽기"""
        try:
            # 메시지 읽기
            result, data = win32file.ReadFile(self.pipe_handle, 64*1024)
            
            if result == 0:  # 성공
                # UTF-8 디코딩 및 BOM 제거
                message = data.decode('utf-8', errors='ignore').strip()
                
                # BOM 및 제어 문자 제거
                if message.startswith('\ufeff'):
                    message = message[1:]
                
                # 빈 메시지나 제어 문자만 있는 경우 필터링
                if not message or message.isspace():
                    logger.debug("빈 메시지 또는 공백 문자 수신됨")
                    return None
                    
                logger.info(f"수신된 메시지: '{message}'")
                return message
            else:
                logger.warning(f"메시지 읽기 결과 코드: {result}")
                return None
                
        except pywintypes.error as e:
            if e.args[0] == 109:  # ERROR_BROKEN_PIPE
                logger.info("파이프 연결이 끊어짐")
                return "DISCONNECT"
            elif e.args[0] == 232:  # ERROR_NO_DATA (pipe closing)
                logger.info("파이프가 닫히는 중")
                return "DISCONNECT"
            else:
                logger.error(f"메시지 읽기 오류: {e}")
                return None
        except Exception as e:
            logger.error(f"메시지 읽기 일반 오류: {e}")
            return None
                
    def send_message(self, message):
        """클라이언트에게 메시지 전송"""
        try:
            # BOM 없는 UTF-8 인코딩으로 메시지 전송
            data = (message + '\n').encode('utf-8')
            
            result, bytes_written = win32file.WriteFile(self.pipe_handle, data)
            
            if result == 0:  # 성공
                logger.info(f"메시지 전송 완료: '{message}' ({bytes_written} bytes)")
                return True
            else:
                logger.warning(f"메시지 전송 결과 코드: {result}")
                return False
                
        except pywintypes.error as e:
            if e.args[0] == 232:  # ERROR_NO_DATA (pipe closing)
                logger.warning("파이프가 닫히는 중 - 메시지 전송 무시")
                return False
            else:
                logger.error(f"메시지 전송 오류: {e}")
                return False
        except Exception as e:
            logger.error(f"메시지 전송 일반 오류: {e}")
            return False
            
    def process_message(self, message):
        """메시지 처리 및 응답 생성"""
        if message == "SHUTDOWN":
            return "SERVER_SHUTDOWN"
            
        # 간단한 에코 응답에 타임스탬프 추가
        timestamp = datetime.now().strftime("%H:%M:%S")
        response = f"[Python Echo {timestamp}] {message}"
        
        return response
        
    def disconnect_client(self):
        """클라이언트 연결 해제"""
        try:
            if self.pipe_handle:
                win32pipe.DisconnectNamedPipe(self.pipe_handle)
                logger.info("클라이언트 연결 해제됨")
                
        except Exception as e:
            logger.error(f"연결 해제 오류: {e}")
            
    def cleanup(self):
        """리소스 정리"""
        try:
            if self.pipe_handle:
                win32file.CloseHandle(self.pipe_handle)
                self.pipe_handle = None
                logger.info("Named Pipe 정리 완료")
                
        except Exception as e:
            logger.error(f"정리 중 오류: {e}")
            
    def run(self):
        """서버 메인 루프"""
        logger.info("Python Named Pipe Server 시작")
        
        # 파이프 생성 및 연결 대기를 한 번에 처리
        if not self.create_pipe():
            logger.error("서버 시작 실패")
            return
            
        self.is_running = True
        logger.info("서버가 클라이언트 연결을 대기 중...")
        
        try:
            # 메시지 처리 루프
            while self.is_running:
                message = self.read_message()
                
                if message is None:
                    time.sleep(0.1)  # CPU 사용률 감소
                    continue
                elif message == "DISCONNECT":
                    logger.info("클라이언트 연결 해제됨")
                    break
                elif message == "SHUTDOWN":
                    logger.info("종료 명령 수신")
                    if self.send_message("서버가 종료됩니다."):
                        time.sleep(0.5)  # 메시지 전송 완료 대기
                    self.is_running = False
                    break
                else:
                    # 메시지 처리 및 응답
                    response = self.process_message(message)
                    if not self.send_message(response):
                        logger.warning("응답 전송 실패, 연결 확인 중...")
                        # 전송 실패 시 연결 상태 확인을 위한 짧은 대기
                        time.sleep(0.1)
                    
        except KeyboardInterrupt:
            logger.info("Ctrl+C로 서버 종료")
        except Exception as e:
            logger.error(f"서버 실행 중 오류: {e}")
        finally:
            self.cleanup()
            logger.info("Python Named Pipe Server 종료")

def main():
    """메인 함수"""
    server = NamedPipeServer()
    server.run()

if __name__ == "__main__":
    main()