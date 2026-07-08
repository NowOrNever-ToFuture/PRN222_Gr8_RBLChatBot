# SỔ TAY HƯỚNG DẪN TOÀN TẬP: SETUP AI LOCAL VÀ KỊCH BẢN DEMO FLOWS

**Dự án:** Chatbot Hỗ Trợ Học Tập Kết Hợp Nghiên Cứu RBL (PRN222)  
**Kiến trúc hệ thống:** Hybrid Microservices, C# .NET Core 3-Layer + Python FastAPI AI Server  
**Cơ sở dữ liệu:** SQL Server, EF Core Code-First, toàn bộ PK/FK dùng GUID

---

## PHẦN 1: CÀI ĐẶT MÔI TRƯỜNG PYTHON BACKEND

Phân hệ Python đóng vai trò là microservice đảm nhận các tác vụ tính toán học máy nặng: chuyển đổi định dạng tài liệu sang Markdown/JSON, cắt nhỏ văn bản thông minh và xử lý vector embedding.

### Bước 1: Khởi tạo môi trường ảo

Mở CMD hoặc PowerShell tại thư mục dự án:

```bat
cd Python_RAG_Server
python -m venv venv
venv\Scripts\activate
```

Khi kích hoạt thành công, đầu dòng lệnh sẽ xuất hiện:

```text
(venv) Python_RAG_Server>
```

### Bước 2: Cài đặt thư viện

Ưu tiên dùng file `requirements.txt` đã đặt trong thư mục này:

```bat
pip install -r requirements.txt
```

Nếu muốn cài thủ công:

```bat
pip install fastapi uvicorn pydantic python-multipart sentence-transformers pandas openpyxl python-docx python-pptx pypdf tabulate
```

### Bước 3: Kiểm tra mã nguồn AI server

File server chính nằm tại:

```text
Python_RAG_Server/api_server.py
```

Server hiện hỗ trợ các API:

- `GET /api/health`: kiểm tra server và danh sách model đã load.
- `POST /api/embed`: sinh vector embedding cho một đoạn text.
- `POST /api/parse-document`: upload tài liệu, parse sang Markdown, chunking và embedding.

Các model local được load:

- `bge-m3`: `BAAI/bge-m3`
- `e5`: `intfloat/multilingual-e5-base`
- `phobert`: `keepitreal/vietnamese-sbert`

### Bước 4: Khởi chạy máy chủ Python

```bat
python -m uvicorn api_server:app --port 8000
```

Lần đầu chạy, hệ thống sẽ tải model từ HuggingFace về máy cục bộ. Dung lượng có thể khoảng 3GB-4GB tùy cache hiện có.

Kiểm tra server:

```text
http://localhost:8000/api/health
```

Khi demo, giữ nguyên cửa sổ terminal này để hội đồng thấy request từ web C# gọi sang Python theo thời gian thực.

---

## PHẦN 2: CÀI ĐẶT HỆ THỐNG C# .NET WEB APPLICATION

### Bước 1: Kiểm tra cấu hình

Mở solution:

```text
PRN222_Project/PRN222_Project.sln
```

Kiểm tra file:

```text
PRN222_Project/PRN222.RazorWebApp/appsettings.json
```

Cần đảm bảo:

- `ConnectionStrings:DefaultConnection` trỏ đúng SQL Server local.
- `AIProviders:PythonMicroservice:BaseUrl` là `http://localhost:8000/`.
- `AIProviders:OpenAI:ApiKey` đã được cấu hình nếu dùng chức năng gọi OpenAI.

### Bước 2: Đồng bộ cơ sở dữ liệu

Trong Visual Studio, mở Package Manager Console, chọn Default Project là project repository/data access, sau đó chạy:

```powershell
Update-Database
```

Mục tiêu là tạo cấu trúc bảng theo EF Core migration và thiết lập quan hệ dữ liệu.

### Bước 3: Chạy ứng dụng web

Bấm `F5` trong Visual Studio hoặc chạy bằng CLI:

```bat
cd PRN222_Project\PRN222.RazorWebApp
dotnet run
```

---

## PHẦN 3: TROUBLESHOOTING

### Lỗi: `uvicorn is not recognized`

Nguyên nhân thường là chưa kích hoạt đúng môi trường ảo.

Cách xử lý:

```bat
cd Python_RAG_Server
venv\Scripts\activate
python -m uvicorn api_server:app --port 8000
```

### Lỗi: `Could not import module "api_server"`

Nguyên nhân là terminal không đứng trong thư mục chứa `api_server.py`.

Cách xử lý:

```bat
cd Python_RAG_Server
python -m uvicorn api_server:app --port 8000
```

### Lỗi: tải model quá lâu ở lần chạy đầu

Đây là hành vi bình thường vì server phải tải model từ HuggingFace. Giữ kết nối internet ổn định và chờ cache model hoàn tất.

### Lỗi: PowerShell chặn chạy script setup

Nếu chạy `.\init-prn222-project.ps1` bị lỗi `running scripts is disabled`, dùng:

```powershell
powershell -ExecutionPolicy Bypass -File .\init-prn222-project.ps1
```

---

## PHẦN 4: KỊCH BẢN DEMO FLOWS

Khi thuyết minh trước hội đồng, nên chia màn hình:

- Nửa bên trái: trình duyệt web C#.
- Nửa bên phải: terminal Python FastAPI.

### FLOW 1: Tạo lập kho tri thức thông minh

**Thao tác UI:** Admin đăng nhập, vào "Quản lý Kho Tri Thức", chọn môn `PRN222`, upload một file PDF hoặc Excel, sau đó bấm "Lưu và Indexing tài liệu".

**Quan sát terminal Python:** Terminal hiển thị request HTTP POST, server parse tài liệu sang Markdown, chia chunk theo heading và sinh vector embedding.

**Minh chứng DB:**

```sql
SELECT * FROM DocumentChunks;
```

**Lời thoại gợi ý:**  
"Thưa thầy cô, hệ thống không chỉ lưu file nhị phân truyền thống. Tài liệu sau khi upload được đẩy qua API Python để chuẩn hóa thành Markdown máy đọc, sau đó chia chunk và sinh vector embedding. Cách lưu nội dung có cấu trúc kèm vector trong SQL Server giúp chatbot truy xuất đúng ngữ cảnh hơn so với việc cào text thô."

### FLOW 2: Chat hỏi đáp thông minh

**Thao tác UI:** Sinh viên mở chatbot và hỏi:

```text
Lớp trừu tượng khác gì Interface trong .NET?
```

**Kết quả mong đợi:** Chatbot trả lời dựa trên tài liệu và hiển thị nguồn trích dẫn.

**Test edge-case:** Hỏi một câu ngoài phạm vi:

```text
Công thức nấu lẩu thái ngon?
```

Chatbot cần từ chối hoặc báo câu hỏi nằm ngoài phạm vi tài liệu đào tạo.

**Lời thoại gợi ý:**  
"Hệ thống dùng Smart Routing để khoanh vùng môn học và metadata phù hợp. Sau đó bộ lọc cosine similarity threshold giúp chặn các câu hỏi không khớp với kho tri thức, giảm nguy cơ AI bịa đặt thông tin."

### FLOW 3: Làm việc nhóm thời gian thực

**Thao tác UI:** Mở hai trình duyệt, đăng nhập Admin A và Admin B, cùng truy cập trang quản lý câu hỏi kiểm thử.

**Thêm mới:** Admin A import Excel có nhiều câu hỏi. Admin B thấy dữ liệu mới xuất hiện mà không cần refresh.

**Sửa/Xóa:** Admin A sửa một câu hỏi, Admin B thấy dòng cập nhật. Admin B xóa một câu hỏi, Admin A thấy dòng biến mất.

**Lời thoại gợi ý:**  
"SignalR Hub đồng bộ trạng thái giữa các client đang kết nối. Sau khi service ghi thay đổi xuống SQL Server, web app phát thông điệp tới client để cập nhật DOM theo thời gian thực."

### FLOW 4: RBL Benchmark Dashboard

**Thao tác UI:** Admin vào trang benchmark, chọn:

```text
Model: bge-m3
Chunking: markdown_header
```

Sau đó bấm "Khởi chạy thực nghiệm Benchmark".

**Kết quả mong đợi:** Progress bar cập nhật liên tục, bảng kết quả hiển thị điểm theo từng câu hỏi, dashboard vẽ biểu đồ bằng Chart.js.

**Lời thoại gợi ý:**  
"Đây là phần nghiên cứu RBL của đồ án. Hệ thống chạy bộ câu hỏi kiểm thử, dùng LLM-as-a-Judge để chấm Faithfulness và Relevance, từ đó so sánh các cấu hình model và chunking ngay trên dashboard."

---

## Checklist Demo Nhanh

- SQL Server đang chạy.
- Đã chạy `Update-Database`.
- Python venv đã được kích hoạt.
- Python server đang chạy tại `http://localhost:8000`.
- Web app trỏ đúng `PythonMicroservice:BaseUrl`.
- Có sẵn file PDF/Excel để upload demo.
- Có sẵn tài khoản Admin và Sinh viên để test các flow.
