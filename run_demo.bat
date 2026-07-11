@echo off
title PRN222 RAG Chatbot - Live Demo Controller
color 0B

:menu
cls
echo =======================================================================
echo          PRN222 RAG CHATBOT - AUTOMATED DEMO LAUNCHER
echo =======================================================================
echo.
echo   [1] Khoi tao Moi truong ao Python va Cai dat cac thu vien (Requirements)
echo   [2] Khoi chay Python FastAPI AI Server (Port 8000)
echo   [3] Khoi chay C# .NET Core Web App (Port 5274)
echo   [4] KHOI CHAY CA HAI MICROSERVICES ^& Tu dong mo Trinh duyet Chrome
echo   [5] Thoat chuong trinh
echo.
echo =======================================================================
set /p opt="Vui long chon chuc nang (1-5): "

if "%opt%"=="1" goto setup_env
if "%opt%"=="2" goto run_python
if "%opt%"=="3" goto run_dotnet
if "%opt%"=="4" goto run_both
if "%opt%"=="5" goto exit_menu
goto menu

:setup_env
cls
echo === DANG KHOI TAO MOI TRUONG AO PYTHON & INSTALL REQUIREMENTS ===
echo.
cd /d "%~dp0Python_RAG_Server"
if exist venv311\Scripts\python.exe (
    set "PYTHON_ENV=venv311\Scripts\python.exe"
) else if exist D:\Python311\python.exe (
    echo [INFO] Dang tao moi truong ao Python 3.11...
    D:\Python311\python.exe -m venv venv311
    set "PYTHON_ENV=venv311\Scripts\python.exe"
) else (
    if not exist venv\Scripts\python.exe (
        echo [INFO] Dang tao moi truong ao Python mac dinh...
        python -m venv venv
    )
    set "PYTHON_ENV=venv\Scripts\python.exe"
)
echo [INFO] Cai dat requirements vao moi truong Python...
"%PYTHON_ENV%" -m pip install -r requirements.txt
echo.
echo === SETUP HOAN TAT! ===
pause
goto menu

:run_python
cls
echo === DANG KHOI CHAY PYTHON FASTAPI AI SERVER ===
echo [INFO] Server se chay tai dia chi: http://127.0.0.1:8000 hoac http://localhost:8000
echo.
cd /d "%~dp0Python_RAG_Server"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Python_RAG_Server\start_server.ps1"
pause
goto menu

:run_dotnet
cls
echo === DANG KHOI CHAY C# .NET WEB APPLICATION ===
echo [INFO] Web App se chay tai dia chi: http://localhost:5274
echo.
cd /d "%~dp0PRN222_Project\PRN222.RazorWebApp"
dotnet run
pause
goto menu

:run_both
cls
echo === DANG KHOI CHAY SONG SONG CA HAI MICROSERVICES ===
echo [INFO] Buoc 1: Bat dau khoi chay Python FastAPI...
start "Python FastAPI AI Gateway" powershell -NoExit -NoProfile -ExecutionPolicy Bypass -File "%~dp0Python_RAG_Server\start_server.ps1"

echo [INFO] Dang cho FastAPI AI Gateway san sang...
for /l %%i in (1,1,60) do (
    powershell -NoProfile -Command "try { Invoke-WebRequest -UseBasicParsing http://127.0.0.1:8000/api/health -TimeoutSec 2 ^| Out-Null; exit 0 } catch { exit 1 }" > nul 2>&1
    if not errorlevel 1 goto python_ready
    timeout /t 2 /nobreak > nul
)
echo [ERROR] FastAPI khong san sang sau 120 giay. Kiem tra cua so Python FastAPI AI Gateway.
pause
goto menu

:python_ready
echo [INFO] FastAPI da san sang voi GPT, Gemini va Qwen.

echo [INFO] Buoc 2: Bat dau khoi chay C# .NET Web App...
start "C# .NET Core WebApp" cmd /k "cd /d ""%~dp0PRN222_Project\PRN222.RazorWebApp"" && dotnet run"

echo [INFO] Cho 4 giay de Web App khoi dong...
timeout /t 4 /nobreak > nul

echo [INFO] Buoc 3: Tu dong mo Trinh duyet Chrome den trang web...
start http://localhost:5274

echo.
echo === DA GHE CO KICH HOAT THANH CONG SONG SONG CA HAI WINDOWS! ===
echo Ban co the xem log cua tung Microservice o 2 cua so CMD vua duoc mo ra.
echo.
pause
goto menu

:exit_menu
exit

