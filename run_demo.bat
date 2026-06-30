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
if not exist venv (
    echo [INFO] Dang tao moi truong ao venv...
    python -m venv venv
) else (
    echo [INFO] Moi truong ao venv da ton tai.
)
echo [INFO] Kich hoat venv va tien hanh cai dat cac thu vien...
call venv\Scripts\activate
pip install -r requirements.txt
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
call venv\Scripts\activate
python -m uvicorn api_server:app --host 0.0.0.0 --port 8000
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
start "Python FastAPI RAG Server" cmd /k "cd /d "%~dp0Python_RAG_Server" && call venv\Scripts\activate && python -m uvicorn api_server:app --host 0.0.0.0 --port 8000"

echo [INFO] Cho 3 giay de Python AI Server san sang...
timeout /t 3 /nobreak > nul

echo [INFO] Buoc 2: Bat dau khoi chay C# .NET Web App...
start "C# .NET Core WebApp" cmd /k "cd /d "%~dp0PRN222_Project\PRN222.RazorWebApp" && dotnet run"

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

