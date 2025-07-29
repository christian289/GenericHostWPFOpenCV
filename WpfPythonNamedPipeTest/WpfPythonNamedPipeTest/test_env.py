#!/usr/bin/env python3
"""
Python Named Pipe 테스트 스크립트
pywin32 설치 및 기본 기능 확인
"""

import sys
import os

def test_python_environment():
    """Python 환경 테스트"""
    print("=== Python 환경 테스트 ===")
    print(f"Python 버전: {sys.version}")
    print(f"Python 실행 파일: {sys.executable}")
    print(f"작업 디렉토리: {os.getcwd()}")
    print()

def test_pywin32_installation():
    """pywin32 설치 테스트"""
    print("=== pywin32 설치 테스트 ===")
    
    try:
        import win32pipe
        print("✓ win32pipe 모듈 가져오기 성공")
    except ImportError as e:
        print(f"✗ win32pipe 모듈 가져오기 실패: {e}")
        return False
    
    try:
        import win32file
        print("✓ win32file 모듈 가져오기 성공")
    except ImportError as e:
        print(f"✗ win32file 모듈 가져오기 실패: {e}")
        return False
        
    try:
        import win32api
        print("✓ win32api 모듈 가져오기 성공")
    except ImportError as e:
        print(f"✗ win32api 모듈 가져오기 실패: {e}")
        return False
        
    try:
        import pywintypes
        print("✓ pywintypes 모듈 가져오기 성공")
    except ImportError as e:
        print(f"✗ pywintypes 모듈 가져오기 실패: {e}")
        return False
    
    print()
    return True

def test_named_pipe_creation():
    """Named Pipe 생성 테스트"""
    print("=== Named Pipe 생성 테스트 ===")
    
    try:
        import win32pipe
        import win32file
        
        pipe_name = r"\\.\pipe\TestPipe"
        print(f"테스트 파이프 이름: {pipe_name}")
        
        pipe_handle = win32pipe.CreateNamedPipe(
            pipe_name,
            win32pipe.PIPE_ACCESS_DUPLEX,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536, 0, None
        )
        
        if pipe_handle == win32file.INVALID_HANDLE_VALUE:
            print("✗ Named Pipe 생성 실패")
            return False
        else:
            print("✓ Named Pipe 생성 성공")
            print(f"  파이프 핸들: {pipe_handle}")
            
            # 파이프 정리
            win32file.CloseHandle(pipe_handle)
            print("✓ Named Pipe 정리 완료")
            return True
            
    except Exception as e:
        print(f"✗ Named Pipe 생성 테스트 실패: {e}")
        return False

def main():
    """메인 함수"""
    print("Python Named Pipe 환경 테스트 시작\n")
    
    # 1. Python 환경 테스트
    test_python_environment()
    
    # 2. pywin32 설치 테스트
    if not test_pywin32_installation():
        print("\n권장 해결책:")
        print("1. pip install pywin32")
        print("2. python -m pip install --upgrade pywin32")
        print("3. 관리자 권한으로 실행: python Scripts/pywin32_postinstall.py -install")
        return
    
    # 3. Named Pipe 생성 테스트
    if test_named_pipe_creation():
        print("\n✓ 모든 테스트 통과! Named Pipe 서버를 실행할 수 있습니다.")
    else:
        print("\n✗ Named Pipe 생성에 문제가 있습니다.")
        print("관리자 권한으로 실행해보거나, 보안 소프트웨어를 확인하세요.")

if __name__ == "__main__":
    main()
    input("\nEnter 키를 눌러 종료...")