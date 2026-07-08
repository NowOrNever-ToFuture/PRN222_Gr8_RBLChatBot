# PRN222 RBL Chatbot

Chatbot hỗ trợ học tập kết hợp nghiên cứu RBL, xây dựng theo kiến trúc hybrid microservices:

- Web application: C# ASP.NET Core MVC, mô hình 3 lớp.
- AI microservice: Python FastAPI xử lý parse tài liệu, chunking và embedding local.
- Database: SQL Server, EF Core Code-First, khóa chính/khóa ngoại dùng GUID.

## Cấu Trúc Repo

```text
PRN222_Project/          # Solution .NET
Python_RAG_Server/       # FastAPI AI microservice
init-prn222-project.ps1  # Script hỗ trợ setup môi trường
```

## Setup Nhanh

Chạy trong PowerShell tại thư mục repo:

```powershell
.\init-prn222-project.ps1
```

Nếu Windows chặn chạy script với lỗi `running scripts is disabled`, dùng lệnh:

```powershell
powershell -ExecutionPolicy Bypass -File .\init-prn222-project.ps1
```

Script sẽ:

- Kiểm tra `dotnet` và `python`.
- Restore package .NET nếu solution đã tồn tại.
- Tạo `Python_RAG_Server/venv` nếu chưa có.
- Cài Python dependencies từ `Python_RAG_Server/requirements.txt`.

Nếu chỉ muốn restore .NET mà chưa cài Python dependencies:

```powershell
.\init-prn222-project.ps1 -SkipPythonVenv
```

## Chạy Python AI Server

```bat
cd Python_RAG_Server
venv\Scripts\activate
python -m uvicorn api_server:app --port 8000
```

Health check:

```text
http://localhost:8000/api/health
```

Lần đầu chạy, server sẽ tải các embedding model từ HuggingFace về máy.

## Chạy Web App

1. Mở `PRN222_Project/PRN222_Project.sln` bằng Visual Studio.
2. Kiểm tra `PRN222_Project/PRN222.RazorWebApp/appsettings.json`.
3. Chạy migration trong Package Manager Console:

```powershell
Update-Database
```

4. Bấm `F5` để chạy web app.

## Tài Liệu Demo

Xem hướng dẫn setup AI local và kịch bản demo đầy đủ tại:

```text
Python_RAG_Server/README.md
```
